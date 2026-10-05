using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Symbolics.Serialization;
using Mathesis.Validation;
using MathesisMauiApp.Models;

namespace MathesisMauiApp.ViewModels;

/// <summary>Expressions, equations, inequalities and intervals, typed as text or LaTeX and checked for shape, variables and function families.</summary>
public sealed partial class ExpressionsViewModel : PageViewModel
{
    public ExpressionsViewModel()
    {
        Formula = "x^2 + 3x - 1";
        Equation = "x^2 - 5x + 6 = 0";
        Inequality = "(x - 1)/(x + 2) >= 0";
        Interval = "[0, oo)";
        Latex = @"\frac{x^{2} - 1}{x + 1}";
        Strict = "sqrt(x)/2";
        Words = "speed*time";
        Inspect = "(x + 1)^2/(x - 1) + sin(x)";
    }

    public IReadOnlyList<Sample> FormulaSamples { get; } =
    [
        new("sin(x)", "sin(x)"),
        new("x + y", "x + y"),
        new("2x +", "2x +"),
        new("(x + 1", "(x + 1"),
        new("x ** 2", "x ** 2"),
        new("9^9^9", "9^9^9"),
        new("5", "5"),
        new("x^2 = 4", "x^2 = 4"),
    ];

    public IReadOnlyList<Sample> InspectSamples { get; } =
    [
        new("equation", "x^2 - 4 = 0"),
        new("inequality", "1 < x"),
        new("interval", "]-oo, 3["),
        new("matrix", "[[1, 2], [3, 4]]"),
        new("set", "{1, 2, 3}"),
        new("integral", "integrate(x^2, x)"),
        new("sum", "sum(k, k, 1, 10)"),
        new("1/2x", "1/2x"),
    ];

    /// <summary>An expression in x alone that does not use trigonometry or hyperbolic functions.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Formula")]
    [Required]
    [MathExpression(Variables = ["x"], DisallowedFamilies = OperatorFamily.Trig | OperatorFamily.Hyperbolic)]
    public partial string? Formula { get; set; }

    /// <summary>One equation that mentions x.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Equation")]
    [Required]
    [MathEquation(RequiredVariables = ["x"])]
    public partial string? Equation { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Inequality")]
    [MathExpression(Shape = ExpressionShape.Inequality, Variables = ["x"])]
    public partial string? Inequality { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Interval")]
    [MathExpression(Shape = ExpressionShape.Interval)]
    public partial string? Interval { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "LaTeX formula")]
    [MathExpression(Format = InputFormat.Latex, Variables = ["x"])]
    public partial string? Latex { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Strict expression")]
    [MathExpression(WarningsAreErrors = true, CheckSorts = true)]
    public partial string? Strict { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Named quantities")]
    [MathExpression(SingleLetterVariables = false, Variables = ["speed", "time"], RequiredVariables = ["speed"])]
    public partial string? Words { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Anything")]
    [Required]
    [MathExpression]
    public partial string? Inspect { get; set; }

    /// <summary>What the library makes of the text in <see cref="Inspect"/>; empty while it does not parse.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Inspection { get; set; }

    [RelayCommand]
    private void UseFormulaSample(string text) => Formula = text;

    [RelayCommand]
    private void UseInspectSample(string text) => Inspect = text;

    partial void OnInspectChanged(string? value) => Inspection = Describe(value);

    private static IReadOnlyList<Fact> Describe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var parsed = Expr.TryParse(text);
        if (parsed.Expr is not { } expr) return [];

        var symbols = expr.FreeSymbols.Where(s => s.DeclaredSort is not FunctionSort).Select(s => s.Name).Order(StringComparer.Ordinal).ToArray();
        var json = ExprJson.Serialize(expr);
        var facts = new List<Fact>
        {
            new("Parsed as", TextPrinter.Print(expr), "linear input notation, printed back"),
            new("Presentation", TextPrinter.Print(expr, PrintOptions.Presentation), "descending powers and Unicode symbols"),
            new("LaTeX", expr.ToLatex(), "Expr.ToLatex()"),
            new("Kind", Classify(expr)),
            new("Free variables", symbols.Length == 0 ? "(none)" : string.Join(", ", symbols), "constants such as pi and e are not variables"),
            new("Operator families", expr.Families == OperatorFamily.None ? "(none)" : expr.Families.ToString()),
            new("Sort", expr.Sort.ToString(), "what SortChecker infers"),
            new("Size", $"{expr.LeafCount} leaves, depth {expr.Depth}"),
        };

        foreach (var warning in parsed.Warnings) facts.Add(new("Parser warning", warning.Message, $"{warning.Code} at {warning.Span}"));
        facts.Add(new("JSON", json.Length > 220 ? json[..220] + "…" : json, "ExprJson.Serialize, source-generated System.Text.Json"));
        return facts;
    }

    private static string Classify(Expr expr) => expr switch
    {
        IntervalLiteral => "an interval",
        MatrixLiteral m => $"a {m.Rows}×{m.Columns} matrix",
        SetLiteral => "a set",
        TupleLiteral => "a tuple",
        Apply { Operator.Id: "eq" } => "an equation",
        Apply { Operator.Id: "ne" or "lt" or "le" or "gt" or "ge" } => "an inequality",
        Apply { Operator.Family: OperatorFamily.Logic or OperatorFamily.Relation } => "a statement",
        Number or Float => "a number",
        Symbol => "a variable",
        _ => "an expression",
    };
}
