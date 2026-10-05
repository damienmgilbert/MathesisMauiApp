using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class RootsPage : ContentPage
{
    public RootsPage(RootsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
