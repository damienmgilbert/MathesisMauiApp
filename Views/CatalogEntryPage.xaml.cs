using MathesisMauiApp.ViewModels;

namespace MathesisMauiApp.Views;

public partial class CatalogEntryPage : ContentPage
{
    public CatalogEntryPage(CatalogEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
