using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class PlaygroundPage : ContentPage
{
    public PlaygroundPage(PlaygroundViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
