using MathesisMauiApp.Models;
using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is HomeViewModel viewModel && (sender as BindableObject)?.BindingContext is NavItem item)
        {
            viewModel.OpenCommand.Execute(item);
        }
    }
}
