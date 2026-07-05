using MauiApp1.ViewModels;
using Microsoft.Maui.Controls;

namespace MauiApp1.Views;

public partial class LoginPage : ContentPage {
    public LoginPage(LoginViewModel vm) {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnRegisterClicked(object sender, System.EventArgs e) {
        // Resolve RegisterPage from DI so its ViewModel + dependencies are wired.
        var services = IPlatformApplication.Current?.Services;
        if (services is null) return;

        var registerPage = services.GetService(typeof(RegisterPage)) as RegisterPage;
        if (registerPage is null) return;

        await Navigation.PushAsync(registerPage);
    }
}
