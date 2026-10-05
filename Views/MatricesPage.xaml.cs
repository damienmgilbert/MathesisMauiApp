using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class MatricesPage : ContentPage
{
    public MatricesPage(MatricesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
