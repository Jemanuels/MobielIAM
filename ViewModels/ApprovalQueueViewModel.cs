using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Views;

namespace MauiApp1.ViewModels {
    public partial class ApprovalQueueViewModel : ObservableObject {
        private readonly IAccessRequestService _requests;
        private readonly ICurrentUserService _currentUser;

        public ApprovalQueueViewModel(
            IAccessRequestService requests,
            ICurrentUserService currentUser) {
            _requests = requests;
            _currentUser = currentUser;
        }

        public ObservableCollection<AccessRequest> Items { get; } = new();

        [ObservableProperty] private bool _isRefreshing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? _errorMessage;
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        [RelayCommand]
        public async Task LoadAsync() {
            var me = _currentUser.Current;
            if (me is null) {
                IsRefreshing = false;
                return;
            }

            IsRefreshing = true;
            ErrorMessage = null;
            try {
                var data = await _requests.GetPendingAsync();

                // Separation of duties: an approver should not see their own
                // requests in the queue, even though Firestore rules technically
                // allow them to act on any pending request.
                var filtered = data.Where(r => r.RequesterUid != me.Uid).ToList();

                Items.Clear();
                foreach (var r in filtered) Items.Add(r);
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't load queue: {ex.Message}";
            }
            finally {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task OpenAsync(AccessRequest? request) {
            if (request is null || Shell.Current is null) return;

            await Shell.Current.GoToAsync(
                $"{nameof(ApprovalDetailsPage)}?requestId={request.Id}");
        }
    }
}
