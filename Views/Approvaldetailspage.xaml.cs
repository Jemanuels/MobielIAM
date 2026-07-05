using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class ApprovalDetailsPage : ContentPage {
    public ApprovalDetailsPage(ApprovalDetailsViewModel vm) {
        InitializeComponent();
        BindingContext = vm;
    }
}
