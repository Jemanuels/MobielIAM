using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class DashboardPage : ContentPage {
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel vm) {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        _vm.Refresh();
        await _vm.RefreshCountsAsync();
    }
}
