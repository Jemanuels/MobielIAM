using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class ApprovalQueuePage : ContentPage {
    private readonly ApprovalQueueViewModel _vm;

    public ApprovalQueuePage(ApprovalQueueViewModel vm) {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
