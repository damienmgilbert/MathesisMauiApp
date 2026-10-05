using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class FormDemoPage : ContentPage
{
    public FormDemoPage(FormDemoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
