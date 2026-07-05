using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class EditBusinessSystemPage : ContentPage {
    public EditBusinessSystemPage(EditBusinessSystemViewModel vm) {
        InitializeComponent();
        BindingContext = vm;
    }
}
