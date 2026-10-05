using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class CalculusPage : ContentPage
{
    public CalculusPage(CalculusViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
