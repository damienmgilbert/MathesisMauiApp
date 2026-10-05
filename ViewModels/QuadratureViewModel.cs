using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Numerics;
using Mathesis.Numerics.Differentiation;
using Mathesis.Numerics.Integration;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>Definite integrals by five numerical methods and derivatives by six finite-difference schemes, each compared with the exact symbolic answer.</summary>
public sealed partial class QuadratureViewModel : PageViewModel
{
    private static readonly Symbol X = new("x");

    private readonly ISettingsService _settings;
    private readonly IOutcomePresenter _presenter;
    private readonly Debouncer _integralDebounce = new();
    private readonly Debouncer _derivativeDebounce = new();

    public QuadratureViewModel(ISettingsService settings, IOutcomePresenter presenter)
    {
        _settings = settings;
        _presenter = presenter;
        Function = "sin(x)";
        Lower = "0";
        Upper = "pi";
        DerivativeFunction = "sin(x)";
        Point = "1";
        Step = "1e-4";
    }

    /// <summary>Each sample is "function|from|to".</summary>
    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("sin x on [0, π]", "sin(x)|0|pi"),
        new("x e^x on [0, 1]", "x*exp(x)|0|1"),
        new("1/(1 + x²) on ℝ", "1/(1 + x^2)|-oo|oo"),
        new("e^(−x²) on ℝ", "exp(-x^2)|-oo|oo"),
        new("√x on [0, 1]", "sqrt(x)|0|1"),
        new("1/x on [1, e]", "1/x|1|e"),
        new("1/x on [0, 1] (diverges)", "1/x|0|1"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "f(x)")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.Set | OperatorFamily.LinearAlgebra)]
    public partial string? Function { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Lower bound")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? Lower { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Upper bound")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? Upper { get; set; }

    [ObservableProperty]
    public partial OutcomeDisplay? ExactIntegral { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<VerdictRow>? Methods { get; set; }

    [ObservableProperty]
    public partial PlotModel? Plot { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "f(x)")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.Set | OperatorFamily.LinearAlgebra)]
    public partial string? DerivativeFunction { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Point")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? Point { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Step")]
    [Required]
    [ExactRange("0", "1", MinimumIsExclusive = true)]
    public partial string? Step { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<VerdictRow>? Derivatives { get; set; }

    [RelayCommand]
    private void UseSample(string text)
    {
        var parts = text.Split('|');
        Function = parts[0];
        if (parts.Length > 2)
        {
            Lower = parts[1];
            Upper = parts[2];
        }
    }

    partial void OnFunctionChanged(string? value) => _ = RefreshIntegral();

    partial void OnLowerChanged(string? value) => _ = RefreshIntegral();

    partial void OnUpperChanged(string? value) => _ = RefreshIntegral();

    partial void OnDerivativeFunctionChanged(string? value) => _ = RefreshDerivative();

    partial void OnPointChanged(string? value) => _ = RefreshDerivative();

    partial void OnStepChanged(string? value) => _ = RefreshDerivative();

    private bool Valid(params string[] properties) => properties.All(p => !GetErrors(p).Any());

    private Task RefreshIntegral() => Debounced(_integralDebounce, TimeSpan.FromMilliseconds(300), async token =>
    {
        if (!Valid(nameof(Function), nameof(Lower), nameof(Upper)) || string.IsNullOrWhiteSpace(Function))
        {
            ExactIntegral = null;
            Methods = null;
            Plot = null;
            return;
        }

        var (text, lower, upper) = (Function!, Lower!, Upper!);
        var context = _settings.CreateContext();
        var budget = _settings.CreateBudget(token);
        var work = await RunAsync(() => ComputeIntegral(text, lower, upper, context, budget));
        if (token.IsCancellationRequested) return;
        ExactIntegral = work.Exact is null ? null : _presenter.PresentExpr(work.Exact, "Exact integral");
        Methods = work.Rows;
        Plot = work.Plot;
    });

    private Task RefreshDerivative() => Debounced(_derivativeDebounce, TimeSpan.FromMilliseconds(300), async token =>
    {
        if (!Valid(nameof(DerivativeFunction), nameof(Point), nameof(Step)) || string.IsNullOrWhiteSpace(DerivativeFunction))
        {
            Derivatives = null;
            return;
        }

        var (text, point, step) = (DerivativeFunction!, Point!, Step!);
        var context = _settings.CreateContext();
        var budget = _settings.CreateBudget(token);
        var rows = await RunAsync(() => ComputeDerivatives(text, point, step, context, budget));
        if (!token.IsCancellationRequested) Derivatives = rows;
    });

    private sealed record IntegralWork(Outcome<Expr>? Exact, IReadOnlyList<VerdictRow> Rows, PlotModel? Plot);

    private static IntegralWork ComputeIntegral(string text, string lowerText, string upperText, MathContext context, Budget budget)
    {
        var expr = Expr.Parse(text);
        var f = ExprFunctions.Of(expr, X);
        var lowerExpr = Expr.Parse(lowerText);
        var upperExpr = Expr.Parse(upperText);
        var a = ExprFunctions.Constant(lowerExpr);
        var b = ExprFunctions.Constant(upperExpr);
        if (f is null || a is null || b is null) return new IntegralWork(null, [new VerdictRow("integral", "n/a", "the function or a bound cannot be evaluated", false)], null);

        var exact = Cas.Integrate(expr, X, lowerExpr, upperExpr, context, budget);
        double? reference = exact is Outcome<Expr>.Success { Value: var value } && Cas.N(value) is Outcome<double>.Success { Value: var number } ? number : null;

        static string G(double value) => value.ToString("G12", CultureInfo.InvariantCulture);
        var rows = new List<VerdictRow>();
        void Add(string name, Func<QuadratureResult<double>> run)
        {
            try
            {
                var r = run();
                var error = reference is { } exactValue ? $"   |Δ| = {Math.Abs(r.Value - exactValue):0.##E+0}" : string.Empty;
                rows.Add(new VerdictRow(name, $"{r.Evaluations} evaluations · est. {r.ErrorEstimate:0.##E+0}", G(r.Value) + error + (r.Warnings.IsDefaultOrEmpty ? string.Empty : "   ⚠ " + string.Join("; ", r.Warnings)), r.Converged));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                rows.Add(new VerdictRow(name, "n/a", ex.Message, false));
            }
        }

        var infinite = double.IsInfinity(a.Value) || double.IsInfinity(b.Value);
        if (!infinite)
        {
            Add("Integrate (automatic)", () => Quadrature.Integrate(f, a.Value, b.Value));
            Add("AdaptiveSimpson", () => Quadrature.AdaptiveSimpson(f, a.Value, b.Value));
            Add("GaussKronrod", () => Quadrature.GaussKronrod(f, a.Value, b.Value));
            Add("Romberg", () => Quadrature.Romberg(f, a.Value, b.Value));
        }

        Add("Infinite (transformed)", () => Quadrature.Infinite(f, a.Value, b.Value));

        var from = double.IsFinite(a.Value) ? a.Value : -6;
        var to = double.IsFinite(b.Value) ? b.Value : 6;
        if (to <= from) to = from + 1;
        var pad = (to - from) * 0.15;
        var plot = new PlotModel
        {
            Title = "f(x), integrated from " + lowerText + " to " + upperText,
            Series = [new PlotSeries("f", PlotModel.Sample(f, from - pad, to + pad))],
            Markers = [new PlotMarker(from, 0, double.IsFinite(a.Value) ? G(a.Value) : "−∞", 5), new PlotMarker(to, 0, double.IsFinite(b.Value) ? G(b.Value) : "∞", 5)],
        };
        return new IntegralWork(exact, rows, plot);
    }

    private static IReadOnlyList<VerdictRow> ComputeDerivatives(string text, string pointText, string stepText, MathContext context, Budget budget)
    {
        var expr = Expr.Parse(text);
        var f = ExprFunctions.Of(expr, X);
        var x = ExprFunctions.Constant(pointText);
        var h = NumberInputs.ReadDouble(stepText);
        if (f is null || x is null || h is null) return [new VerdictRow("derivative", "n/a", "the function or the point cannot be evaluated", false)];

        static string G(double value) => value.ToString("G12", CultureInfo.InvariantCulture);
        double? Exact(int order) =>
            Cas.Differentiate(expr, X, order, context, budget) is Outcome<Expr>.Success { Value: var d } && Cas.N(d, new Dictionary<Symbol, double> { [X] = x.Value }) is Outcome<double>.Success { Value: var v } ? v : null;
        var first = Exact(1);
        var second = Exact(2);

        var rows = new List<VerdictRow>();
        void Add(string name, string order, double value, double? reference)
        {
            var error = reference is { } r ? $"   |Δ| = {Math.Abs(value - r):0.##E+0}" : string.Empty;
            rows.Add(new VerdictRow(name, order, G(value) + error, reference is null || Math.Abs(value - reference.Value) < 1e-3 * (1 + Math.Abs(reference.Value))));
        }

        rows.Add(new VerdictRow("Exact (Cas.Differentiate)", "f′", first is { } d1 ? G(d1) : "unavailable", first is not null));
        Add("Forward", "f′, O(h)", FiniteDifferences.Forward(f, x.Value, h.Value), first);
        Add("Backward", "f′, O(h)", FiniteDifferences.Backward(f, x.Value, h.Value), first);
        Add("Central", "f′, O(h²)", FiniteDifferences.Central(f, x.Value, h.Value), first);
        Add("FivePoint", "f′, O(h⁴)", FiniteDifferences.FivePoint(f, x.Value, h.Value), first);
        var richardson = FiniteDifferences.Richardson(f, x.Value, h.Value);
        rows.Add(new VerdictRow("Richardson extrapolation", $"f′, {richardson.Evaluations} evaluations · est. {richardson.ErrorEstimate:0.##E+0}", G(richardson.Value) + (first is { } r1 ? $"   |Δ| = {Math.Abs(richardson.Value - r1):0.##E+0}" : string.Empty), true));
        rows.Add(new VerdictRow("Exact second derivative", "f″", second is { } d2 ? G(d2) : "unavailable", second is not null));
        Add("Second (central)", "f″, O(h²)", FiniteDifferences.Second(f, x.Value, h.Value), second);
        return rows;
    }
}
