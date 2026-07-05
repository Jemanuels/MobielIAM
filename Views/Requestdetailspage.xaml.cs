using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class RequestDetailsPage : ContentPage {
    public RequestDetailsPage(RequestDetailsViewModel vm) {
        InitializeComponent();
        BindingContext = vm;
    }
}
