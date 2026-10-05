using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

internal sealed class PlotDrawable : IDrawable
{
    private const float Left = 54, Right = 16, Top = 30, Bottom = 34;

    public PlotModel? Model { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var text = ThemeColors.Get("TextSecondary");
        var grid = ThemeColors.Get("Outline");
        var strong = ThemeColors.Get("TextSecondary");
        var primary = ThemeColors.Get("TextPrimary");

        if (Model is not { } model)
        {
            return;
        }

        var plot = new RectF(Left, Top, Math.Max(10, dirtyRect.Width - Left - Right), Math.Max(10, dirtyRect.Height - Top - Bottom));
        var (xMin, xMax, yMin, yMax) = Bounds(model);

        float X(double x) => plot.Left + (float)((x - xMin) / (xMax - xMin) * plot.Width);
        float Y(double y) => plot.Bottom - (float)((y - yMin) / (yMax - yMin) * plot.Height);

        canvas.FontSize = 10;
        canvas.FontColor = text;

        // Grid and tick labels.
        canvas.StrokeSize = 1;
        foreach (var tick in Ticks(xMin, xMax))
        {
            var x = X(tick);
            canvas.StrokeColor = grid;
            canvas.DrawLine(x, plot.Top, x, plot.Bottom);
            canvas.DrawString(Format(tick), x - 30, plot.Bottom + 4, 60, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
        }

        foreach (var tick in Ticks(yMin, yMax))
        {
            var y = Y(tick);
            canvas.StrokeColor = grid;
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            canvas.DrawString(Format(tick), 0, y - 7, Left - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        // Axes through zero when zero is in view.
        canvas.StrokeColor = strong;
        canvas.StrokeSize = 1.5f;
        if (xMin <= 0 && xMax >= 0) canvas.DrawLine(X(0), plot.Top, X(0), plot.Bottom);
        if (yMin <= 0 && yMax >= 0) canvas.DrawLine(plot.Left, Y(0), plot.Right, Y(0));
        canvas.StrokeSize = 1;
        canvas.DrawRectangle(plot);

        // Curves and points, clipped to the plot area.
        canvas.SaveState();
        canvas.ClipRectangle(plot);
        foreach (var series in model.Series)
        {
            var color = ThemeColors.Series(series.Color);
            canvas.StrokeColor = color;
            canvas.FillColor = color;
            canvas.StrokeSize = 2;
            canvas.StrokeLineJoin = LineJoin.Round;

            if (series.Style is SeriesStyle.Line or SeriesStyle.LineAndPoints)
            {
                PathF? path = null;
                var span = yMax - yMin;
                PlotPoint? previous = null;
                foreach (var point in series.Points)
                {
                    var finite = double.IsFinite(point.X) && double.IsFinite(point.Y);
                    // A jump of more than several screens between neighbours is a pole, not a line.
                    var jump = previous is { } p && finite && Math.Abs(point.Y - p.Y) > 4 * span;
                    if (!finite || jump)
                    {
                        if (path is not null) canvas.DrawPath(path);
                        path = null;
                        previous = finite ? point : null;
                        if (finite) { path = new PathF(); path.MoveTo(X(point.X), Y(point.Y)); }
                        continue;
                    }

                    if (path is null) { path = new PathF(); path.MoveTo(X(point.X), Y(point.Y)); }
                    else path.LineTo(X(point.X), Y(point.Y));
                    previous = point;
                }

                if (path is not null) canvas.DrawPath(path);
            }

            if (series.Style is SeriesStyle.Points or SeriesStyle.LineAndPoints)
            {
                foreach (var point in series.Points)
                {
                    if (double.IsFinite(point.X) && double.IsFinite(point.Y)) canvas.FillCircle(X(point.X), Y(point.Y), 3.5f);
                }
            }
        }

        canvas.RestoreState();

        // Markers sit on top: a ring with a label.
        foreach (var marker in model.Markers)
        {
            if (!double.IsFinite(marker.X) || !double.IsFinite(marker.Y) || marker.X < xMin || marker.X > xMax || marker.Y < yMin || marker.Y > yMax) continue;
            var color = ThemeColors.Series(marker.Color);
            var x = X(marker.X);
            var y = Y(marker.Y);
            canvas.FillColor = color;
            canvas.FillCircle(x, y, 5);
            canvas.StrokeColor = ThemeColors.Get("Surface");
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(x, y, 5);
            if (!string.IsNullOrEmpty(marker.Label))
            {
                canvas.FontColor = primary;
                canvas.FontSize = 11;
                var flip = x > plot.Right - 110;
                canvas.DrawString(marker.Label, flip ? x - 118 : x + 8, y - 20, 110, 16, flip ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Center);
            }
        }

        // Title, axis labels and legend.
        canvas.FontColor = primary;
        canvas.FontSize = 12;
        if (!string.IsNullOrEmpty(model.Title)) canvas.DrawString(model.Title, plot.Left, 4, plot.Width, 18, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.FontSize = 10;
        canvas.FontColor = text;
        if (!string.IsNullOrEmpty(model.XLabel)) canvas.DrawString(model.XLabel, plot.Left, dirtyRect.Height - 14, plot.Width, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        if (!string.IsNullOrEmpty(model.YLabel)) canvas.DrawString(model.YLabel, 0, 4, Left + 40, 18, HorizontalAlignment.Left, VerticalAlignment.Center);

        var legendX = plot.Right;
        foreach (var series in model.Series.Where(s => !string.IsNullOrEmpty(s.Name)).Reverse())
        {
            var width = series.Name.Length * 6.2f + 24;
            legendX -= width;
            canvas.StrokeColor = ThemeColors.Series(series.Color);
            canvas.StrokeSize = 3;
            canvas.DrawLine(legendX, 13, legendX + 14, 13);
            canvas.FontColor = primary;
            canvas.DrawString(series.Name, legendX + 18, 5, width, 16, HorizontalAlignment.Left, VerticalAlignment.Center);
        }
    }

    /// <summary>The ranges to draw: those the model asks for, otherwise the data with a margin; extreme values (poles) do not stretch the view.</summary>
    private static (double XMin, double XMax, double YMin, double YMax) Bounds(PlotModel model)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var series in model.Series)
        {
            foreach (var p in series.Points)
            {
                if (double.IsFinite(p.X)) xs.Add(p.X);
                if (double.IsFinite(p.Y)) ys.Add(p.Y);
            }
        }

        foreach (var m in model.Markers)
        {
            if (double.IsFinite(m.X)) xs.Add(m.X);
            if (double.IsFinite(m.Y)) ys.Add(m.Y);
        }

        if (xs.Count == 0) xs.AddRange([-1, 1]);
        if (ys.Count == 0) ys.AddRange([-1, 1]);
        xs.Sort();
        ys.Sort();

        double x0 = xs[0], x1 = xs[^1];
        double y0 = ys[0], y1 = ys[^1];

        // A pole makes the full range useless; when the middle 98 % of the values fit in a much smaller band, show that band.
        var low = Quantile(ys, 0.01);
        var high = Quantile(ys, 0.99);
        if (ys.Count > 20 && (y1 - y0) > 20 * Math.Max(high - low, 1e-12))
        {
            y0 = low;
            y1 = high;
        }

        Pad(ref x0, ref x1, 0.0);
        Pad(ref y0, ref y1, 0.08);
        return (model.XMin ?? x0, model.XMax ?? x1, model.YMin ?? y0, model.YMax ?? y1);
    }

    private static void Pad(ref double min, ref double max, double fraction)
    {
        if (max - min < 1e-12)
        {
            min -= 1;
            max += 1;
            return;
        }

        var pad = (max - min) * fraction;
        min -= pad;
        max += pad;
    }

    private static double Quantile(List<double> sorted, double q) => sorted[Math.Clamp((int)(q * (sorted.Count - 1)), 0, sorted.Count - 1)];

    /// <summary>Round tick positions (1, 2 or 5 times a power of ten) covering the range with about six steps.</summary>
    private static IEnumerable<double> Ticks(double min, double max)
    {
        var range = max - min;
        if (range <= 0 || !double.IsFinite(range)) yield break;
        var rough = range / 6;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;
        var step = (residual < 1.5 ? 1 : residual < 3.5 ? 2 : residual < 7.5 ? 5 : 10) * magnitude;
        var first = Math.Ceiling(min / step) * step;
        for (var tick = first; tick <= max + step * 1e-9; tick += step) yield return Math.Abs(tick) < step * 1e-9 ? 0 : tick;
    }

    private static string Format(double value)
    {
        var abs = Math.Abs(value);
        if (abs != 0 && (abs >= 1e5 || abs < 1e-3)) return value.ToString("0.##e+0", System.Globalization.CultureInfo.InvariantCulture);
        return Math.Round(value, 6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }
}
