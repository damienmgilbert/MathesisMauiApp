using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class ExpressionsPage : ContentPage
{
    public ExpressionsPage(ExpressionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
