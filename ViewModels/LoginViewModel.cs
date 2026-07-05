using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Services;

namespace MauiApp1.ViewModels {
    public partial class LoginViewModel : ObservableObject {
        private readonly IFirebaseAuthService _authService;

        public LoginViewModel(IFirebaseAuthService authService) {
            _authService = authService;
        }

        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _errorMessage;

        [RelayCommand]
        private async Task LoginAsync() {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            try {
                var result = await _authService.LoginAsync(Email, Password);
                if (result.Success) {
                    // Swap the root page to the main Shell so the user is "inside" the app.
                    // Later, replace `new AppShell()` with a route to your DashboardPage.
                    if (Application.Current?.Windows.Count > 0) {
                        Application.Current.Windows[0].Page = new AppShell();
                    }
                }
                else {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (System.Exception ex) {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally {
                IsBusy = false;
            }
        }
    }
}
