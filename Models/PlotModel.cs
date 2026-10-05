namespace MathesisMauiApp.Models;

/// <summary>How a series is drawn.</summary>
public enum SeriesStyle
{
    Line,
    Points,
    LineAndPoints,
}

public readonly record struct PlotPoint(double X, double Y);

/// <summary>One curve or point set. <paramref name="Color"/> indexes the plot palette.</summary>
public sealed record PlotSeries(string Name, IReadOnlyList<PlotPoint> Points, SeriesStyle Style = SeriesStyle.Line, int Color = 0);

/// <summary>A highlighted point such as a root or a minimum.</summary>
public sealed record PlotMarker(double X, double Y, string? Label = null, int Color = 1);

/// <summary>What a <c>PlotView</c> draws. Ranges that are not given are fitted to the data.</summary>
public sealed class PlotModel
{
    public string? Title { get; init; }

    public string? XLabel { get; init; }

    public string? YLabel { get; init; }

    public IReadOnlyList<PlotSeries> Series { get; init; } = [];

    public IReadOnlyList<PlotMarker> Markers { get; init; } = [];

    public double? XMin { get; init; }

    public double? XMax { get; init; }

    public double? YMin { get; init; }

    public double? YMax { get; init; }

    /// <summary>Samples <paramref name="f"/> at <paramref name="count"/> evenly spaced points; a value that is not finite is kept so the plot breaks the line there.</summary>
    public static PlotPoint[] Sample(Func<double, double> f, double from, double to, int count = 400)
    {
        var points = new PlotPoint[count];
        for (var i = 0; i < count; i++)
        {
            var x = from + (to - from) * i / (count - 1);
            double y;
            try
            {
                y = f(x);
            }
            catch (Exception ex) when (ex is ArithmeticException or InvalidOperationException)
            {
                y = double.NaN;
            }

            points[i] = new PlotPoint(x, y);
        }

        return points;
    }
}
