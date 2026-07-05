using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class MyRequestsPage : ContentPage {
    private readonly MyRequestsViewModel _vm;

    public MyRequestsPage(MyRequestsViewModel vm) {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
