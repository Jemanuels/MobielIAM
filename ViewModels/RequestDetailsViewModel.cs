using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.ViewModels {
    public partial class RequestDetailsViewModel : ObservableObject, IQueryAttributable {
        private readonly IAccessRequestService _requests;
        private readonly ICurrentUserService _currentUser;

        public RequestDetailsViewModel(
            IAccessRequestService requests,
            ICurrentUserService currentUser) {
            _requests = requests;
            _currentUser = currentUser;
        }

        // ---- Bound state ----
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

        // Header fields
        [ObservableProperty] private string _businessSystem = string.Empty;
        [ObservableProperty] private string _accessRole = string.Empty;
        [ObservableProperty] private RequestStatus _status;
        [ObservableProperty] private string _statusLabel = string.Empty;
        [ObservableProperty] private string _justification = string.Empty;
        [ObservableProperty] private string _requesterName = string.Empty;
        [ObservableProperty] private string _requesterEmail = string.Empty;
        [ObservableProperty] private string _submittedAtDisplay = string.Empty;
        [ObservableProperty] private string _updatedAtDisplay = string.Empty;

        // Decision section
        [ObservableProperty] private bool _hasDecision;
        [ObservableProperty] private string _approverName = string.Empty;
        [ObservableProperty] private string _approvalComments = string.Empty;
        [ObservableProperty] private string _decidedAtDisplay = string.Empty;

        // Provisioning section
        [ObservableProperty] private bool _hasProvisioning;
        [ObservableProperty] private string _provisioningStatus = string.Empty;
        [ObservableProperty] private string _provisioningMessage = string.Empty;
        [ObservableProperty] private string _provisionedAtDisplay = string.Empty;

        // Cancel button visibility
        [ObservableProperty] private bool _canCancel;

        // ---- Lifecycle ----

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
            UpdatedAtDisplay = r.UpdatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

            HasDecision = r.DecidedAt.HasValue;
            ApproverName = r.ApproverDisplayName ?? string.Empty;
            ApprovalComments = r.ApprovalComments ?? string.Empty;
            DecidedAtDisplay = r.DecidedAt.HasValue
                ? r.DecidedAt.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm")
                : string.Empty;

            HasProvisioning = !string.IsNullOrWhiteSpace(r.ProvisioningStatus);
            ProvisioningStatus = r.ProvisioningStatus ?? string.Empty;
            ProvisioningMessage = r.ProvisioningMessage ?? string.Empty;
            ProvisionedAtDisplay = r.ProvisionedAt.HasValue
                ? r.ProvisionedAt.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm")
                : string.Empty;

            var currentUid = _currentUser.Current?.Uid;
            CanCancel = currentUid is not null
                        && currentUid == r.RequesterUid
                        && (r.Status == RequestStatus.Submitted
                            || r.Status == RequestStatus.PendingApproval);
        }

        // ---- Commands ----

        [RelayCommand]
        private async Task CancelRequestAsync() {
            if (Request is null || Shell.Current is null) return;

            var confirmed = await Shell.Current.DisplayAlert(
                "Cancel request",
                $"Cancel your request for {Request.BusinessSystemName}? This cannot be undone.",
                "Yes, cancel",
                "Keep request");
            if (!confirmed) return;

            IsBusy = true;
            ErrorMessage = null;
            try {
                await _requests.CancelAsync(Request.Id);
                await LoadAsync(Request.Id);
                await Shell.Current.DisplayAlert("Cancelled", "Your request has been cancelled.", "OK");
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't cancel: {ex.Message}";
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
