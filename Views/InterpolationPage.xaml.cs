using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class InterpolationPage : ContentPage
{
    public InterpolationPage(InterpolationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
