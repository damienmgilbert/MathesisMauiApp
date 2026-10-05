using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class QuadraturePage : ContentPage
{
    public QuadraturePage(QuadratureViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
