using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Solving;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum SolveMode
{
    Equation,
    Inequality,
    System,
    Numeric,
}

/// <summary>Equations, inequalities and systems. Each input has the attribute that fits what the solver accepts: an equation, an inequality, or a list of equations.</summary>
public sealed partial class SolveViewModel : PageViewModel
{
    private readonly ISettingsService _settings;
    private readonly IOutcomePresenter _presenter;
    private readonly Debouncer _debounce = new();

    public SolveViewModel(ISettingsService settings, IOutcomePresenter presenter)
    {
        _settings = settings;
        _presenter = presenter;
        Equation = "x^2 - 5x + 6 = 0";
        Inequality = "(x - 1)/(x + 2) >= 0";
        Equation1 = "x + y = 3";
        Equation2 = "x - y = 1";
        Variable = "x";
        SystemVariables = "x, y";
        Low = "0";
        High = "1";
        Method = SolveMethod.Auto;
        Mode = SolveMode.Equation;
        ApplyMode(Mode);
    }

    public IReadOnlyList<SolveMode> Modes { get; } = Enum.GetValues<SolveMode>();

    public IReadOnlyList<SolveMethod> Methods { get; } = Enum.GetValues<SolveMethod>();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Equation")]
    [Required]
    [MathEquation]
    public partial string? Equation { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Inequality")]
    [Required]
    [MathExpression(Shape = ExpressionShape.Inequality)]
    public partial string? Inequality { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "First equation")]
    [Required]
    [MathEquation]
    public partial string? Equation1 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Second equation")]
    [Required]
    [MathEquation]
    public partial string? Equation2 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Third equation")]
    [MathEquation]
    public partial string? Equation3 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Variable")]
    [Required]
    [RegularExpression("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "{0} must be a variable name such as x or theta.")]
    public partial string? Variable { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Unknowns")]
    [Required]
    [RegularExpression(@"^\s*[A-Za-z]\w*(\s*,\s*[A-Za-z]\w*)*\s*$", ErrorMessage = "{0} must be variable names separated by commas, for example x, y.")]
    public partial string? SystemVariables { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Search from")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? Low { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Search to")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? High { get; set; }

    [ObservableProperty]
    public partial SolveMode Mode { get; set; }

    [ObservableProperty]
    public partial SolveMethod Method { get; set; }

    [ObservableProperty]
    public partial string? ModeNote { get; set; }

    [ObservableProperty]
    public partial bool IsEquation { get; set; }

    [ObservableProperty]
    public partial bool IsInequality { get; set; }

    [ObservableProperty]
    public partial bool IsSystem { get; set; }

    [ObservableProperty]
    public partial bool IsNumeric { get; set; }

    [ObservableProperty]
    public partial bool NeedsVariable { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Facts { get; set; }

    [ObservableProperty]
    public partial PlotModel? Plot { get; set; }

    public IReadOnlyList<Sample> EquationSamples { get; } =
    [
        new("quadratic", "x^2 - 5x + 6 = 0"),
        new("extraneous root", "sqrt(x + 2) = x"),
        new("trigonometric", "2*sin(x)^2 - sin(x) - 1 = 0"),
        new("exponential", "exp(x) = 5"),
        new("no real solution", "x^2 + 1 = 0"),
        new("quintic", "x^5 - x - 1 = 0"),
        new("absolute value", "abs(x - 2) = 3"),
        new("cubic", "x^3 - 2x - 5 = 0"),
        new("not an equation", "x^2 - 4"),
    ];

    public IReadOnlyList<Sample> InequalitySamples { get; } =
    [
        new("rational", "(x - 1)/(x + 2) >= 0"),
        new("quadratic", "x^2 - 4 < 0"),
        new("absolute value", "abs(x - 2) < 3"),
        new("cubic", "x^3 - x > 0"),
        new("not an inequality", "x^2 = 4"),
    ];

    [RelayCommand]
    private void UseEquationSample(string text) => Equation = text;

    [RelayCommand]
    private void UseInequalitySample(string text) => Inequality = text;

    partial void OnModeChanged(SolveMode value) => ApplyMode(value);

    partial void OnEquationChanged(string? value) => _ = Refresh();

    partial void OnInequalityChanged(string? value) => _ = Refresh();

    partial void OnEquation1Changed(string? value) => _ = Refresh();

    partial void OnEquation2Changed(string? value) => _ = Refresh();

    partial void OnEquation3Changed(string? value) => _ = Refresh();

    partial void OnVariableChanged(string? value) => _ = Refresh();

    partial void OnSystemVariablesChanged(string? value) => _ = Refresh();

    partial void OnLowChanged(string? value) => _ = Refresh();

    partial void OnHighChanged(string? value) => _ = Refresh();

    partial void OnMethodChanged(SolveMethod value) => _ = Refresh();

    private void ApplyMode(SolveMode mode)
    {
        IsEquation = mode == SolveMode.Equation;
        IsInequality = mode == SolveMode.Inequality;
        IsSystem = mode == SolveMode.System;
        IsNumeric = mode == SolveMode.Numeric;
        NeedsVariable = mode != SolveMode.System;
        ModeNote = mode switch
        {
            SolveMode.Equation => "Exact solutions: finite sets, image sets over the integers for trigonometric equations, or a numeric root when there is no closed form. Candidates from non-reversible steps are checked, and bad ones rejected.",
            SolveMode.Inequality => "Solution sets as unions of intervals.",
            SolveMode.System => "Two or three equations in the unknowns you list; linear and polynomial systems.",
            _ => "Cas.NSolve: every root of an equation in a closed interval, as doubles, found by bracketing and refinement.",
        };
        _ = Refresh();
    }

    private bool Valid(params string[] properties) => properties.All(p => !GetErrors(p).Any());

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(300), async token =>
    {
        var mode = Mode;
        var ready = mode switch
        {
            SolveMode.Equation => Valid(nameof(Equation), nameof(Variable)) && !string.IsNullOrWhiteSpace(Equation),
            SolveMode.Inequality => Valid(nameof(Inequality), nameof(Variable)) && !string.IsNullOrWhiteSpace(Inequality),
            SolveMode.System => Valid(nameof(Equation1), nameof(Equation2), nameof(Equation3), nameof(SystemVariables)) && !string.IsNullOrWhiteSpace(Equation1) && !string.IsNullOrWhiteSpace(Equation2),
            _ => Valid(nameof(Equation), nameof(Variable), nameof(Low), nameof(High)) && !string.IsNullOrWhiteSpace(Equation),
        };
        if (!ready)
        {
            Result = null;
            Facts = null;
            Plot = null;
            return;
        }

        var snapshot = (Equation: Equation!, Inequality: Inequality!, Equation1: Equation1!, Equation2: Equation2!, Equation3, Variable: Variable!, Unknowns: SystemVariables!, Low: Low, High: High, Method);
        var context = _settings.CreateContext();
        var budget = _settings.CreateBudget(token);
        var work = await RunAsync(() => Compute(mode, snapshot.Equation, snapshot.Inequality, [snapshot.Equation1, snapshot.Equation2, snapshot.Equation3], snapshot.Variable, snapshot.Unknowns, snapshot.Low, snapshot.High, snapshot.Method, context, budget));
        if (token.IsCancellationRequested) return;
        Result = work.Display(_presenter);
        Facts = work.Facts;
        Plot = work.Plot;
    });

    private sealed record Work(Func<IOutcomePresenter, OutcomeDisplay> Display, IReadOnlyList<Fact> Facts, PlotModel? Plot);

    private static Work Compute(SolveMode mode, string equation, string inequality, string?[] system, string variable, string unknowns, string? low, string? high,
        SolveMethod method, MathContext context, Budget budget)
    {
        var x = new Symbol(variable);
        static string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);

        if (mode == SolveMode.System)
        {
            var equations = system.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => Expr.Parse(s!)).ToList();
            var symbols = unknowns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(n => new Symbol(n)).ToList();
            var outcome = Cas.Solve(equations, symbols, context, budget);
            return new Work(p => p.Present(outcome, set => Pretty(set.Set), set => set.Set.ToLatex(), set => set.Set.ToString(), "Solution set"), Describe(outcome), null);
        }

        var relation = Expr.Parse(mode == SolveMode.Inequality ? inequality : equation);
        if (mode == SolveMode.Numeric)
        {
            var a = NumberInputs.ReadDouble(low) ?? 0;
            var b = NumberInputs.ReadDouble(high) ?? 1;
            var numeric = Cas.NSolve(relation, x, a, b, context, budget);
            var roots = numeric is Outcome<ImmutableArray<double>>.Success { Value: var r } ? r.ToArray() : [];
            var plotted = PlotRelation(relation, x, roots, [], a, b);
            return new Work(p => p.Present(numeric, values => values.Length == 0 ? "no root in the interval" : string.Join(",  ", values.Select(v => v.ToString("0.############", CultureInfo.InvariantCulture))), heading: "Roots in [" + a + ", " + b + "]"),
                [new("Roots found", roots.Length.ToString(CultureInfo.InvariantCulture), "refined to machine precision")], plotted);
        }

        var solved = Cas.Solve(relation, x, mode == SolveMode.Equation ? method : SolveMethod.Auto, context, budget);
        var approximations = solved is Outcome<SolutionSet>.Success { Value: var set } ? set.Approximations.ToArray() : [];
        var pieces = solved is Outcome<SolutionSet>.Success { Value: var found } ? found.Pieces : ImmutableArray<IntervalPiece>.Empty;
        return new Work(p => p.Present(solved, v => Pretty(v.Set), v => v.Set.ToLatex(), v => v.Set.ToString(), "Solution set"), Describe(solved), PlotRelation(relation, x, approximations, pieces, null, null));
    }

    private static IReadOnlyList<Fact> Describe(Outcome<SolutionSet> outcome)
    {
        if (outcome is not Outcome<SolutionSet>.Success { Value: var set }) return [];
        static string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);
        var facts = new List<Fact> { new("Kind", set.Kind.ToString(), set.Kind switch
        {
            SolutionKind.Finite => "a finite set of values",
            SolutionKind.Intervals => "a union of intervals",
            SolutionKind.Image => "image sets: values depending on an integer k",
            SolutionKind.All => "every value",
            SolutionKind.Empty => "no solution in the current number field",
            SolutionKind.Parametric => "a family with free parameters",
            _ => null,
        }) };

        if (set.Points.Length > 0)
        {
            for (var i = 0; i < set.Points.Length; i++)
            {
                var approx = i < set.Approximations.Length ? set.Approximations[i].ToString("0.############", CultureInfo.InvariantCulture) : null;
                facts.Add(new($"Solution {i + 1}", Pretty(set.Points[i]), approx is null ? null : $"≈ {approx}"));
            }
        }

        foreach (var family in set.Families) facts.Add(new("Family", $"{Pretty(family.Element)}   ({family.Parameter.Name} ∈ ℤ)"));
        foreach (var piece in set.Pieces) facts.Add(new("Interval", $"{(piece.LowerClosed ? "[" : "(")}{Pretty(piece.Lower)}, {Pretty(piece.Upper)}{(piece.UpperClosed ? "]" : ")")}"));
        if (set.IsApproximate) facts.Add(new("Approximate", "yes", "there is no closed form, so the value is a numeric root"));
        facts.Add(new("Complete", set.IsComplete ? "yes" : "no", set.IsComplete ? "every solution is in the set" : "more solutions may exist"));
        return facts;
    }

    /// <summary>Plots lhs − rhs with the solutions marked on the axis; intervals are drawn as thick segments along it.</summary>
    private static PlotModel? PlotRelation(Expr relation, Symbol x, IReadOnlyList<double> points, IReadOnlyList<IntervalPiece> pieces, double? from, double? to)
    {
        if (relation is not Apply { Arguments: [var left, var right] }) return null;
        if (ExprFunctions.Of(Sym.Sub(left, right), x) is not { } f) return null;

        var finite = points.Where(double.IsFinite).Concat(pieces.SelectMany(p => new[] { p.LowerValue, p.UpperValue }).Where(double.IsFinite)).ToList();
        var lo = from ?? (finite.Count == 0 ? -10 : finite.Min() - 3);
        var hi = to ?? (finite.Count == 0 ? 10 : finite.Max() + 3);
        if (hi - lo < 1e-6) { lo -= 1; hi += 1; }

        var series = new List<PlotSeries> { new("lhs − rhs", PlotModel.Sample(f, lo, hi)) };
        foreach (var piece in pieces)
        {
            var a = double.IsFinite(piece.LowerValue) ? piece.LowerValue : lo;
            var b = double.IsFinite(piece.UpperValue) ? piece.UpperValue : hi;
            series.Add(new PlotSeries(string.Empty, [new PlotPoint(Math.Max(a, lo), 0), new PlotPoint(Math.Min(b, hi), 0)], SeriesStyle.LineAndPoints, 1));
        }

        return new PlotModel
        {
            Title = pieces.Count > 0 ? "lhs − rhs, with the solution intervals on the axis" : "lhs − rhs",
            Series = series,
            Markers = [.. points.Where(double.IsFinite).Select(v => new PlotMarker(v, 0, v.ToString("0.###", CultureInfo.InvariantCulture)))],
            XMin = lo,
            XMax = hi,
        };
    }
}
