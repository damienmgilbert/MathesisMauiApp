using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Numbers;
using Mathesis.Numerics;
using Mathesis.Numerics.Optimization;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum RootMethod
{
    Brent,
    Bisection,
    NewtonSymbolic,
    NewtonNumeric,
    NewtonAutomatic,
    Secant,
}

/// <summary>
/// Root finding and minimization. The function is typed as an expression, validated, and compiled to <c>double</c> and to <c>Dual&lt;double&gt;</c>;
/// Newton's method then gets its derivative three ways: symbolic (<c>Cas.Differentiate</c>), numeric (finite differences) and automatic (dual numbers).
/// </summary>
public sealed partial class RootsViewModel : PageViewModel
{
    private static readonly Symbol X = new("x");
    private static readonly Symbol Y = new("y");

    private readonly ISettingsService _settings;
    private readonly Debouncer _debounce = new();

    public RootsViewModel(ISettingsService settings)
    {
        _settings = settings;
        Function = "x^3 - 2x - 5";
        First = "2";
        Second = "3";
        Tolerance = "1e-12";
        MaxIterations = "100";
        Method = RootMethod.Brent;
        Objective = "(x - 2)^2 + 1";
        MinFrom = "0";
        MinTo = "5";
        Objective2 = "(x - 1)^2 + (y + 2)^2 + x*y/4";
        StartX = "0";
        StartY = "0";
    }

    public IReadOnlyList<RootMethod> Methods { get; } = Enum.GetValues<RootMethod>();

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("cubic", "x^3 - 2x - 5"),
        new("cos x − x", "cos(x) - x"),
        new("eˣ − 3", "exp(x) - 3"),
        new("x² − 2", "x^2 - 2"),
        new("x e^x − 1", "x*exp(x) - 1"),
        new("1/x", "1/x"),
        new("y", "y"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "f(x)")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.Set | OperatorFamily.LinearAlgebra)]
    public partial string? Function { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "First point")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? First { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Second point")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? Second { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Tolerance")]
    [Required]
    [ExactRange("0", "1", MinimumIsExclusive = true)]
    public partial string? Tolerance { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Maximum iterations")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "10000")]
    public partial string? MaxIterations { get; set; }

    [ObservableProperty]
    public partial RootMethod Method { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? RootFacts { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<VerdictRow>? Comparison { get; set; }

    [ObservableProperty]
    public partial PlotModel? RootPlot { get; set; }

    // ----- minimization -----

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "g(x)")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"])]
    public partial string? Objective { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "From")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? MinFrom { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "To")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? MinTo { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "h(x, y)")]
    [Required]
    [MathExpression(Variables = ["x", "y"], RequiredVariables = ["x", "y"])]
    public partial string? Objective2 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Start x")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? StartX { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Start y")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? StartY { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? MinimumFacts { get; set; }

    [ObservableProperty]
    public partial PlotModel? MinimumPlot { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Function = text;

    partial void OnFunctionChanged(string? value) => _ = RefreshRoots();

    partial void OnFirstChanged(string? value) => _ = RefreshRoots();

    partial void OnSecondChanged(string? value) => _ = RefreshRoots();

    partial void OnToleranceChanged(string? value) => _ = RefreshRoots();

    partial void OnMaxIterationsChanged(string? value) => _ = RefreshRoots();

    partial void OnMethodChanged(RootMethod value) => _ = RefreshRoots();

    partial void OnObjectiveChanged(string? value) => _ = RefreshMinimum();

    partial void OnMinFromChanged(string? value) => _ = RefreshMinimum();

    partial void OnMinToChanged(string? value) => _ = RefreshMinimum();

    partial void OnObjective2Changed(string? value) => _ = RefreshMinimum();

    partial void OnStartXChanged(string? value) => _ = RefreshMinimum();

    partial void OnStartYChanged(string? value) => _ = RefreshMinimum();

    private bool Valid(params string[] properties) => properties.All(p => !GetErrors(p).Any());

    private readonly Debouncer _minimumDebounce = new();

    private Task RefreshRoots() => Debounced(_debounce, TimeSpan.FromMilliseconds(250), async token =>
    {
        if (!Valid(nameof(Function), nameof(First), nameof(Second), nameof(Tolerance), nameof(MaxIterations)) || string.IsNullOrWhiteSpace(Function))
        {
            RootFacts = null;
            Comparison = null;
            RootPlot = null;
            return;
        }

        var (function, first, second, tolerance, iterations, method) = (Function!, First!, Second!, Tolerance!, MaxIterations!, Method);
        var work = await RunAsync(() => ComputeRoots(function, first, second, tolerance, iterations, method, _settings.CreateContext(), _settings.CreateBudget(token)));
        if (token.IsCancellationRequested) return;
        RootFacts = work.Facts;
        Comparison = work.Comparison;
        RootPlot = work.Plot;
    });

    private Task RefreshMinimum() => Debounced(_minimumDebounce, TimeSpan.FromMilliseconds(250), async token =>
    {
        if (!Valid(nameof(Objective), nameof(MinFrom), nameof(MinTo), nameof(Objective2), nameof(StartX), nameof(StartY)) || string.IsNullOrWhiteSpace(Objective) || string.IsNullOrWhiteSpace(Objective2))
        {
            MinimumFacts = null;
            MinimumPlot = null;
            return;
        }

        var (g, from, to, h, sx, sy) = (Objective!, MinFrom!, MinTo!, Objective2!, StartX!, StartY!);
        var work = await RunAsync(() => ComputeMinimum(g, from, to, h, sx, sy));
        if (token.IsCancellationRequested) return;
        MinimumFacts = work.Facts;
        MinimumPlot = work.Plot;
    });

    private sealed record RootWork(IReadOnlyList<Fact> Facts, IReadOnlyList<VerdictRow> Comparison, PlotModel? Plot);

    private sealed record MinimumWork(IReadOnlyList<Fact> Facts, PlotModel? Plot);

    private static StoppingCriteria Stop(string tolerance, string iterations) =>
        new(AbsoluteTolerance: NumberInputs.ReadDouble(tolerance), MaxIterations: int.Parse(iterations.Trim(), CultureInfo.InvariantCulture));

    private static RootWork ComputeRoots(string text, string firstText, string secondText, string toleranceText, string iterationText, RootMethod method, MathContext context, Budget budget)
    {
        var expr = Expr.Parse(text);
        var f = ExprFunctions.Of(expr, X);
        var a = ExprFunctions.Constant(firstText) ?? 0;
        var b = ExprFunctions.Constant(secondText) ?? 1;
        if (f is null) return new RootWork([new("f(x)", "cannot be compiled")], [], null);
        var stop = Stop(toleranceText, iterationText);

        // The three sources of a derivative for Newton's method.
        Func<double, double>? symbolic = Cas.Differentiate(expr, X, 1, context, budget) is Outcome<Expr>.Success { Value: var d } ? ExprFunctions.Of(d, X) : null;
        Func<Dual<double>, Dual<double>>? automatic = expr.Compile<Dual<double>>(X) is Outcome<CompiledExpr<Dual<double>>>.Success { Value: var dual } ? value => dual.Invoke(value) : null;

        RootResult<double>? Run(RootMethod m)
        {
            try
            {
                return m switch
                {
                    RootMethod.Brent => Roots.Brent(f, a, b, stop),
                    RootMethod.Bisection => Roots.Bisection(f, a, b, stop),
                    RootMethod.NewtonSymbolic => symbolic is null ? null : Roots.Newton(f, symbolic, a, stop),
                    RootMethod.NewtonNumeric => Roots.Newton(f, a, stop),
                    RootMethod.NewtonAutomatic => automatic is null ? null : Roots.NewtonAutomatic(automatic, a, stop),
                    _ => Roots.Secant(f, a, b, stop),
                };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static string G(double value) => value.ToString("G12", CultureInfo.InvariantCulture);
        var result = Run(method);
        var facts = new List<Fact>();
        if (result is null) facts.Add(new("Result", "this method cannot run here", method is RootMethod.NewtonSymbolic ? "no symbolic derivative" : "the arguments are not usable"));
        else
        {
            facts.Add(new("Root", G(result.Root), result.Converged ? "converged" : "did not converge"));
            facts.Add(new("f(root)", G(result.FunctionValue)));
            facts.Add(new("Error estimate", G(result.ErrorEstimate)));
            facts.Add(new("Iterations", result.Iterations.ToString(CultureInfo.InvariantCulture), $"{result.Evaluations} function evaluations"));
            facts.Add(new("Stopped because", result.Reason.ToString(), result.Reason switch
            {
                Convergence.Tolerance => "the step or interval became smaller than the tolerance",
                Convergence.FunctionTolerance => "|f| fell below the function tolerance",
                Convergence.MaxIterations => "the iteration limit was reached",
                Convergence.InvalidBracket => "f has the same sign at both ends: there is no bracket",
                Convergence.ZeroDerivative => "the derivative vanished",
                Convergence.NotFinite => "a value was not finite",
                _ => null,
            }));
        }

        if (Cas.NSolve(Sym.Eq(expr, 0), X, Math.Min(a, b), Math.Max(a, b), context, budget) is Outcome<System.Collections.Immutable.ImmutableArray<double>>.Success { Value: var found })
        {
            facts.Add(new("Cas.NSolve on the interval", found.Length == 0 ? "no root found" : string.Join(",  ", found.Select(G)), "the symbolic layer's own search, as a cross-check"));
        }

        var comparison = new List<VerdictRow>();
        foreach (var m in Enum.GetValues<RootMethod>())
        {
            var r = Run(m);
            comparison.Add(r is null
                ? new VerdictRow(m.ToString(), "n/a", "cannot run", false)
                : new VerdictRow(m.ToString(), $"{r.Iterations} it · {r.Evaluations} ev", $"{G(r.Root)}   |f| = {Math.Abs(r.FunctionValue):0.##E+0}", r.Converged));
        }

        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        var pad = high - low < 1e-9 ? 3 : (high - low) * 0.75;
        var markers = new List<PlotMarker>();
        if (result is not null && double.IsFinite(result.Root)) markers.Add(new PlotMarker(result.Root, 0, G(result.Root)));
        markers.Add(new PlotMarker(a, f(a), "a", 5));
        if (method is RootMethod.Brent or RootMethod.Bisection or RootMethod.Secant) markers.Add(new PlotMarker(b, f(b), "b", 5));
        var plot = new PlotModel
        {
            Title = "f(x)",
            Series = [new PlotSeries("f", PlotModel.Sample(f, low - pad, high + pad))],
            Markers = markers,
            XMin = low - pad,
            XMax = high + pad,
        };
        return new RootWork(facts, comparison, plot);
    }

    private static MinimumWork ComputeMinimum(string gText, string fromText, string toText, string hText, string sxText, string syText)
    {
        static string G(double value) => value.ToString("G10", CultureInfo.InvariantCulture);
        var facts = new List<Fact>();
        PlotModel? plot = null;

        var g = ExprFunctions.Of(Expr.Parse(gText), X);
        var from = ExprFunctions.Constant(fromText) ?? 0;
        var to = ExprFunctions.Constant(toText) ?? 1;
        if (g is not null && from < to)
        {
            var golden = Minimize.GoldenSection(g, from, to);
            facts.Add(new("Golden section on [" + G(from) + ", " + G(to) + "]", $"x = {G(golden.Minimizer[0])},  g = {G(golden.Minimum)}", $"{golden.Iterations} iterations, {golden.Evaluations} evaluations, {(golden.Converged ? "converged" : "not converged")}"));
            plot = new PlotModel
            {
                Title = "g(x)",
                Series = [new PlotSeries("g", PlotModel.Sample(g, from, to))],
                Markers = [new PlotMarker(golden.Minimizer[0], golden.Minimum, "min " + G(golden.Minimizer[0]))],
                XMin = from,
                XMax = to,
            };
        }
        else
        {
            facts.Add(new("Golden section", "needs g(x) and From below To"));
        }

        var h = ExprFunctions.Of(Expr.Parse(hText), X, Y);
        var start = new[] { ExprFunctions.Constant(sxText) ?? 0, ExprFunctions.Constant(syText) ?? 0 };
        if (h is not null)
        {
            var nelder = Minimize.NelderMead(point => h(point[0], point[1]), start);
            facts.Add(new("Nelder–Mead in two variables", $"(x, y) = ({G(nelder.Minimizer[0])}, {G(nelder.Minimizer[1])}),  h = {G(nelder.Minimum)}", $"{nelder.Iterations} iterations, {nelder.Evaluations} evaluations, {(nelder.Converged ? "converged" : "not converged")}; derivative-free simplex search"));
        }
        else
        {
            facts.Add(new("Nelder–Mead", "needs h(x, y)"));
        }

        return new MinimumWork(facts, plot);
    }
}
