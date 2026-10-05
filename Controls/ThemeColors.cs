namespace MathesisMauiApp.Controls;

/// <summary>Reads the palette of <c>Colors.xaml</c> from code (for drawn plots and highlighted text), following the current light or dark theme.</summary>
internal static class ThemeColors
{
    // Categorical colours for plot series: distinguishable in both themes and without relying on red against green.
    private static readonly string[] SeriesLight = ["#512BD4", "#00796B", "#D9730D", "#C2185B", "#1E6FD9", "#6D6D78"];
    private static readonly string[] SeriesDark = ["#AC99EA", "#5FD3C3", "#FFB066", "#FF8AB5", "#7FB5FF", "#B4B4C0"];

    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>The colour of a resource such as <c>Danger</c>; its <c>Dark</c> twin is used in the dark theme.</summary>
    public static Color Get(string key)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return Colors.Gray;
        if (IsDark && resources.TryGetValue(key + "Dark", out var dark) && dark is Color darkColor) return darkColor;
        return resources.TryGetValue(key, out var light) && light is Color lightColor ? lightColor : Colors.Gray;
    }

    public static Color Series(int index) => Color.FromArgb((IsDark ? SeriesDark : SeriesLight)[Math.Abs(index) % SeriesLight.Length]);

    public static string MonoFont => Application.Current?.Resources.TryGetValue("MonoFont", out var font) == true && font is string name ? name : "monospace";
}
