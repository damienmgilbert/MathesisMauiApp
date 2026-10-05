using Microsoft.Maui.Handlers;

namespace MathesisMauiApp;

/// <summary>
/// Entries and editors draw their own underline or border on most platforms. The pages put them inside a bordered "Field" that already shows
/// the validation state, so the platform chrome is removed.
/// </summary>
internal static class HandlerCustomizations
{
	public static void Apply()
	{
		EntryHandler.Mapper.AppendToMapping("Flat", static (handler, _) =>
		{
#if ANDROID
			handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
			Flatten(handler.PlatformView);
#elif IOS || MACCATALYST
			handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#endif
		});

		EditorHandler.Mapper.AppendToMapping("Flat", static (handler, _) =>
		{
#if ANDROID
			handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
			Flatten(handler.PlatformView);
#endif
		});
	}

#if WINDOWS
	private static void Flatten(Microsoft.UI.Xaml.Controls.TextBox box)
	{
		var none = new Microsoft.UI.Xaml.Thickness(0);
		var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
		box.BorderThickness = none;
		foreach (var key in new[]
		{
			"TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
			"TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushDisabled",
		})
		{
			box.Resources[key] = transparent;
		}

		box.Resources["TextControlBorderThemeThickness"] = none;
		box.Resources["TextControlBorderThemeThicknessFocused"] = none;
	}
#endif
}
