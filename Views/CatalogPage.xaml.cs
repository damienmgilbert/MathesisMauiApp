using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class CatalogPage : ContentPage
{
    public CatalogPage(CatalogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CatalogRow row && BindingContext is CatalogViewModel viewModel)
        {
            viewModel.OpenCommand.Execute(row);
        }

        // The list is a menu, not a selection: clear it so the same row can be opened again.
        if (sender is CollectionView view) view.SelectedItem = null;
    }
}
