using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Views;

namespace MauiApp1.ViewModels {
    public partial class AdminSystemsViewModel : ObservableObject {
        private readonly IBusinessSystemService _systems;

        public AdminSystemsViewModel(IBusinessSystemService systems) {
            _systems = systems;
        }

        public ObservableCollection<BusinessSystem> Items { get; } = new();

        [ObservableProperty] private bool _isRefreshing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? _errorMessage;
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
        private bool _hasItems;
        public bool ShowEmptyState => !HasItems && !IsRefreshing;

        [RelayCommand]
        public async Task LoadAsync() {
            IsRefreshing = true;
            ErrorMessage = null;
            try {
                var data = await _systems.GetAllAsync();
                Items.Clear();
                foreach (var s in data) Items.Add(s);
                HasItems = Items.Count > 0;
                OnPropertyChanged(nameof(ShowEmptyState));
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't load systems: {ex.Message}";
            }
            finally {
                IsRefreshing = false;
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }

        [RelayCommand]
        private async Task AddAsync() {
            if (Shell.Current is not null) {
                await Shell.Current.GoToAsync(nameof(EditBusinessSystemPage));
            }
        }

        [RelayCommand]
        private async Task EditAsync(BusinessSystem? system) {
            if (system is null || Shell.Current is null) return;
            await Shell.Current.GoToAsync($"{nameof(EditBusinessSystemPage)}?systemId={system.Id}");
        }

        [RelayCommand]
        private async Task SeedExamplesAsync() {
            if (Shell.Current is null) return;

            var confirmed = await Shell.Current.DisplayAlert(
                "Seed example systems",
                "Add Finance System, Sales CRM and SharePoint Sales Site with default roles?",
                "Add", "Cancel");
            if (!confirmed) return;

            IsRefreshing = true;
            try {
                await _systems.SeedExamplesAsync();
                await LoadAsync();
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't seed: {ex.Message}";
            }
            finally {
                IsRefreshing = false;
            }
        }
    }
}
