using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class AlgebraPage : ContentPage
{
    public AlgebraPage(AlgebraViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
