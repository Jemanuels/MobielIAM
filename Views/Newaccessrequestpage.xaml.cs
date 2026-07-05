using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class NewAccessRequestPage : ContentPage {
    private readonly NewAccessRequestViewModel _vm;

    public NewAccessRequestPage(NewAccessRequestViewModel vm) {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        await _vm.LoadSystemsAsync();
    }
}
