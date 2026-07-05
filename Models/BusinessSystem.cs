using System;
using System.Collections.Generic;

namespace MauiApp1.Models {
    public class BusinessSystem {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public List<string> AvailableRoles { get; set; } = new();

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ---- Entra ID mapping ----
        // These two fields tell the Cloud Function how to provision a user
        // into a real enterprise application in Microsoft Entra.
        //
        // EntraServicePrincipalId is the Object ID of the target enterprise
        // application's service principal (NOT the App Registration object ID).
        //
        // EntraAppRoleMappings maps a friendly role name (the one shown in our
        // dropdown) to Entra's appRoleId GUID. For example:
        //   "Standard User"  -> "63406193-1ac3-4d89-9b08-476b439ab42b"
        //   "Read only"      -> "8bd6b077-4146-4b0d-897b-bb188b2d9692"
        //
        // Both fields are optional. If they're missing, the Cloud Function
        // will mark the request as ProvisioningStatus=Failed with a clear
        // message and the admin can fill them in and retry.
        public string EntraServicePrincipalId { get; set; } = string.Empty;
        public Dictionary<string, string> EntraAppRoleMappings { get; set; } = new();
    }
}
