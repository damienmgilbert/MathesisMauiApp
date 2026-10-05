using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Numerics.Interpolation;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum InterpolationMethod
{
    Newton,
    Barycentric,
    NaturalSpline,
    ClampedSpline,
    Chebyshev,
}

/// <summary>
/// Interpolation through data points, typed as a two-column matrix and checked by <c>MathMatrix(Columns = 2)</c>, or through a function sampled at
/// Chebyshev nodes. The Chebyshev method is drawn next to the same-degree polynomial on equally spaced nodes, which shows Runge's phenomenon.
/// </summary>
public sealed partial class InterpolationViewModel : PageViewModel
{
    private static readonly Symbol X = new("x");

    private readonly Debouncer _debounce = new();

    public InterpolationViewModel()
    {
        Points = "[[0, 1], [1, 2.7], [2, 5.8], [3, 6.6], [4, 7.5]]";
        Function = "1/(1 + 25*x^2)";
        From = "-1";
        To = "1";
        Degree = "12";
        StartSlope = "0";
        EndSlope = "0";
        Query = "0.5";
        Method = InterpolationMethod.NaturalSpline;
    }

    public IReadOnlyList<InterpolationMethod> Methods { get; } = Enum.GetValues<InterpolationMethod>();

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("smooth data", "[[0, 1], [1, 2.7], [2, 5.8], [3, 6.6], [4, 7.5]]"),
        new("parabola", "[[0, 0], [1, 1], [2, 4], [3, 9], [4, 16]]"),
        new("wave", "[[0, 0], [1, 0.84], [2, 0.91], [3, 0.14], [4, -0.76], [5, -0.96], [6, -0.28]]"),
        new("three columns", "[[0, 1, 2], [1, 2, 3]]"),
        new("not numbers", "[[0, a], [1, 2]]"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Data points")]
    [Required]
    [MathMatrix(Columns = 2, MaxDimension = 40)]
    public partial string? Points { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "f(x)")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"])]
    public partial string? Function { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "From")]
    [Required]
    [ExactRange("-1000", "1000")]
    public partial string? From { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "To")]
    [Required]
    [ExactRange("-1000", "1000")]
    public partial string? To { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Degree")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "60")]
    public partial string? Degree { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Slope at the start")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? StartSlope { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Slope at the end")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? EndSlope { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Evaluate at x")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? Query { get; set; }

    [ObservableProperty]
    public partial InterpolationMethod Method { get; set; }

    [ObservableProperty]
    public partial bool UsesFunction { get; set; }

    [ObservableProperty]
    public partial bool UsesPoints { get; set; }

    [ObservableProperty]
    public partial bool UsesSlopes { get; set; }

    [ObservableProperty]
    public partial string? MethodNote { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Facts { get; set; }

    [ObservableProperty]
    public partial PlotModel? Plot { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Points = text;

    partial void OnMethodChanged(InterpolationMethod value)
    {
        UsesFunction = value == InterpolationMethod.Chebyshev;
        UsesPoints = value != InterpolationMethod.Chebyshev;
        UsesSlopes = value == InterpolationMethod.ClampedSpline;
        MethodNote = value switch
        {
            InterpolationMethod.Newton => "The Newton form of the interpolating polynomial through every point: divided differences, with an exact derivative.",
            InterpolationMethod.Barycentric => "The barycentric form of the same polynomial: numerically stable, evaluated in O(n) per point.",
            InterpolationMethod.NaturalSpline => "A piecewise cubic with continuous second derivative that is straight at both ends. Needs strictly increasing x.",
            InterpolationMethod.ClampedSpline => "A cubic spline with the slopes at both ends prescribed. Needs strictly increasing x.",
            _ => "A polynomial through a function sampled at Chebyshev nodes, which avoids the oscillation of equally spaced nodes.",
        };
        _ = Refresh();
    }

    partial void OnPointsChanged(string? value) => _ = Refresh();

    partial void OnFunctionChanged(string? value) => _ = Refresh();

    partial void OnFromChanged(string? value) => _ = Refresh();

    partial void OnToChanged(string? value) => _ = Refresh();

    partial void OnDegreeChanged(string? value) => _ = Refresh();

    partial void OnStartSlopeChanged(string? value) => _ = Refresh();

    partial void OnEndSlopeChanged(string? value) => _ = Refresh();

    partial void OnQueryChanged(string? value) => _ = Refresh();

    private bool Valid(params string[] properties) => properties.All(p => !GetErrors(p).Any());

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(250), async token =>
    {
        var method = Method;
        var ready = method == InterpolationMethod.Chebyshev
            ? Valid(nameof(Function), nameof(From), nameof(To), nameof(Degree), nameof(Query)) && !string.IsNullOrWhiteSpace(Function)
            : Valid(nameof(Points), nameof(StartSlope), nameof(EndSlope), nameof(Query)) && !string.IsNullOrWhiteSpace(Points);
        if (!ready)
        {
            Facts = null;
            Plot = null;
            return;
        }

        var input = (Points ?? string.Empty, Function ?? string.Empty, From ?? "-1", To ?? "1", Degree ?? "10", StartSlope ?? "0", EndSlope ?? "0", Query ?? "0");
        var work = await RunAsync(() => Compute(method, input));
        if (token.IsCancellationRequested) return;
        Facts = work.Facts;
        Plot = work.Plot;
    });

    private sealed record Work(IReadOnlyList<Fact> Facts, PlotModel? Plot);

    private static Work Compute(InterpolationMethod method, (string Points, string Function, string From, string To, string Degree, string StartSlope, string EndSlope, string Query) input)
    {
        static string G(double value) => value.ToString("G10", CultureInfo.InvariantCulture);
        var query = NumberInputs.ReadDouble(input.Query) ?? 0;
        var facts = new List<Fact>();
        var series = new List<PlotSeries>();
        var markers = new List<PlotMarker>();
        IInterpolant<double>? interpolant = null;
        double lo, hi;

        if (method == InterpolationMethod.Chebyshev)
        {
            var f = ExprFunctions.Of(Expr.Parse(input.Function), X);
            var a = NumberInputs.ReadDouble(input.From) ?? -1;
            var b = NumberInputs.ReadDouble(input.To) ?? 1;
            var degree = int.Parse(input.Degree.Trim(), CultureInfo.InvariantCulture);
            if (f is null || a >= b) return new Work([new("Chebyshev", a >= b ? "From must be below To" : "the function cannot be compiled")], null);

            var chebyshev = Interpolate.Chebyshev(f, a, b, degree);
            interpolant = chebyshev;
            lo = a;
            hi = b;
            var nodes = Enumerable.Range(0, degree + 1).Select(i => a + (b - a) * i / degree).ToArray();
            var equal = Interpolate.Newton(nodes, nodes.Select(f).ToArray());
            series.Add(new PlotSeries("f", PlotModel.Sample(f, a, b), SeriesStyle.Line, 5));
            series.Add(new PlotSeries($"Chebyshev, degree {degree}", PlotModel.Sample(chebyshev.Evaluate, a, b), SeriesStyle.Line, 0));
            series.Add(new PlotSeries($"equally spaced, degree {degree}", PlotModel.Sample(equal.Evaluate, a, b), SeriesStyle.Line, 3));

            var samples = PlotModel.Sample(x => Math.Abs(f(x) - chebyshev.Evaluate(x)), a, b, 800).Max(p => p.Y);
            var samplesEqual = PlotModel.Sample(x => Math.Abs(f(x) - equal.Evaluate(x)), a, b, 800).Max(p => p.Y);
            facts.Add(new("Largest error on [" + G(a) + ", " + G(b) + "]", $"Chebyshev {samples:0.###E+0},  equally spaced {samplesEqual:0.###E+0}", "sampled at 800 points; equally spaced nodes oscillate near the ends"));
            facts.Add(new("f(" + G(query) + ")", G(f(query)), $"interpolant {G(chebyshev.Evaluate(query))}"));
        }
        else
        {
            if (!MatrixConversion.TryParse(input.Points, out var matrix) || matrix.Columns != 2) return new Work([new("Points", "not a matrix of numbers with two columns")], null);
            var xs = Enumerable.Range(0, matrix.Rows).Select(r => matrix[r, 0].ToDouble()).ToArray();
            var ys = Enumerable.Range(0, matrix.Rows).Select(r => matrix[r, 1].ToDouble()).ToArray();
            if (xs.Length < 2) return new Work([new("Points", "needs at least two points")], null);
            var increasing = xs.Zip(xs.Skip(1), (p, q) => q > p).All(ok => ok);
            var distinct = xs.Distinct().Count() == xs.Length;
            if (method is InterpolationMethod.NaturalSpline or InterpolationMethod.ClampedSpline && !increasing) return new Work([new("Spline", "the x values must be strictly increasing")], null);
            if (!distinct) return new Work([new("Interpolation", "the x values must be distinct")], null);

            lo = xs.Min();
            hi = xs.Max();
            CubicSpline<double>? spline = null;
            switch (method)
            {
                case InterpolationMethod.Newton:
                    interpolant = Interpolate.Newton(xs, ys);
                    break;
                case InterpolationMethod.Barycentric:
                    interpolant = Interpolate.Barycentric(xs, ys);
                    break;
                case InterpolationMethod.NaturalSpline:
                    interpolant = spline = Interpolate.NaturalSpline(xs, ys);
                    break;
                default:
                    interpolant = spline = Interpolate.ClampedSpline(xs, ys, NumberInputs.ReadDouble(input.StartSlope) ?? 0, NumberInputs.ReadDouble(input.EndSlope) ?? 0);
                    break;
            }

            var pad = (hi - lo) * 0.05;
            series.Add(new PlotSeries(method.ToString(), PlotModel.Sample(interpolant.Evaluate, lo - pad, hi + pad), SeriesStyle.Line, 0));
            series.Add(new PlotSeries("data", [.. xs.Select((x, i) => new PlotPoint(x, ys[i]))], SeriesStyle.Points, 2));
            facts.Add(new("Points", $"{xs.Length}", "every point lies on the curve"));
            if (spline is not null) facts.Add(new("Integral over the data range", G(spline.Integral(lo, hi)), "CubicSpline.Integral, exact for the piecewise cubic"));
            if (spline is not null) facts.Add(new("f″ at the query", G(spline.SecondDerivative(query))));
        }

        var value = interpolant.Evaluate(query);
        facts.Insert(0, new($"Value at x = {G(query)}", G(value), query < lo || query > hi ? "outside the data: this is extrapolation" : null));
        if (interpolant is NewtonInterpolant<double> newton) facts.Add(new("Derivative at the query", G(newton.Derivative(query)), "NewtonInterpolant.Derivative"));
        if (interpolant is CubicSpline<double> cubic) facts.Add(new("Derivative at the query", G(cubic.Derivative(query)), "CubicSpline.Derivative"));
        markers.Add(new PlotMarker(query, value, $"({G(query)}, {G(value)})", 1));
        // Keep the view on the data: a query far outside it is extrapolation and must not stretch the plot.
        var margin = (hi - lo) * 0.05;
        return new Work(facts, new PlotModel { Title = method.ToString(), Series = series, Markers = markers, XMin = lo - margin, XMax = hi + margin });
    }
}
