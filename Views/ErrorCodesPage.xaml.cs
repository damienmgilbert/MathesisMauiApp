using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class ErrorCodesPage : ContentPage
{
    public ErrorCodesPage(ErrorCodesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
