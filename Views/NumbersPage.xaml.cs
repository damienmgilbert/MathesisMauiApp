using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class NumbersPage : ContentPage
{
    public NumbersPage(NumbersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
