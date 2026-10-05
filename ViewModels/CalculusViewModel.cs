using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Calculus;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum CalculusOperation
{
    Differentiate,
    Integrate,
    DefiniteIntegral,
    Limit,
    Taylor,
    Series,
    ImplicitDerivative,
}

/// <summary>
/// Calculus: derivatives, integrals (checked by differentiating back), limits and series. Bounds, points and centres are expressions with no variables,
/// so <c>pi</c>, <c>-oo</c> and <c>1/2</c> are fine and <c>x</c> is refused by the attribute.
/// </summary>
public sealed partial class CalculusViewModel : PageViewModel
{
    private readonly ISettingsService _settings;
    private readonly IOutcomePresenter _presenter;
    private readonly Debouncer _debounce = new();
    private string _lastDefault = string.Empty;

    public CalculusViewModel(ISettingsService settings, IOutcomePresenter presenter)
    {
        _settings = settings;
        _presenter = presenter;
        Variable = "x";
        Dependent = "y";
        Order = "1";
        Lower = "0";
        Upper = "pi";
        Point = "0";
        Center = "0";
        TaylorOrder = "5";
        ImplicitEquation = "x^2 + y^2 = 25";
        Direction = LimitDirection.Both;
        Operation = CalculusOperation.Differentiate;
        ApplyOperation(Operation);
    }

    public IReadOnlyList<CalculusOperation> Operations { get; } = Enum.GetValues<CalculusOperation>();

    public IReadOnlyList<LimitDirection> Directions { get; } = Enum.GetValues<LimitDirection>();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Expression")]
    [Required]
    [MathExpression(CheckSorts = true)]
    public partial string? Expression { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Equation")]
    [Required]
    [MathEquation]
    public partial string? ImplicitEquation { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Variable")]
    [Required]
    [RegularExpression("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "{0} must be a variable name such as x or theta.")]
    public partial string? Variable { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Dependent variable")]
    [Required]
    [RegularExpression("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "{0} must be a variable name such as y.")]
    public partial string? Dependent { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Order")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "10")]
    public partial string? Order { get; set; }

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
    [NotifyDataErrorInfo]
    [Display(Name = "Point")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? Point { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Centre")]
    [Required]
    [MathExpression(Variables = [])]
    public partial string? Center { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Order of the series")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("0", "20")]
    public partial string? TaylorOrder { get; set; }

    [ObservableProperty]
    public partial CalculusOperation Operation { get; set; }

    [ObservableProperty]
    public partial LimitDirection Direction { get; set; }

    [ObservableProperty]
    public partial string? OperationNote { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Sample>? Samples { get; set; }

    [ObservableProperty]
    public partial bool UsesExpression { get; set; }

    [ObservableProperty]
    public partial bool UsesOrder { get; set; }

    [ObservableProperty]
    public partial bool UsesBounds { get; set; }

    [ObservableProperty]
    public partial bool UsesPoint { get; set; }

    [ObservableProperty]
    public partial bool UsesCenter { get; set; }

    [ObservableProperty]
    public partial bool UsesImplicit { get; set; }

    [ObservableProperty]
    public partial PlotModel? Plot { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Expression = text;

    partial void OnOperationChanged(CalculusOperation value) => ApplyOperation(value);

    partial void OnExpressionChanged(string? value) => _ = Refresh();

    partial void OnImplicitEquationChanged(string? value) => _ = Refresh();

    partial void OnVariableChanged(string? value) => _ = Refresh();

    partial void OnDependentChanged(string? value) => _ = Refresh();

    partial void OnOrderChanged(string? value) => _ = Refresh();

    partial void OnLowerChanged(string? value) => _ = Refresh();

    partial void OnUpperChanged(string? value) => _ = Refresh();

    partial void OnPointChanged(string? value) => _ = Refresh();

    partial void OnCenterChanged(string? value) => _ = Refresh();

    partial void OnTaylorOrderChanged(string? value) => _ = Refresh();

    partial void OnDirectionChanged(LimitDirection value) => _ = Refresh();

    private void ApplyOperation(CalculusOperation value)
    {
        var (note, samples) = Describe(value);
        OperationNote = note;
        Samples = samples;
        UsesExpression = value != CalculusOperation.ImplicitDerivative;
        UsesImplicit = value == CalculusOperation.ImplicitDerivative;
        UsesOrder = value == CalculusOperation.Differentiate;
        UsesBounds = value == CalculusOperation.DefiniteIntegral;
        UsesPoint = value == CalculusOperation.Limit;
        UsesCenter = value is CalculusOperation.Taylor or CalculusOperation.Series;
        if (string.IsNullOrEmpty(Expression) || Expression == _lastDefault) Expression = samples[0].Text;
        _lastDefault = samples[0].Text;
        _ = Refresh();
    }

    private bool Valid(params string[] properties) => properties.All(p => !GetErrors(p).Any());

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(300), async token =>
    {
        var operation = Operation;
        var ready = operation switch
        {
            CalculusOperation.Differentiate => Valid(nameof(Expression), nameof(Variable), nameof(Order)),
            CalculusOperation.Integrate => Valid(nameof(Expression), nameof(Variable)),
            CalculusOperation.DefiniteIntegral => Valid(nameof(Expression), nameof(Variable), nameof(Lower), nameof(Upper)),
            CalculusOperation.Limit => Valid(nameof(Expression), nameof(Variable), nameof(Point)),
            CalculusOperation.Taylor or CalculusOperation.Series => Valid(nameof(Expression), nameof(Variable), nameof(Center), nameof(TaylorOrder)),
            _ => Valid(nameof(ImplicitEquation), nameof(Variable), nameof(Dependent)),
        };
        if (!ready || (UsesExpression ? string.IsNullOrWhiteSpace(Expression) : string.IsNullOrWhiteSpace(ImplicitEquation)))
        {
            Result = null;
            Plot = null;
            return;
        }

        var input = new Input(Expression ?? string.Empty, ImplicitEquation ?? string.Empty, Variable!, Dependent ?? "y", Order ?? "1", Lower ?? "0", Upper ?? "1", Point ?? "0", Center ?? "0", TaylorOrder ?? "5", Direction);
        var context = _settings.CreateContext();
        var budget = _settings.CreateBudget(token);
        var work = await RunAsync(() => Compute(operation, input, context, budget));
        if (token.IsCancellationRequested) return;
        Result = work.Display(_presenter);
        Plot = work.Plot;
    });

    private sealed record Input(string Expression, string Equation, string Variable, string Dependent, string Order, string Lower, string Upper, string Point, string Center, string TaylorOrder, LimitDirection Direction);

    private sealed record Work(Func<IOutcomePresenter, OutcomeDisplay> Display, PlotModel? Plot);

    private static Work Compute(CalculusOperation operation, Input input, MathContext context, Budget budget)
    {
        var x = new Symbol(input.Variable);
        static string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);
        static int Int(string text) => int.Parse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);

        if (operation == CalculusOperation.ImplicitDerivative)
        {
            var implicitResult = Cas.ImplicitDerivative(Expr.Parse(input.Equation), new Symbol(input.Dependent), x, context);
            return new Work(p => p.PresentExpr(implicitResult, $"d{input.Dependent}/d{input.Variable}"), null);
        }

        var f = Expr.Parse(input.Expression);
        switch (operation)
        {
            case CalculusOperation.Differentiate:
            {
                var outcome = Cas.Differentiate(f, x, Int(input.Order), context, budget);
                return new Work(p => p.PresentExpr(outcome, "Derivative"), MakePlot(x, -6, 6, ("f", f), outcome is Outcome<Expr>.Success { Value: var d } ? ("derivative", d) : default));
            }
            case CalculusOperation.Integrate:
            {
                var outcome = Cas.Integrate(f, x, context, budget);
                return new Work(p => p.PresentExpr(outcome, "Antiderivative (+ C)"), MakePlot(x, -6, 6, ("f", f), outcome is Outcome<Expr>.Success { Value: var integral } ? ("antiderivative", integral) : default));
            }
            case CalculusOperation.DefiniteIntegral:
            {
                var a = Expr.Parse(input.Lower);
                var b = Expr.Parse(input.Upper);
                var outcome = Cas.Integrate(f, x, a, b, context, budget);
                var lo = ExprFunctions.Constant(a);
                var hi = ExprFunctions.Constant(b);
                var from = lo is { } l && double.IsFinite(l) ? l : -6;
                var to = hi is { } h && double.IsFinite(h) ? h : 6;
                if (to <= from) to = from + 1;
                var plot = MakePlot(x, from - (to - from) * 0.15, to + (to - from) * 0.15, ("f", f), default);
                return new Work(p => p.PresentExpr(outcome, $"∫ from {Pretty(a)} to {Pretty(b)}"), plot is null ? null : new PlotModel { Title = plot.Title, Series = plot.Series, Markers = [.. new[] { lo, hi }.Where(v => v is { } n && double.IsFinite(n)).Select(v => new PlotMarker(v!.Value, 0, v.Value.ToString("0.###", CultureInfo.InvariantCulture)))] });
            }
            case CalculusOperation.Limit:
            {
                var point = Expr.Parse(input.Point);
                var outcome = Cas.Limit(f, x, point, input.Direction, context, budget);
                var at = ExprFunctions.Constant(point);
                var center = at is { } c && double.IsFinite(c) ? c : 0;
                var span = at is { } cc && double.IsFinite(cc) ? 3 : 20;
                var plot = MakePlot(x, center - span, center + span, ("f", f), default);
                return new Work(p => p.Present(outcome, v => v is LimitResult.DoesNotExist d ? "does not exist: " + d.Reason : Pretty(v.ToExpression()), v => v.ToExpression().ToLatex(), v => v.ToExpression().ToString(), $"limit as {input.Variable} → {Pretty(point)}"), plot);
            }
            default:
            {
                var center = Expr.Parse(input.Center);
                var order = Int(input.TaylorOrder);
                var outcome = operation == CalculusOperation.Taylor ? Cas.Taylor(f, x, center, order, context) : Cas.Series(f, x, center, order, context);
                var at = ExprFunctions.Constant(center) ?? 0;
                var plot = MakePlot(x, at - 4, at + 4, ("f", f), outcome is Outcome<Expr>.Success { Value: var series } ? ($"order {order}", series) : default);
                return new Work(p => p.PresentExpr(outcome, operation == CalculusOperation.Taylor ? "Taylor polynomial" : "Series"), plot);
            }
        }
    }

    private static PlotModel? MakePlot(Symbol x, double from, double to, (string Name, Expr? Expression) first, (string Name, Expr? Expression) second)
    {
        var series = new List<PlotSeries>();
        if (first.Expression is not null && ExprFunctions.Of(first.Expression, x) is { } f) series.Add(new PlotSeries(first.Name, PlotModel.Sample(f, from, to), SeriesStyle.Line, 0));
        if (second.Expression is not null && ExprFunctions.Of(second.Expression, x) is { } g) series.Add(new PlotSeries(second.Name, PlotModel.Sample(g, from, to), SeriesStyle.Line, 1));
        return series.Count == 0 ? null : new PlotModel { Series = series, XMin = from, XMax = to };
    }

    private static (string Note, IReadOnlyList<Sample> Samples) Describe(CalculusOperation operation) => operation switch
    {
        CalculusOperation.Differentiate => ("Derivatives of any order, each step naming the rule: product, quotient, chain.",
            [new("product rule", "x^2*sin(x)"), new("chain rule", "sin(x^2)"), new("quotient", "exp(x)/x"), new("logarithm", "ln(x^2 + 1)")]),
        CalculusOperation.Integrate => ("Antiderivatives, each verified by differentiating the answer and zero-testing the difference.",
            [new("by parts", "x*cos(x)"), new("arctangent", "1/(x^2 + 1)"), new("x e^x", "x*exp(x)"), new("sin²", "sin(x)^2"), new("1/x", "1/x")]),
        CalculusOperation.DefiniteIntegral => ("Definite and improper integrals, compared with quadrature. A divergent integral is not returned as a number.",
            [new("x²", "x^2"), new("sin x", "sin(x)"), new("Gaussian-like", "1/(x^2 + 1)"), new("diverges on [0, 1]", "1/x")]),
        CalculusOperation.Limit => ("Limits, one-sided or two-sided, at a number or at infinity. If the two sides differ the answer is undefined.",
            [new("sin x / x", "sin(x)/x"), new("e", "(1 + 1/x)^x"), new("1/x", "1/x"), new("(eˣ − 1)/x", "(exp(x) - 1)/x")]),
        CalculusOperation.Taylor => ("The Taylor polynomial around a centre, drawn next to the function.",
            [new("eˣ", "exp(x)"), new("sin x", "sin(x)"), new("1/(1 − x)", "1/(1 - x)"), new("ln(1 + x)", "ln(1 + x)")]),
        CalculusOperation.Series => ("Series that may start with negative powers (Laurent) at a pole.",
            [new("two poles", "1/(x*(1 - x))"), new("1/sin x", "1/sin(x)"), new("x⁻² cos x", "cos(x)/x^2")]),
        _ => ("dy/dx of a relation between x and y without solving for y.",
            [new("circle", "x^2 + y^2 = 25")]),
    };
}
