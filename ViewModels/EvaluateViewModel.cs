using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Printing;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// One expression evaluated in five number systems of Mathesis.Core: exact rationals (as a symbolic result), double, <c>Complex&lt;double&gt;</c>,
/// <c>Interval&lt;double&gt;</c> (a guaranteed enclosure) and <c>Dual&lt;double&gt;</c> (automatic differentiation, compared with the symbolic derivative).
/// </summary>
public sealed partial class EvaluateViewModel : PageViewModel
{
    private static readonly Symbol X = new("x");

    private readonly ISettingsService _settings;
    private readonly Debouncer _debounce = new();

    public EvaluateViewModel(ISettingsService settings)
    {
        _settings = settings;
        Expression = "sqrt(x) + x^2/3";
        XValue = "2";
        Imaginary = "0";
        Radius = "1/8";
        Digits = "6";
    }

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("√x + x²/3", "sqrt(x) + x^2/3"),
        new("sin x · eˣ", "sin(x)*exp(x)"),
        new("1/(x² − 1)", "1/(x^2 - 1)"),
        new("x ln x", "x*ln(x)"),
        new("(x³ − x)/(x + 2)", "(x^3 - x)/(x + 2)"),
        new("√(x² − 3)", "sqrt(x^2 - 3)"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Expression")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.LinearAlgebra | OperatorFamily.Set)]
    public partial string? Expression { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "x")]
    [Required]
    [ExactRange("-1000", "1000")]
    public partial string? XValue { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Imaginary part of x")]
    [Required]
    [ExactRange("-1000", "1000")]
    public partial string? Imaginary { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Radius")]
    [Required]
    [ExactRange("0", "10")]
    public partial string? Radius { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Digits")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "15")]
    public partial string? Digits { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Exact { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Approximate { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Rigorous { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Differentiated { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Expression = text;

    partial void OnExpressionChanged(string? value) => _ = Refresh();

    partial void OnXValueChanged(string? value) => _ = Refresh();

    partial void OnImaginaryChanged(string? value) => _ = Refresh();

    partial void OnRadiusChanged(string? value) => _ = Refresh();

    partial void OnDigitsChanged(string? value) => _ = Refresh();

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(250), async token =>
    {
        if (HasErrors || string.IsNullOrWhiteSpace(Expression))
        {
            Exact = Approximate = Rigorous = Differentiated = null;
            return;
        }

        var (text, x, im, radius, digits) = (Expression!, XValue!, Imaginary!, Radius!, Digits!);
        var context = _settings.CreateContext();
        var budget = _settings.CreateBudget(token);
        var work = await RunAsync(() => Compute(text, x, im, radius, digits, context, budget));
        if (token.IsCancellationRequested) return;
        (Exact, Approximate, Rigorous, Differentiated) = work;
    });

    private static (IReadOnlyList<Fact>, IReadOnlyList<Fact>, IReadOnlyList<Fact>, IReadOnlyList<Fact>) Compute(string text, string xText, string imText, string radiusText, string digitsText, MathContext context, Budget budget)
    {
        static string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);
        static string G(double value) => value.ToString("G15", CultureInfo.InvariantCulture);
        static string Say<T>(Outcome<T> outcome, Func<T, string> print) => outcome switch
        {
            Outcome<T>.Success s => print(s.Value),
            Outcome<T>.Partial p => print(p.Value) + " (partial: " + p.Reason + ")",
            Outcome<T>.Unevaluated u => "unevaluated: " + u.Reason,
            Outcome<T>.Failed f => "failed: " + f.Error,
            _ => string.Empty,
        };

        var expr = Expr.Parse(text);
        if (!NumberInputs.TryRead(xText, out var xr) || !NumberInputs.TryRead(imText, out var im) || !NumberInputs.TryRead(radiusText, out var rr)) return ([], [], [], []);
        var x = xr.ToDouble();
        var digits = int.Parse(digitsText.Trim(), CultureInfo.InvariantCulture);

        // Exact: the symbols are replaced by exact rationals and the result stays symbolic.
        var exact = Cas.Evaluate(expr, new Dictionary<Symbol, Expr> { [X] = Sym.Number(xr) }, context, budget);
        var exactFacts = new List<Fact> { new($"f({xr})", Say(exact, Pretty), "Cas.Evaluate: exact arithmetic; roots and trigonometric values stay symbolic") };

        var values = new Dictionary<Symbol, double> { [X] = x };
        var approximateFacts = new List<Fact>
        {
            new("double", Say(Cas.N(expr, values), G), "Cas.N, compiled to double arithmetic"),
            new($"{digits} digits", Say(Cas.N(expr, values, digits), G), "rounded to the digits you asked for"),
        };
        var complex = Evaluator.NComplex(expr, new Dictionary<Symbol, Complex<double>> { [X] = new Complex<double>(x, im.ToDouble()) });
        approximateFacts.Add(new($"complex, x = {G(x)} {(im.Sign < 0 ? "−" : "+")} {G(Math.Abs(im.ToDouble()))}i", Say(complex, z => Describe(z)), "principal values: Complex<double>"));

        // Rigorous: an interval around x, so the result contains every value the function takes there.
        var rigorousFacts = new List<Fact>();
        var radius = rr.ToDouble();
        if (expr.Compile<Interval<double>>(X) is Outcome<CompiledExpr<Interval<double>>>.Success { Value: var interval })
        {
            var enclosure = interval.Invoke(new Interval<double>(x - radius, x + radius));
            rigorousFacts.Add(new($"f([{G(x - radius)}, {G(x + radius)}])", enclosure.IsEmpty ? "∅" : $"[{G(enclosure.Lower)}, {G(enclosure.Upper)}]", "Interval<double>: every value of f on the interval is inside"));
            if (!enclosure.IsEmpty) rigorousFacts.Add(new("Width", G(enclosure.Width)));
            if (Cas.N(expr, values) is Outcome<double>.Success { Value: var point }) rigorousFacts.Add(new("Contains f(x)?", enclosure.Contains(point) ? "yes ✓" : "no", $"f({G(x)}) = {G(point)}"));
        }
        else
        {
            rigorousFacts.Add(new("Interval", "this expression cannot be evaluated over intervals"));
        }

        // Automatic differentiation: a dual number carries the value and the derivative through the same compiled code.
        var differentiated = new List<Fact>();
        if (expr.Compile<Dual<double>>(X) is Outcome<CompiledExpr<Dual<double>>>.Success { Value: var dual })
        {
            var d = dual.Invoke(Dual<double>.Variable(x));
            differentiated.Add(new("Dual number", $"value {G(d.Value)}, derivative {G(d.Derivative)}", "Dual<double>: automatic differentiation, no formula for f′ needed"));
            if (Cas.Differentiate(expr, X, 1, context, budget) is Outcome<Expr>.Success { Value: var symbolic })
            {
                differentiated.Add(new("Symbolic f′", Pretty(symbolic), "Cas.Differentiate"));
                if (Cas.N(symbolic, values) is Outcome<double>.Success { Value: var exactSlope })
                {
                    differentiated.Add(new("f′(x) from the symbolic derivative", G(exactSlope)));
                    differentiated.Add(new("Difference", Math.Abs(exactSlope - d.Derivative).ToString("0.###E+0", CultureInfo.InvariantCulture), "the two agree to rounding"));
                }
            }
        }
        else
        {
            differentiated.Add(new("Dual number", "this expression cannot be evaluated with dual numbers"));
        }

        return (exactFacts, approximateFacts, rigorousFacts, differentiated);
    }

    private static string Describe(Complex<double> z)
    {
        static string G(double value) => value.ToString("G12", CultureInfo.InvariantCulture);
        return $"{G(z.Real)} {(z.Imaginary < 0 ? "−" : "+")} {G(Math.Abs(z.Imaginary))}i";
    }
}
