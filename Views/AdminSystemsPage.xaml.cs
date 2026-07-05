using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class AdminSystemsPage : ContentPage {
    private readonly AdminSystemsViewModel _vm;

    public AdminSystemsPage(AdminSystemsViewModel vm) {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
