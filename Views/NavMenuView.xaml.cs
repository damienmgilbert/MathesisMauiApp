using MathesisMauiApp.Models;
using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

/// <summary>The content of the Shell flyout: the menu of pages.</summary>
public partial class NavMenuView : ContentView
{
    public NavMenuView(NavMenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is NavMenuViewModel viewModel && (sender as BindableObject)?.BindingContext is NavItem item)
        {
            viewModel.OpenCommand.Execute(item);
        }
    }
}
