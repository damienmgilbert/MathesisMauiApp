using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>The flyout menu: the groups of pages, with the current page marked.</summary>
public sealed partial class NavMenuViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public NavMenuViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        Groups = [new NavGroup(string.Empty, string.Empty, [new NavItem("Home", "⌂", Routes.Home, "What this app shows.")]), .. NavigationCatalog.Create()];
        Select(Routes.Home);
    }

    public IReadOnlyList<NavGroup> Groups { get; }

    /// <summary>Marks the item whose route is <paramref name="location"/> (<c>//numbers</c>, or <c>//numbers/...</c>).</summary>
    public void Select(string location)
    {
        var route = location.TrimStart('/').Split('/')[0];
        foreach (var item in Groups.SelectMany(g => g.Items)) item.IsSelected = item.Route == route;
    }

    [RelayCommand]
    private Task Open(NavItem item) => _navigation.GoToAsync($"//{item.Route}");
}
