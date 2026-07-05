using System;

namespace MauiApp1.Models {
    public class AccessRequest {
        public string Id { get; set; } = string.Empty;

        // Who is asking
        public string RequesterUid { get; set; } = string.Empty;
        public string RequesterDisplayName { get; set; } = string.Empty;
        public string RequesterEmail { get; set; } = string.Empty;

        // What they're asking for.
        // BusinessSystemId is the canonical reference; BusinessSystemName is
        // denormalised so the request still makes sense if the system is
        // renamed or deactivated later.
        public string BusinessSystemId { get; set; } = string.Empty;
        public string BusinessSystemName { get; set; } = string.Empty;
        public string AccessRoleRequested { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;

        // Workflow
        public RequestStatus Status { get; set; } = RequestStatus.Draft;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Decision
        public string? ApproverUid { get; set; }
        public string? ApproverDisplayName { get; set; }
        public string? ApprovalComments { get; set; }
        public DateTime? DecidedAt { get; set; }

        // Provisioning (filled by Cloud Function later)
        public string? ProvisioningStatus { get; set; }
        public string? ProvisioningMessage { get; set; }
        public DateTime? ProvisionedAt { get; set; }
    }
}
