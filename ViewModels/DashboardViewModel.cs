using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1.ViewModels {
    public partial class DashboardViewModel : ObservableObject {
        private readonly ICurrentUserService _currentUser;
        private readonly IFirebaseAuthService _auth;
        private readonly IAccessRequestService _requests;
        private readonly IBusinessSystemService _systems;

        public DashboardViewModel(
            ICurrentUserService currentUser,
            IFirebaseAuthService auth,
            IAccessRequestService requests,
            IBusinessSystemService systems) {
            _currentUser = currentUser;
            _auth = auth;
            _requests = requests;
            _systems = systems;
            Refresh();
        }

        // ---- Header ----
        [ObservableProperty] private string _greetingText = "Welcome";
        [ObservableProperty] private string _roleText = string.Empty;

        // ---- Role flags ----
        [ObservableProperty] private bool _isRequester;
        [ObservableProperty] private bool _isApprover;
        [ObservableProperty] private bool _isAdmin;

        // ---- Requester counts ----
        [ObservableProperty] private int _submittedCount;
        [ObservableProperty] private int _pendingCount;
        [ObservableProperty] private int _approvedCount;
        [ObservableProperty] private int _rejectedCount;

        // ---- Approver counts ----
        [ObservableProperty] private int _pendingApprovalsCount;
        [ObservableProperty] private int _recentlyApprovedCount;
        [ObservableProperty] private int _recentlyRejectedCount;

        // ---- Admin counts ----
        [ObservableProperty] private int _totalUsersCount;
        [ObservableProperty] private int _totalSystemsCount;
        [ObservableProperty] private int _totalRequestsCount;

        public void Refresh() {
            var user = _currentUser.Current;
            if (user is null) {
                GreetingText = "Welcome";
                RoleText = string.Empty;
                IsRequester = IsApprover = IsAdmin = false;
                return;
            }

            var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
            GreetingText = $"Hello, {name}";
            RoleText = $"Signed in as {user.Role}";

            IsRequester = user.Role == UserRole.Requester;
            IsApprover = user.Role == UserRole.Approver;
            IsAdmin = user.Role == UserRole.Admin;
        }

        [RelayCommand]
        public async Task RefreshCountsAsync() {
            var user = _currentUser.Current;
            if (user is null) return;

            try {
                if (IsRequester || IsAdmin) {
                    var mine = await _requests.GetMineAsync(user.Uid);
                    SubmittedCount = mine.Count(r => r.Status == RequestStatus.Submitted);
                    PendingCount = mine.Count(r => r.Status == RequestStatus.PendingApproval);
                    ApprovedCount = mine.Count(r => r.Status == RequestStatus.Approved);
                    RejectedCount = mine.Count(r => r.Status == RequestStatus.Rejected);
                }

                if (IsApprover || IsAdmin) {
                    var pending = await _requests.GetPendingAsync();
                    PendingApprovalsCount = pending.Count(r => r.RequesterUid != user.Uid);
                }

                if (IsAdmin) {
                    var systems = await _systems.GetAllAsync();
                    TotalSystemsCount = systems.Count;
                }
            }
            catch (Exception ex) {
                if (Shell.Current is not null) {
                    await Shell.Current.DisplayAlert("Couldn't load dashboard", ex.Message, "OK");
                }
            }
        }

        // ---- Navigation ----

        [RelayCommand]
        private async Task GoToNewRequestAsync() {
            if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(NewAccessRequestPage));
        }

        [RelayCommand]
        private async Task GoToMyRequestsAsync() {
            if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(MyRequestsPage));
        }

        [RelayCommand]
        private async Task GoToApprovalQueueAsync() {
            if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(ApprovalQueuePage));
        }

        [RelayCommand]
        private async Task GoToAdminSystemsAsync() {
            if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(AdminSystemsPage));
        }

        [RelayCommand]
        private async Task ComingSoonAsync(string? feature) {
            var name = string.IsNullOrWhiteSpace(feature) ? "This feature" : feature;
            if (Shell.Current is not null) {
                await Shell.Current.DisplayAlert("Coming soon", $"{name} is not implemented yet.", "OK");
            }
        }

        [RelayCommand]
        private async Task LogoutAsync() {
            await _auth.SignOutAsync();

            var services = IPlatformApplication.Current?.Services;
            if (services is null) return;

            var loginPage = services.GetRequiredService<LoginPage>();
            if (Application.Current?.Windows.Count > 0) {
                Application.Current.Windows[0].Page = new NavigationPage(loginPage);
            }
        }
    }
}
