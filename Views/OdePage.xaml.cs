using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class OdePage : ContentPage
{
    public OdePage(OdeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
