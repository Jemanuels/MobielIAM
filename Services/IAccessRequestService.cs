using System.Collections.Generic;
using System.Threading.Tasks;
using MauiApp1.Models;

namespace MauiApp1.Services {
    public interface IAccessRequestService {
        Task<string> CreateAsync(AccessRequest request);
        Task<IList<AccessRequest>> GetMineAsync(string requesterUid);
        Task<AccessRequest?> GetByIdAsync(string requestId);
        Task CancelAsync(string requestId);

        // Returns all requests currently awaiting a decision (Submitted or
        // PendingApproval), across all users. Used by approvers and admins.
        // Firestore rules restrict this to those roles.
        Task<IList<AccessRequest>> GetPendingAsync();

        // Approver actions. Both PATCH the document with decision fields.
        // approverUid / approverDisplayName identify who made the call.
        Task ApproveAsync(string requestId, string approverUid, string approverDisplayName, string? comments);
        Task RejectAsync(string requestId, string approverUid, string approverDisplayName, string? comments);
    }
}
