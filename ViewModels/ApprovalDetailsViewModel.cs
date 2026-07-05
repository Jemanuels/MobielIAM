using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.ViewModels {
    public partial class ApprovalDetailsViewModel : ObservableObject, IQueryAttributable {
        private readonly IAccessRequestService _requests;
        private readonly ICurrentUserService _currentUser;

        public ApprovalDetailsViewModel(
            IAccessRequestService requests,
            ICurrentUserService currentUser) {
            _requests = requests;
            _currentUser = currentUser;
        }

        // ---- Load state ----
        [ObservableProperty] private bool _isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLoadError))]
        private string? _loadError;
        public bool HasLoadError => !string.IsNullOrEmpty(LoadError);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasActionError))]
        private string? _errorMessage;
        public bool HasActionError => !string.IsNullOrEmpty(ErrorMessage);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRequest))]
        private AccessRequest? _request;
        public bool HasRequest => Request is not null;

        // ---- Display fields ----
        [ObservableProperty] private string _businessSystem = string.Empty;
        [ObservableProperty] private string _accessRole = string.Empty;
        [ObservableProperty] private RequestStatus _status;
        [ObservableProperty] private string _statusLabel = string.Empty;
        [ObservableProperty] private string _justification = string.Empty;
        [ObservableProperty] private string _requesterName = string.Empty;
        [ObservableProperty] private string _requesterEmail = string.Empty;
        [ObservableProperty] private string _submittedAtDisplay = string.Empty;

        // ---- Decision form ----
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
        [NotifyCanExecuteChangedFor(nameof(RejectCommand))]
        private string _comments = string.Empty;

        [ObservableProperty] private bool _canDecide;
        [ObservableProperty] private bool _alreadyDecided;
        [ObservableProperty] private string _decisionSummary = string.Empty;

        // ---- Self-approval guard ----
        [ObservableProperty] private bool _isSelfRequest;

        public void ApplyQueryAttributes(IDictionary<string, object> query) {
            if (query.TryGetValue("requestId", out var idObj) && idObj is string id) {
                _ = LoadAsync(id);
            }
        }

        private async Task LoadAsync(string requestId) {
            IsBusy = true;
            LoadError = null;
            try {
                var r = await _requests.GetByIdAsync(requestId);
                if (r is null) {
                    LoadError = "Request not found or you don't have permission to view it.";
                    return;
                }
                ApplyRequest(r);
            }
            catch (Exception ex) {
                LoadError = $"Couldn't load request: {ex.Message}";
            }
            finally {
                IsBusy = false;
            }
        }

        private void ApplyRequest(AccessRequest r) {
            Request = r;

            BusinessSystem = r.BusinessSystemName;
            AccessRole = r.AccessRoleRequested;
            Status = r.Status;
            StatusLabel = r.Status.ToString();
            Justification = r.Justification;
            RequesterName = r.RequesterDisplayName;
            RequesterEmail = r.RequesterEmail;
            SubmittedAtDisplay = r.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

            var me = _currentUser.Current;
            IsSelfRequest = me is not null && me.Uid == r.RequesterUid;

            var isPending = r.Status == RequestStatus.Submitted
                            || r.Status == RequestStatus.PendingApproval;
            CanDecide = isPending && !IsSelfRequest;
            AlreadyDecided = !isPending;
            DecisionSummary = AlreadyDecided
                ? $"This request has already been {r.Status.ToString().ToLowerInvariant()}" +
                  (string.IsNullOrWhiteSpace(r.ApproverDisplayName) ? "." : $" by {r.ApproverDisplayName}.")
                : string.Empty;
        }

        // ---- Commands ----

        private bool CanApprove() =>
            !IsBusy && CanDecide;

        private bool CanReject() =>
            !IsBusy && CanDecide && !string.IsNullOrWhiteSpace(Comments);

        [RelayCommand(CanExecute = nameof(CanApprove))]
        private async Task ApproveAsync() {
            var me = _currentUser.Current;
            if (Request is null || me is null || Shell.Current is null) return;

            var confirmed = await Shell.Current.DisplayAlert(
                "Approve request",
                $"Approve {Request.RequesterDisplayName}'s request for {Request.BusinessSystemName}?",
                "Approve",
                "Cancel");
            if (!confirmed) return;

            await PerformDecisionAsync(approve: true, me);
        }

        [RelayCommand(CanExecute = nameof(CanReject))]
        private async Task RejectAsync() {
            var me = _currentUser.Current;
            if (Request is null || me is null || Shell.Current is null) return;

            var confirmed = await Shell.Current.DisplayAlert(
                "Reject request",
                $"Reject {Request.RequesterDisplayName}'s request? They'll see your comments.",
                "Reject",
                "Cancel");
            if (!confirmed) return;

            await PerformDecisionAsync(approve: false, me);
        }

        private async Task PerformDecisionAsync(bool approve, AppUser me) {
            IsBusy = true;
            ErrorMessage = null;
            try {
                var approverName = string.IsNullOrWhiteSpace(me.DisplayName) ? me.Email : me.DisplayName;

                if (approve) {
                    await _requests.ApproveAsync(Request!.Id, me.Uid, approverName, Comments);
                }
                else {
                    await _requests.RejectAsync(Request!.Id, me.Uid, approverName, Comments);
                }

                if (Shell.Current is not null) {
                    await Shell.Current.DisplayAlert(
                        approve ? "Approved" : "Rejected",
                        approve ? "The request has been approved." : "The request has been rejected.",
                        "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't save decision: {ex.Message}";
            }
            finally {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoBackAsync() {
            if (Shell.Current is not null) {
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}
