using CommunityToolkit.Maui;
using MathesisMauiApp.Services;
using MathesisMauiApp.ViewModels;
using MathesisMauiApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MathesisMauiApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		HandlerCustomizations.Apply();

		var services = builder.Services;

		// Platform APIs are registered behind their interfaces so that ViewModels never touch the statics.
		services.AddSingleton(Preferences.Default);
		services.AddSingleton(Clipboard.Default);
		services.AddSingleton(DeviceInfo.Current);

		services.AddSingleton<ISettingsService, SettingsService>();
		services.AddSingleton<IFeedbackService, FeedbackService>();
		services.AddSingleton<INavigationService, ShellNavigationService>();
		services.AddSingleton<IOutcomePresenter, OutcomePresenter>();

		services.AddSingleton<NavMenuViewModel>();
		services.AddSingleton<NavMenuView>();
		services.AddSingleton<AppShell>();

		services.AddPage<HomePage, HomeViewModel>();
		services.AddPage<NumbersPage, NumbersViewModel>();
		services.AddPage<ExpressionsPage, ExpressionsViewModel>();
		services.AddPage<PolynomialsPage, PolynomialsViewModel>();
		services.AddPage<MatricesPage, MatricesViewModel>();
		services.AddPage<FormDemoPage, FormDemoViewModel>();
		services.AddPage<PlaygroundPage, PlaygroundViewModel>();
		services.AddPage<ErrorCodesPage, ErrorCodesViewModel>();

		services.AddPage<AlgebraPage, AlgebraViewModel>();
		services.AddPage<SolvePage, SolveViewModel>();
		services.AddPage<CalculusPage, CalculusViewModel>();
		services.AddPage<EvaluatePage, EvaluateViewModel>();
		services.AddPage<RootsPage, RootsViewModel>();
		services.AddPage<QuadraturePage, QuadratureViewModel>();
		services.AddPage<OdePage, OdeViewModel>();
		services.AddPage<InterpolationPage, InterpolationViewModel>();
		services.AddPage<CatalogPage, CatalogViewModel>();
		services.AddPage<SettingsPage, SettingsViewModel>();
		services.AddPage<CatalogEntryPage, CatalogEntryViewModel>();
#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	/// <summary>Pages and their ViewModels are transient: a page asks for its ViewModel in its constructor.</summary>
	private static IServiceCollection AddPage<TPage, TViewModel>(this IServiceCollection services)
		where TPage : Page
		where TViewModel : class
	{
		services.AddTransient<TViewModel>();
		return services.AddTransient<TPage>();
	}
}
