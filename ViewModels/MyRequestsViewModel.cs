using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Views;

namespace MauiApp1.ViewModels {
    public partial class MyRequestsViewModel : ObservableObject {
        private readonly IAccessRequestService _requests;
        private readonly ICurrentUserService _currentUser;

        public MyRequestsViewModel(
            IAccessRequestService requests,
            ICurrentUserService currentUser) {
            _requests = requests;
            _currentUser = currentUser;
        }

        public ObservableCollection<AccessRequest> Items { get; } = new();

        [ObservableProperty] private bool _isRefreshing;
        [ObservableProperty] private bool _hasItems;

        // Bound from the XAML error banner. NotifyPropertyChangedFor keeps
        // HasError in sync without us writing a custom setter.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? _errorMessage;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        [RelayCommand]
        public async Task LoadAsync() {
            var user = _currentUser.Current;
            if (user is null) {
                IsRefreshing = false;
                return;
            }

            IsRefreshing = true;
            ErrorMessage = null;
            try {
                var data = await _requests.GetMineAsync(user.Uid);
                Items.Clear();
                foreach (var r in data) Items.Add(r);
                HasItems = Items.Count > 0;
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't load requests: {ex.Message}";
            }
            finally {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task OpenAsync(AccessRequest? request) {
            if (request is null || Shell.Current is null) return;

            await Shell.Current.GoToAsync(
                $"{nameof(RequestDetailsPage)}?requestId={request.Id}");
        }

        [RelayCommand]
        private async Task GoToNewRequestAsync() {
            if (Shell.Current is not null) {
                await Shell.Current.GoToAsync(nameof(NewAccessRequestPage));
            }
        }
    }
}
