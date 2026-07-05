using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Services;

namespace MauiApp1.ViewModels {
    public partial class RegisterViewModel : ObservableObject {
        private readonly IFirebaseAuthService _authService;

        public RegisterViewModel(IFirebaseAuthService authService) {
            _authService = authService;
        }

        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _errorMessage;

        [RelayCommand]
        private async Task RegisterAsync() {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            try {
                var result = await _authService.RegisterAsync(Email, Password, DisplayName);
                if (result.Success) {
                    if (Application.Current?.Windows.Count > 0) {
                        Application.Current.Windows[0].Page = new AppShell();
                    }
                }
                else {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (System.Exception ex) {
                ErrorMessage = $"Registration failed: {ex.Message}";
            }
            finally {
                IsBusy = false;
            }
        }
    }
}
