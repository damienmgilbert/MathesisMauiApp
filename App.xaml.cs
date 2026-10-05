using MathesisMauiApp.Services;

namespace MathesisMauiApp;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services, ISettingsService settings)
	{
		InitializeComponent();
		_services = services;

		// The user's theme choice (or the system theme when none) is applied here, so the settings service stays free of UI types.
		UserAppTheme = settings.Theme;
#if DEBUG
		// Lets a script look at the other theme: MATHESIS_THEME=light.
		if (Environment.GetEnvironmentVariable("MATHESIS_THEME") is { Length: > 0 } theme && Enum.TryParse<AppTheme>(theme, ignoreCase: true, out var forced)) UserAppTheme = forced;
#endif
		settings.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(ISettingsService.Theme)) UserAppTheme = settings.Theme;
		};
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// The shell is resolved here, not in the constructor: its menu uses the resources that InitializeComponent has just loaded.
		return new Window(_services.GetRequiredService<AppShell>())
		{
			Title = "Mathesis Showcase",
			Width = 1200,
			Height = 860,
		};
	}
}
