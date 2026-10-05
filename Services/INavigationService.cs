namespace MathesisMauiApp.Services;

/// <summary>Navigation as ViewModels see it, so they can be tested without a Shell.</summary>
public interface INavigationService
{
    /// <summary>Goes to a registered route or a Shell content route (<c>//home</c>).</summary>
    Task GoToAsync(string route);

    /// <summary>Opens the catalog entry with the given ID.</summary>
    Task OpenCatalogEntryAsync(string entryId);

    /// <summary>Goes back one page.</summary>
    Task GoBackAsync();
}
