using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class PolynomialsPage : ContentPage
{
    public PolynomialsPage(PolynomialsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
