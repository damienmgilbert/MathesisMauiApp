using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum AlgebraOperation
{
    Simplify,
    Expand,
    Factor,
    Collect,
    Together,
    Apart,
    Cancel,
    Rationalize,
    RadicalSimplify,
    CompleteSquare,
    PowerSimplify,
    LogCombine,
    LogExpand,
    TrigSimplify,
    TrigExpand,
    TrigReduce,
    TrigToExp,
    ExpToTrig,
}

/// <summary>Symbolic algebra: one expression, one operation, every step cited. The expression is validated before it ever reaches <c>Cas</c>.</summary>
public sealed partial class AlgebraViewModel : PageViewModel
{
    private readonly ISettingsService _settings;
    private readonly IOutcomePresenter _presenter;
    private readonly Debouncer _debounce = new();
    private string _lastDefault = string.Empty;

    public AlgebraViewModel(ISettingsService settings, IOutcomePresenter presenter)
    {
        _settings = settings;
        _presenter = presenter;
        Variable = "x";
        Operation = AlgebraOperation.Simplify;
        ApplyOperation(Operation);
    }

    public IReadOnlyList<AlgebraOperation> Operations { get; } = Enum.GetValues<AlgebraOperation>();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Expression")]
    [Required]
    [MathExpression(CheckSorts = true)]
    public partial string? Expression { get; set; }

    /// <summary>A standard attribute next to the Mathesis one: the variable the operation works in is a plain name.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Variable")]
    [Required]
    [RegularExpression("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "{0} must be a variable name such as x or theta.")]
    public partial string? Variable { get; set; }

    [ObservableProperty]
    public partial AlgebraOperation Operation { get; set; }

    [ObservableProperty]
    public partial bool AssumePositive { get; set; }

    [ObservableProperty]
    public partial string? OperationNote { get; set; }

    [ObservableProperty]
    public partial bool NeedsVariable { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Sample>? Samples { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Expression = text;

    partial void OnExpressionChanged(string? value) => _ = Refresh();

    partial void OnVariableChanged(string? value) => _ = Refresh();

    partial void OnAssumePositiveChanged(bool value) => _ = Refresh();

    partial void OnOperationChanged(AlgebraOperation value) => ApplyOperation(value);

    private void ApplyOperation(AlgebraOperation value)
    {
        var (note, samples) = Describe(value);
        OperationNote = note;
        Samples = samples;
        NeedsVariable = value is AlgebraOperation.Collect or AlgebraOperation.Apart or AlgebraOperation.CompleteSquare;

        // Move to the first example of the operation unless the user has typed something of their own.
        if (string.IsNullOrEmpty(Expression) || Expression == _lastDefault) Expression = samples[0].Text;
        _lastDefault = samples[0].Text;
        _ = Refresh();
    }

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(300), async token =>
    {
        var needsVariable = NeedsVariable;
        if (GetErrors(nameof(Expression)).Any() || string.IsNullOrWhiteSpace(Expression) || (needsVariable && (GetErrors(nameof(Variable)).Any() || string.IsNullOrWhiteSpace(Variable))))
        {
            Result = null;
            return;
        }

        var (text, operation, variableName, positive) = (Expression!, Operation, Variable ?? "x", AssumePositive);
        var context = _settings.CreateContext();
        var budget = _settings.CreateBudget(token);
        var outcome = await RunAsync(() =>
        {
            var expr = Expr.Parse(text);
            var x = new Symbol(variableName);
            if (positive)
            {
                foreach (var symbol in expr.FreeSymbols.Where(s => s.DeclaredSort is not FunctionSort)) context = context.Assume(Expr.Parse(symbol.Name + " > 0"));
            }

            return operation switch
            {
                AlgebraOperation.Simplify => Cas.Simplify(expr, context, budget: budget),
                AlgebraOperation.Expand => Cas.Expand(expr, context, budget),
                AlgebraOperation.Factor => Cas.Factor(expr, context, budget),
                AlgebraOperation.Collect => Cas.Collect(expr, x, context, budget),
                AlgebraOperation.Together => Cas.Together(expr, context, budget),
                AlgebraOperation.Apart => Cas.Apart(expr, x, context, budget),
                AlgebraOperation.Cancel => Cas.Cancel(expr, context, budget),
                AlgebraOperation.Rationalize => Cas.Rationalize(expr, context, budget),
                AlgebraOperation.RadicalSimplify => Cas.RadicalSimplify(expr, context, budget),
                AlgebraOperation.CompleteSquare => Cas.CompleteSquare(expr, x, context, budget),
                AlgebraOperation.PowerSimplify => Cas.PowerSimplify(expr, context, budget),
                AlgebraOperation.LogCombine => Cas.LogCombine(expr, context, budget),
                AlgebraOperation.LogExpand => Cas.LogExpand(expr, context, budget),
                AlgebraOperation.TrigSimplify => Cas.TrigSimplify(expr, context, budget),
                AlgebraOperation.TrigExpand => Cas.TrigExpand(expr, context, budget),
                AlgebraOperation.TrigReduce => Cas.TrigReduce(expr, context, budget),
                AlgebraOperation.TrigToExp => Cas.TrigToExp(expr, context, budget),
                _ => Cas.ExpToTrig(expr, context, budget),
            };
        });
        if (!token.IsCancellationRequested) Result = _presenter.PresentExpr(outcome, operation.ToString());
    });

    private static (string Note, IReadOnlyList<Sample> Samples) Describe(AlgebraOperation operation) => operation switch
    {
        AlgebraOperation.Simplify => ("Rewrites with the laws of the catalog until nothing applies; a cancellation that is only valid where a denominator is non-zero comes back with that proviso.",
            [new("Pythagoras and cancel", "sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)"), new("cancel", "(x^2 - 1)/(x - 1)"), new("collect terms", "2x + 3x - x"), new("powers", "x^2*x^3/x")]),
        AlgebraOperation.Expand => ("Multiplies out products and powers of sums.",
            [new("cube", "(x + 1)^3"), new("product", "(x - 2)(x + 5)"), new("two variables", "(a + b)^4")]),
        AlgebraOperation.Factor => ("Factors over the rationals: common factors, differences of squares, the rational root theorem.",
            [new("cubic", "x^3 - 6x^2 + 11x - 6"), new("difference of squares", "x^2 - 9"), new("x^4 - 1", "x^4 - 1"), new("trinomial", "6x^2 + 5x - 4")]),
        AlgebraOperation.Collect => ("Groups the terms by powers of the variable.",
            [new("a x + b x + c", "a*x + b*x + c"), new("three terms", "x*y + x*z + 3x")]),
        AlgebraOperation.Together => ("Puts a sum of fractions over a common denominator.",
            [new("two fractions", "1/x + 1/(x + 1)"), new("difference", "1/(x - 1) - 1/(x + 1)")]),
        AlgebraOperation.Apart => ("Partial fractions in the variable.",
            [new("two poles", "(x + 3)/((x + 1)(x + 2))"), new("1/(x² − 1)", "1/(x^2 - 1)")]),
        AlgebraOperation.Cancel => ("Cancels common factors of numerator and denominator, with the proviso that they are non-zero.",
            [new("quadratics", "(x^2 - 4)/(x^2 - 5x + 6)"), new("x³ − x", "(x^3 - x)/(x^2 - 1)")]),
        AlgebraOperation.Rationalize => ("Removes radicals from a denominator.",
            [new("1/(1 + √2)", "1/(1 + sqrt(2))"), new("3/√5", "3/sqrt(5)")]),
        AlgebraOperation.RadicalSimplify => ("Simplifies square roots of numbers and products of radicals.",
            [new("√50", "sqrt(50)"), new("√12·√3", "sqrt(12)*sqrt(3)")]),
        AlgebraOperation.CompleteSquare => ("Writes a quadratic as a(x − h)² + k.",
            [new("x² + 4x + 7", "x^2 + 4x + 7"), new("2x² − 8x + 3", "2x^2 - 8x + 3")]),
        AlgebraOperation.PowerSimplify => ("Combines powers: a^m · a^n = a^(m+n).",
            [new("x²·x³/x", "x^2*x^3/x"), new("(x³)²", "(x^3)^2")]),
        AlgebraOperation.LogCombine => ("Combines logarithms. The laws need positive arguments, so state that with the switch below or in Settings.",
            [new("ln x + ln y", "ln(x) + ln(y)"), new("2 ln x − ln y", "2*ln(x) - ln(y)")]),
        AlgebraOperation.LogExpand => ("Expands the logarithm of a product, quotient or power; needs positive arguments.",
            [new("ln(x y)", "ln(x*y)"), new("ln(x³/y)", "ln(x^3/y)")]),
        AlgebraOperation.TrigSimplify => ("Applies the trigonometric identities that make an expression smaller.",
            [new("Pythagoras", "sin(x)^2 + cos(x)^2"), new("1 − sin²", "1 - sin(x)^2"), new("tan·cos", "tan(x)*cos(x)")]),
        AlgebraOperation.TrigExpand => ("Expands sums and multiple angles.",
            [new("sin 2x", "sin(2x)"), new("cos(a + b)", "cos(a + b)"), new("sin 3x", "sin(3x)")]),
        AlgebraOperation.TrigReduce => ("Reduces powers and products of sines and cosines to multiple angles.",
            [new("sin²x", "sin(x)^2"), new("sin x cos x", "sin(x)*cos(x)")]),
        AlgebraOperation.TrigToExp => ("Writes trigonometric functions with complex exponentials (Euler).",
            [new("cos x", "cos(x)"), new("sin x", "sin(x)")]),
        _ => ("Writes complex exponentials as trigonometric functions.",
            [new("e^(ix)", "exp(I*x)"), new("e^(2ix)", "exp(2*I*x)")]),
    };
}
