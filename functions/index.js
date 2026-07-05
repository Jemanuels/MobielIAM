/*
 * onAccessRequestUpdated
 * ----------------------
 * Triggered when any accessRequests/{id} document is updated.
 *
 * If the status just transitioned to "Approved" and we haven't already
 * provisioned this request, we:
 *
 *   1. Load the business system document to get the Entra service
 *      principal ID and the appRoleId for the requested role.
 *   2. Authenticate to Microsoft Graph via the client credentials flow
 *      (using the IAM IGA app registration's client ID + secret).
 *   3. Look up the requester in Entra by email.
 *   4. POST an appRoleAssignment to add them to the target enterprise app.
 *   5. Write provisioningStatus back to the request doc so the mobile
 *      app can show it.
 *   6. Write an audit log entry.
 *
 * The function is idempotent: if provisioningStatus is already set it
 * skips. Failures are caught and written as ProvisioningStatus="Failed"
 * with a human-readable message so the user can see what went wrong
 * without having to open the function logs.
 *
 * Secrets / config:
 *   ENTRA_CLIENT_SECRET   → defineSecret, set via `firebase functions:secrets:set`
 *   ENTRA_TENANT_ID       → from .env.<projectId>
 *   ENTRA_CLIENT_ID       → from .env.<projectId>
 */

const { onDocumentUpdated } = require('firebase-functions/v2/firestore');
const { defineSecret } = require('firebase-functions/params');
const { logger } = require('firebase-functions/v2');
const { initializeApp } = require('firebase-admin/app');
const { getFirestore, FieldValue } = require('firebase-admin/firestore');

initializeApp();
const db = getFirestore();

const entraClientSecret = defineSecret('ENTRA_CLIENT_SECRET');

exports.onAccessRequestUpdated = onDocumentUpdated(
    {
        document: 'accessRequests/{requestId}',
        secrets: [entraClientSecret],
        region: 'us-central1',
    },
    async (event) => {
        const before = event.data?.before?.data();
        const after = event.data?.after?.data();
        const requestId = event.params.requestId;

        if (!before || !after) return;

        // Only act on the transition INTO Approved.
        const wasApproved = before.status === 'Approved';
        const isApproved = after.status === 'Approved';
        if (wasApproved || !isApproved) {
            return;
        }

        // Idempotency: if we've already provisioned (or failed), don't re-run.
        // Without this, restarts or retries could double-assign roles.
        if (after.provisioningStatus) {
            logger.info(`Request ${requestId}: provisioningStatus is "${after.provisioningStatus}", skipping`);
            return;
        }

        logger.info(
            `Request ${requestId}: starting provisioning for ${after.requesterEmail} ` +
            `(${after.businessSystemName} / ${after.accessRoleRequested})`
        );

        const ref = db.collection('accessRequests').doc(requestId);

        try {
            // Tell the app something is happening.
            await ref.update({
                provisioningStatus: 'InProgress',
                provisioningMessage: 'Connecting to Microsoft Entra...',
                updatedAt: FieldValue.serverTimestamp(),
            });

            // 1. Load the business system for the Entra mapping.
            const systemId = after.businessSystemId;
            if (!systemId) {
                throw new Error('Request has no businessSystemId. Cannot map to an Entra application.');
            }

            const systemSnap = await db.collection('businessSystems').doc(systemId).get();
            if (!systemSnap.exists) {
                throw new Error(`Business system ${systemId} not found.`);
            }
            const system = systemSnap.data();

            const servicePrincipalId = system.entraServicePrincipalId;
            if (!servicePrincipalId) {
                throw new Error(
                    `Business system "${system.name}" has no Entra service principal configured. ` +
                    `Edit the system in the Admin pages and add the service principal Object ID.`
                );
            }

            const roleMappings = system.entraAppRoleMappings || {};
            const appRoleId = roleMappings[after.accessRoleRequested];
            if (!appRoleId) {
                throw new Error(
                    `No Entra app role configured for "${after.accessRoleRequested}" in system "${system.name}". ` +
                    `Edit the system and add a role mapping line: "${after.accessRoleRequested}=<role-guid>".`
                );
            }

            // 2. Get a Graph token (client credentials).
            const tenantId = process.env.ENTRA_TENANT_ID;
            const clientId = process.env.ENTRA_CLIENT_ID;
            if (!tenantId || !clientId) {
                throw new Error('ENTRA_TENANT_ID and ENTRA_CLIENT_ID env vars are required.');
            }
            const accessToken = await getGraphToken(tenantId, clientId, entraClientSecret.value());

            // 3. Find the user in Entra by email.
            const userId = await findUserByEmail(accessToken, after.requesterEmail);

            // 4. Assign the app role.
            await assignAppRole(accessToken, servicePrincipalId, userId, appRoleId);

            // 5. Write success.
            const successMessage = `Assigned "${after.accessRoleRequested}" in "${system.name}".`;
            await ref.update({
                provisioningStatus: 'Completed',
                provisioningMessage: successMessage,
                provisionedAt: FieldValue.serverTimestamp(),
                updatedAt: FieldValue.serverTimestamp(),
            });

            // 6. Audit log entry. Only Cloud Functions (Admin SDK) can write here —
            // Firestore rules block all client writes to auditLogs.
            await writeAuditLog({
                action: 'Provisioned',
                requestId,
                request: after,
                message: successMessage,
            });

            logger.info(`Request ${requestId}: provisioning completed`);
        } catch (err) {
            logger.error(`Request ${requestId} failed: ${err.message}`, err);

            // Best-effort: write the failure back. Don't let a write-error swallow the original.
            try {
                await ref.update({
                    provisioningStatus: 'Failed',
                    provisioningMessage: err.message,
                    updatedAt: FieldValue.serverTimestamp(),
                });
                await writeAuditLog({
                    action: 'ProvisioningFailed',
                    requestId,
                    request: after,
                    message: err.message,
                });
            } catch (writeErr) {
                logger.error('Also failed to record the failure:', writeErr);
            }
        }
    }
);

// =====================================================================
// Audit log
// =====================================================================

async function writeAuditLog({ action, requestId, request, message }) {
    await db.collection('auditLogs').add({
        action,
        requestId,
        requesterUid: request.requesterUid || null,
        requesterEmail: request.requesterEmail || null,
        requesterDisplayName: request.requesterDisplayName || null,
        businessSystemId: request.businessSystemId || null,
        businessSystemName: request.businessSystemName || null,
        accessRoleRequested: request.accessRoleRequested || null,
        approverUid: request.approverUid || null,
        approverDisplayName: request.approverDisplayName || null,
        performedBy: 'system',
        performedByName: 'Cloud Function',
        message,
        timestamp: FieldValue.serverTimestamp(),
    });
}

// =====================================================================
// Microsoft Graph helpers
// =====================================================================

/**
 * Client credentials flow: trade client_id + secret for an access token
 * scoped to all of Microsoft Graph (.default scope).
 */
async function getGraphToken(tenantId, clientId, clientSecret) {
    const url = `https://login.microsoftonline.com/${tenantId}/oauth2/v2.0/token`;
    const body = new URLSearchParams({
        client_id: clientId,
        client_secret: clientSecret,
        scope: 'https://graph.microsoft.com/.default',
        grant_type: 'client_credentials',
    });

    const resp = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body,
    });

    const data = await resp.json().catch(() => ({}));
    if (!resp.ok) {
        throw new Error(`Token request failed: ${data.error_description || data.error || resp.statusText}`);
    }
    return data.access_token;
}

/**
 * Find an Entra user by email. Tries UPN lookup first (most common case
 * for M365 dev tenant users) then falls back to filtering by mail.
 */
async function findUserByEmail(accessToken, email) {
    if (!email) {
        throw new Error('Cannot look up user: requester email is missing.');
    }

    // 1. Direct UPN lookup (fast, works when email == UPN).
    const upnLookup = await fetch(
        `https://graph.microsoft.com/v1.0/users/${encodeURIComponent(email)}`,
        { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    if (upnLookup.ok) {
        const user = await upnLookup.json();
        return user.id;
    }
    // 404 here just means the UPN didn't match — fall through to filter.
    if (upnLookup.status !== 404) {
        const errText = await upnLookup.text();
        throw new Error(`Graph user UPN lookup failed (${upnLookup.status}): ${errText}`);
    }

    // 2. Filter by mail attribute (covers users with mail != UPN).
    const escaped = email.replace(/'/g, "''");
    const filterResp = await fetch(
        `https://graph.microsoft.com/v1.0/users?$filter=mail eq '${escaped}'`,
        { headers: { Authorization: `Bearer ${accessToken}` } }
    );
    if (!filterResp.ok) {
        const errText = await filterResp.text();
        throw new Error(`Graph user filter lookup failed (${filterResp.status}): ${errText}`);
    }

    const data = await filterResp.json();
    if (!data.value || data.value.length === 0) {
        throw new Error(
            `User with email "${email}" not found in your Entra directory. ` +
            `Ensure the user exists in Entra ID before approving requests for them.`
        );
    }
    return data.value[0].id;
}

/**
 * Create an app role assignment that puts the user into the target role
 * on the specified service principal. Returns silently if the user
 * already has that role (treated as success — assignment is the goal).
 */
async function assignAppRole(accessToken, servicePrincipalId, userId, appRoleId) {
    const url = `https://graph.microsoft.com/v1.0/servicePrincipals/${servicePrincipalId}/appRoleAssignedTo`;
    const body = {
        principalId: userId,
        resourceId: servicePrincipalId,
        appRoleId,
    };

    const resp = await fetch(url, {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${accessToken}`,
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
    });

    if (resp.ok) return;

    const errText = await resp.text();
    // Graph returns this when the user already has this role — fine, no-op.
    if (errText.includes('Permission being assigned was already assigned')) {
        logger.info('User already has this role; treating as success.');
        return;
    }
    throw new Error(`Graph role assignment failed (${resp.status}): ${errText}`);
}
