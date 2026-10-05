using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class EvaluatePage : ContentPage
{
    public EvaluatePage(EvaluateViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
