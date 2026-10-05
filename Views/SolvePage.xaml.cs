using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class SolvePage : ContentPage
{
    public SolvePage(SolveViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
