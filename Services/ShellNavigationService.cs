namespace MathesisMauiApp.Services;

public sealed class ShellNavigationService : INavigationService
{
    public async Task GoToAsync(string route)
    {
        await Shell.Current.GoToAsync(route);
        Shell.Current.FlyoutIsPresented = false;
    }

    // The ID is a query value, so it is escaped here and unescaped by the receiving ViewModel.
    public Task OpenCatalogEntryAsync(string entryId) =>
        Shell.Current.GoToAsync($"{Routes.CatalogEntry}?{Routes.EntryIdKey}={Uri.EscapeDataString(entryId)}");

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
