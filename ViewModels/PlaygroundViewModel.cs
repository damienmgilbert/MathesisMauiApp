using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum AttributeKind
{
    RationalNumber,
    ExactRange,
    NonZero,
    MathExpression,
    MathEquation,
    PolynomialExpression,
    MathMatrix,
}

/// <summary>The .NET type a value is handed to the attribute as. Text is what a form delivers; the others show the typed values some attributes accept.</summary>
public enum ValueKind
{
    Text,
    Double,
    Decimal,
    Int64,
    BigRational,
    Expr,
}

/// <summary>
/// The attributes built at run time. Pick one, set its options, type a value and see the verdict, the exact C# that declares the same
/// attribute, and the configuration errors that a bad combination of options produces. Nothing here is special: the attributes are the
/// public API, and <c>Check</c> is the entry point that works without a <c>ValidationContext</c>.
/// </summary>
public sealed partial class PlaygroundViewModel : PageViewModel
{
    private static readonly string[] OutputProperties =
    [
        nameof(Generated), nameof(ConfigurationError), nameof(Verdict), nameof(Facts), nameof(Misuse), nameof(Note), nameof(ShowVerdict),
        nameof(IsBusy), nameof(Result), nameof(HasErrors),
    ];

    private bool _updating;

    public PlaygroundViewModel()
    {
        _updating = true;
        Kind = AttributeKind.ExactRange;
        ValueKind = ValueKind.Text;
        Input = "0.3";
        MaxLengthText = "1000";
        Minimum = "0";
        Maximum = "3/10";
        AllowFractions = true;
        AllowDecimals = true;
        Shape = ExpressionShape.Any;
        VariablesText = "x";
        SingleLetterVariables = true;
        PolynomialVariable = "x";
        MaxDegreeText = "3";
        RowsText = "2";
        ColumnsText = "2";
        NumericEntries = true;
        MaxDimensionText = "10";
        _updating = false;
        Recompute();
    }

    public IReadOnlyList<AttributeKind> Kinds { get; } = Enum.GetValues<AttributeKind>();

    public IReadOnlyList<InputFormat> Formats { get; } = Enum.GetValues<InputFormat>();

    public IReadOnlyList<ExpressionShape> Shapes { get; } = Enum.GetValues<ExpressionShape>();

    public IReadOnlyList<Sample> Presets { get; } =
    [
        new("0.1 + 0.2 ≤ 3/10?", "0"),
        new("No trigonometry", "1"),
        new("Cubic at most", "2"),
        new("2×2 matrix", "3"),
        new("LaTeX equation", "4"),
        new("Bad configuration", "5"),
        new("Wrong type", "6"),
        new("Custom message", "7"),
    ];

    // ----- what is validated -----

    [ObservableProperty]
    public partial AttributeKind Kind { get; set; }

    [ObservableProperty]
    public partial string? Input { get; set; }

    [ObservableProperty]
    public partial ValueKind ValueKind { get; set; }

    /// <summary>Every kind is offered for every attribute: handing an attribute a type it does not support is API misuse, and seeing the exception is part of the lesson.</summary>
    public IReadOnlyList<ValueKind> ValueKinds { get; } = Enum.GetValues<ValueKind>();

    [ObservableProperty]
    public partial string? MaxLengthText { get; set; }

    [ObservableProperty]
    public partial string? CustomMessage { get; set; }

    // ----- number options -----

    [ObservableProperty]
    public partial bool IntegerOnly { get; set; }

    [ObservableProperty]
    public partial bool AllowFractions { get; set; }

    [ObservableProperty]
    public partial bool AllowDecimals { get; set; }

    [ObservableProperty]
    public partial string? Minimum { get; set; }

    [ObservableProperty]
    public partial string? Maximum { get; set; }

    [ObservableProperty]
    public partial bool MinimumIsExclusive { get; set; }

    [ObservableProperty]
    public partial bool MaximumIsExclusive { get; set; }

    // ----- expression options -----

    [ObservableProperty]
    public partial InputFormat Format { get; set; }

    [ObservableProperty]
    public partial ExpressionShape Shape { get; set; }

    [ObservableProperty]
    public partial string? VariablesText { get; set; }

    [ObservableProperty]
    public partial string? RequiredVariablesText { get; set; }

    [ObservableProperty]
    public partial bool DisallowTrig { get; set; }

    [ObservableProperty]
    public partial bool DisallowHyperbolic { get; set; }

    [ObservableProperty]
    public partial bool DisallowExpLog { get; set; }

    [ObservableProperty]
    public partial bool DisallowCalculus { get; set; }

    [ObservableProperty]
    public partial bool DisallowLinearAlgebra { get; set; }

    [ObservableProperty]
    public partial bool DisallowSet { get; set; }

    [ObservableProperty]
    public partial bool WarningsAreErrors { get; set; }

    [ObservableProperty]
    public partial bool CheckSorts { get; set; }

    [ObservableProperty]
    public partial bool SingleLetterVariables { get; set; }

    [ObservableProperty]
    public partial bool LogMeansNatural { get; set; }

    // ----- polynomial and matrix options -----

    [ObservableProperty]
    public partial string? PolynomialVariable { get; set; }

    [ObservableProperty]
    public partial string? MaxDegreeText { get; set; }

    [ObservableProperty]
    public partial string? RowsText { get; set; }

    [ObservableProperty]
    public partial string? ColumnsText { get; set; }

    [ObservableProperty]
    public partial bool Square { get; set; }

    [ObservableProperty]
    public partial bool NumericEntries { get; set; }

    [ObservableProperty]
    public partial string? MaxDimensionText { get; set; }

    // ----- which options apply -----

    public bool IsRational => Kind == AttributeKind.RationalNumber;

    public bool IsRange => Kind == AttributeKind.ExactRange;

    public bool IsExpression => Kind is AttributeKind.MathExpression or AttributeKind.MathEquation;

    public bool IsMathExpression => Kind == AttributeKind.MathExpression;

    public bool IsPolynomial => Kind == AttributeKind.PolynomialExpression;

    public bool IsMatrix => Kind == AttributeKind.MathMatrix;

    public bool HasFormat => Kind is AttributeKind.MathExpression or AttributeKind.MathEquation or AttributeKind.PolynomialExpression or AttributeKind.MathMatrix;

    // ----- the verdict -----

    /// <summary>The attribute as the C# that declares it.</summary>
    [ObservableProperty]
    public partial string? Generated { get; set; }

    /// <summary>What <c>GetConfigurationError()</c> returns, or <c>null</c> when the options are valid.</summary>
    [ObservableProperty]
    public partial string? ConfigurationError { get; set; }

    [ObservableProperty]
    public partial ValidationResult? Verdict { get; set; }

    /// <summary>False while the options are misconfigured or the value has the wrong type: there is no verdict to show then, and no "valid" mark either.</summary>
    [ObservableProperty]
    public partial bool ShowVerdict { get; set; }

    /// <summary>The message of the <see cref="InvalidOperationException"/> that API misuse (a type the attribute cannot check) throws.</summary>
    [ObservableProperty]
    public partial string? Misuse { get; set; }

    /// <summary>What was done with the input before it reached the attribute, when that is not obvious.</summary>
    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Facts { get; set; }

    [RelayCommand]
    private void ApplyPreset(string index)
    {
        _updating = true;
        try
        {
            Reset();
            switch (index)
            {
                case "0":
                    Kind = AttributeKind.ExactRange;
                    Minimum = "0";
                    Maximum = "3/10";
                    ValueKind = ValueKind.Double;
                    Input = "0.30000000000000004";
                    break;
                case "1":
                    Kind = AttributeKind.MathExpression;
                    VariablesText = "x";
                    DisallowTrig = true;
                    DisallowHyperbolic = true;
                    Input = "sin(x) + 1";
                    break;
                case "2":
                    Kind = AttributeKind.PolynomialExpression;
                    MaxDegreeText = "3";
                    Input = "(x + 1)^4 - x^4";
                    break;
                case "3":
                    Kind = AttributeKind.MathMatrix;
                    RowsText = "2";
                    ColumnsText = "2";
                    Input = "[[1, -1/2], [0.25, 3]]";
                    break;
                case "4":
                    Kind = AttributeKind.MathEquation;
                    Format = InputFormat.Latex;
                    VariablesText = "x";
                    Input = @"\frac{x^{2} - 1}{x + 1} = 0";
                    break;
                case "5":
                    Kind = AttributeKind.ExactRange;
                    Minimum = "5";
                    Maximum = "1";
                    Input = "3";
                    break;
                case "6":
                    Kind = AttributeKind.MathExpression;
                    VariablesText = string.Empty;
                    ValueKind = ValueKind.Double;
                    Input = "1.5";
                    break;
                case "7":
                    Kind = AttributeKind.ExactRange;
                    Minimum = "0";
                    Maximum = "1";
                    CustomMessage = "{0} must lie between {2} and {3}, but {1} is the range.";
                    Input = "5";
                    break;
            }
        }
        finally
        {
            _updating = false;
        }

        RaiseKindFlags();
        Recompute();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_updating || Array.IndexOf(OutputProperties, e.PropertyName) >= 0) return;
        if (e.PropertyName == nameof(Kind)) RaiseKindFlags();

        Recompute();
    }

    private void RaiseKindFlags()
    {
        OnPropertyChanged(nameof(IsRational));
        OnPropertyChanged(nameof(IsRange));
        OnPropertyChanged(nameof(IsExpression));
        OnPropertyChanged(nameof(IsMathExpression));
        OnPropertyChanged(nameof(IsPolynomial));
        OnPropertyChanged(nameof(IsMatrix));
        OnPropertyChanged(nameof(HasFormat));
    }

    private void Reset()
    {
        Kind = AttributeKind.ExactRange;
        ValueKind = ValueKind.Text;
        MaxLengthText = "1000";
        CustomMessage = null;
        IntegerOnly = false;
        AllowFractions = true;
        AllowDecimals = true;
        Minimum = "0";
        Maximum = "1";
        MinimumIsExclusive = MaximumIsExclusive = false;
        Format = InputFormat.Text;
        Shape = ExpressionShape.Any;
        VariablesText = null;
        RequiredVariablesText = null;
        DisallowTrig = DisallowHyperbolic = DisallowExpLog = DisallowCalculus = DisallowLinearAlgebra = DisallowSet = false;
        WarningsAreErrors = CheckSorts = LogMeansNatural = false;
        SingleLetterVariables = true;
        PolynomialVariable = "x";
        MaxDegreeText = "20";
        RowsText = "0";
        ColumnsText = "0";
        Square = false;
        NumericEntries = true;
        MaxDimensionText = "10";
    }

    private void Recompute()
    {
        var attribute = Build(out var declaration);
        Generated = declaration;
        ConfigurationError = attribute.GetConfigurationError();
        Verdict = null;
        Misuse = null;
        Note = null;
        Facts = null;
        ShowVerdict = false;
        if (ConfigurationError is not null) return;

        var understood = TryMakeValue(out var value, out var problem);
        Note = problem;
        if (!understood) return;

        try
        {
            var failure = attribute.Check(value, "Value", "Input");
            Verdict = failure;
            var facts = new List<Fact>
            {
                new("Check(value)", failure is null ? "null: the value is valid" : "a MathValidationResult"),
                new("IsValid(value)", attribute.IsValid(value) ? "true" : "false", "the standard ValidationAttribute entry point agrees"),
            };
            if (failure is not null)
            {
                facts.Add(new("Code", failure.Code.ToString(), "stable: switch on it, store it, map it to your own text"));
                facts.Add(new("ErrorMessage", failure.ErrorMessage ?? string.Empty));
                facts.Add(new("Span", failure.Span is { } span ? $"{span} (start {span.Start}, length {span.Length})" : "null", "the characters of the text the failure concerns"));
                facts.Add(new("Suggestion", failure.Suggestion ?? "null"));
                facts.Add(new("MemberNames", string.Join(", ", failure.MemberNames)));
            }

            Facts = facts;
            ShowVerdict = true;
        }
        catch (InvalidOperationException ex)
        {
            Misuse = ex.Message;
        }
    }

    /// <summary>Turns the input into the value handed to the attribute: the text itself, or the text read as the chosen .NET type.</summary>
    private bool TryMakeValue(out object? value, out string? problem)
    {
        value = Input;
        problem = null;
        var text = Input?.Trim() ?? string.Empty;
        switch (ValueKind)
        {
            case ValueKind.Text:
                return true;
            case ValueKind.Double when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d):
                value = d;
                problem = $"Handed over as the double {d:R}.";
                return true;
            case ValueKind.Decimal when decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var m):
                value = m;
                problem = $"Handed over as the decimal {m}.";
                return true;
            case ValueKind.Int64 when long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l):
                value = l;
                problem = $"Handed over as the long {l}.";
                return true;
            case ValueKind.BigRational when NumberInputs.TryRead(text, out var r):
                value = r;
                problem = $"Handed over as the BigRational {r}.";
                return true;
            case ValueKind.Expr when Expr.TryParse(text).Expr is { } expr:
                value = expr;
                problem = "Handed over as a parsed Expr: the attribute skips the parse and the parser warnings.";
                return true;
            default:
                value = null;
                problem = $"The input cannot be read as a {ValueKind}, so there is nothing to check yet.";
                return false;
        }
    }

    /// <summary>Builds the attribute from the options, and the C# declaration of it, written the way a developer would.</summary>
    private MathValidationAttribute Build(out string declaration)
    {
        var maxLength = int.TryParse(MaxLengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ml) ? ml : 1000;
        var message = string.IsNullOrWhiteSpace(CustomMessage) ? null : CustomMessage;
        var options = new List<string>();
        void Add(bool condition, string text) { if (condition) options.Add(text); }
        static string Q(string? text) => "\"" + (text ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        static string[]? Names(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim() == "-" ? [] : text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        static string ArrayText(string[] names) => "[" + string.Join(", ", names.Select(Q)) + "]";

        Add(maxLength != 1000, $"MaxLength = {maxLength}");
        Add(message is not null, $"ErrorMessage = {Q(message)}");

        MathValidationAttribute attribute;
        string name;
        string? positional = null;
        switch (Kind)
        {
            case AttributeKind.RationalNumber:
                Add(IntegerOnly, "IntegerOnly = true");
                Add(!AllowFractions, "AllowFractions = false");
                Add(!AllowDecimals, "AllowDecimals = false");
                attribute = new RationalNumberAttribute { IntegerOnly = IntegerOnly, AllowFractions = AllowFractions, AllowDecimals = AllowDecimals, MaxLength = maxLength };
                name = "RationalNumber";
                break;
            case AttributeKind.ExactRange:
                Add(MinimumIsExclusive, "MinimumIsExclusive = true");
                Add(MaximumIsExclusive, "MaximumIsExclusive = true");
                attribute = new ExactRangeAttribute(Minimum, Maximum) { MinimumIsExclusive = MinimumIsExclusive, MaximumIsExclusive = MaximumIsExclusive, MaxLength = maxLength };
                name = "ExactRange";
                positional = $"{(Minimum is null ? "null" : Q(Minimum))}, {(Maximum is null ? "null" : Q(Maximum))}";
                break;
            case AttributeKind.NonZero:
                attribute = new NonZeroAttribute { MaxLength = maxLength };
                name = "NonZero";
                break;
            case AttributeKind.MathExpression:
            case AttributeKind.MathEquation:
            {
                var variables = Names(VariablesText);
                var required = Names(RequiredVariablesText);
                var families = OperatorFamily.None;
                if (DisallowTrig) families |= OperatorFamily.Trig;
                if (DisallowHyperbolic) families |= OperatorFamily.Hyperbolic;
                if (DisallowExpLog) families |= OperatorFamily.ExpLog;
                if (DisallowCalculus) families |= OperatorFamily.Calculus;
                if (DisallowLinearAlgebra) families |= OperatorFamily.LinearAlgebra;
                if (DisallowSet) families |= OperatorFamily.Set;

                Add(Format != InputFormat.Text, $"Format = InputFormat.{Format}");
                Add(Kind == AttributeKind.MathExpression && Shape != ExpressionShape.Any, $"Shape = ExpressionShape.{Shape}");
                Add(variables is not null, $"Variables = {ArrayText(variables ?? [])}");
                Add(required is not null, $"RequiredVariables = {ArrayText(required ?? [])}");
                Add(families != OperatorFamily.None, "DisallowedFamilies = " + string.Join(" | ", families.ToString().Split(", ").Select(f => "OperatorFamily." + f)));
                Add(WarningsAreErrors, "WarningsAreErrors = true");
                Add(CheckSorts, "CheckSorts = true");
                Add(!SingleLetterVariables, "SingleLetterVariables = false");
                Add(LogMeansNatural, "LogMeansNatural = true");

                if (Kind == AttributeKind.MathEquation)
                {
                    attribute = new MathEquationAttribute { Format = Format, Variables = variables, RequiredVariables = required, DisallowedFamilies = families, WarningsAreErrors = WarningsAreErrors, CheckSorts = CheckSorts, SingleLetterVariables = SingleLetterVariables, LogMeansNatural = LogMeansNatural, MaxLength = maxLength };
                    name = "MathEquation";
                }
                else
                {
                    attribute = new MathExpressionAttribute { Format = Format, Shape = Shape, Variables = variables, RequiredVariables = required, DisallowedFamilies = families, WarningsAreErrors = WarningsAreErrors, CheckSorts = CheckSorts, SingleLetterVariables = SingleLetterVariables, LogMeansNatural = LogMeansNatural, MaxLength = maxLength };
                    name = "MathExpression";
                }

                break;
            }
            case AttributeKind.PolynomialExpression:
            {
                var degree = int.TryParse(MaxDegreeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 20;
                Add(degree != 20, $"MaxDegree = {degree}");
                Add(Format != InputFormat.Text, $"Format = InputFormat.{Format}");
                attribute = new PolynomialExpressionAttribute(PolynomialVariable ?? string.Empty) { MaxDegree = degree, Format = Format, MaxLength = maxLength };
                name = "PolynomialExpression";
                positional = Q(PolynomialVariable);
                break;
            }
            default:
            {
                var rows = int.TryParse(RowsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : 0;
                var columns = int.TryParse(ColumnsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? c : 0;
                var dimension = int.TryParse(MaxDimensionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var md) ? md : 10;
                Add(rows != 0, $"Rows = {rows}");
                Add(columns != 0, $"Columns = {columns}");
                Add(Square, "Square = true");
                Add(!NumericEntries, "NumericEntries = false");
                Add(dimension != 10, $"MaxDimension = {dimension}");
                Add(Format != InputFormat.Text, $"Format = InputFormat.{Format}");
                attribute = new MathMatrixAttribute { Rows = rows, Columns = columns, Square = Square, NumericEntries = NumericEntries, MaxDimension = dimension, Format = Format, MaxLength = maxLength };
                name = "MathMatrix";
                break;
            }
        }

        // Assigning null to ErrorMessage is itself a configuration error in the base class, so the property is only touched when there is a message.
        if (message is not null) attribute.ErrorMessage = message;

        var builder = new StringBuilder("[").Append(name);
        var parts = new List<string>();
        if (positional is not null) parts.Add(positional);
        parts.AddRange(options);
        if (parts.Count > 0) builder.Append('(').Append(string.Join(", ", parts)).Append(')');
        declaration = builder.Append(']').ToString();
        return attribute;
    }
}
