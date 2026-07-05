using MauiApp1.ViewModels;
using Microsoft.Maui.Controls;

namespace MauiApp1.Views;

public partial class RegisterPage : ContentPage {
    public RegisterPage(RegisterViewModel vm) {
        InitializeComponent();
        BindingContext = vm;
    }
}
