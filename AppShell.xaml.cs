using MathesisMauiApp.Services;
using MathesisMauiApp.ViewModels;
using MathesisMauiApp.Views;

namespace MathesisMauiApp;

public partial class AppShell : Shell
{
	// Below this width the menu slides in instead of taking a fixed column.
	private const double LockedMenuWidth = 900;

	private readonly NavMenuViewModel _menu;
	private readonly IDeviceInfo _deviceInfo;

	public AppShell(NavMenuViewModel menu, NavMenuView menuView, IDeviceInfo deviceInfo)
	{
		InitializeComponent();
		_menu = menu;
		_deviceInfo = deviceInfo;

		// The one route that is pushed on top of a page rather than shown from the menu.
		Routing.RegisterRoute(Routes.CatalogEntry, typeof(CatalogEntryPage));

		FlyoutContent = menuView;
		FlyoutBehavior = FlyoutBehavior.Flyout;

		// The shell itself reports no size changes; the window does.
		Loaded += (_, _) =>
		{
			if (Window is { } window) window.SizeChanged += (_, _) => UpdateFlyoutBehavior(window.Width);
			UpdateFlyoutBehavior(Window?.Width ?? Width);
		};

		Navigated += (_, e) => _menu.Select(e.Current?.Location.OriginalString ?? string.Empty);

#if DEBUG
		// Lets a script open a page directly: MATHESIS_ROUTE=numbers.
		if (Environment.GetEnvironmentVariable("MATHESIS_ROUTE") is { Length: > 0 } route)
		{
			Loaded += async (_, _) => await GoToAsync($"//{route}");
		}
#endif
	}

	// The menu is a persistent column in a wide desktop window and slides in everywhere else.
	private void UpdateFlyoutBehavior(double width) =>
		FlyoutBehavior = _deviceInfo.Idiom != DeviceIdiom.Phone && width >= LockedMenuWidth ? FlyoutBehavior.Locked : FlyoutBehavior.Flyout;
}
