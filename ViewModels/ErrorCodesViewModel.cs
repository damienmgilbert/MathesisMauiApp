using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Mathesis.Symbolics;
using Mathesis.Validation;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// One <see cref="MathValidationCode"/> with an attribute and an input that raise it. The input can be edited: the verdict is recomputed, and
/// the card says whether the edited input still raises the code it is named after.
/// </summary>
public sealed partial class CodeCase : ObservableObject
{
    private readonly MathValidationAttribute _rule;

    public CodeCase(MathValidationCode code, string declaration, MathValidationAttribute rule, string input)
    {
        Code = code;
        Declaration = declaration;
        _rule = rule;
        Input = input;
    }

    public MathValidationCode Code { get; }

    public string Name => Code.ToString();

    /// <summary>The attribute as the C# that declares it.</summary>
    public string Declaration { get; }

    [ObservableProperty]
    public partial string? Input { get; set; }

    [ObservableProperty]
    public partial ValidationResult? Result { get; set; }

    [ObservableProperty]
    public partial string? Outcome { get; set; }

    [ObservableProperty]
    public partial bool StillRaisesCode { get; set; }

    partial void OnInputChanged(string? value)
    {
        var result = _rule.Check(value, "Value", "Input");
        Result = result;
        StillRaisesCode = result?.Code == Code;
        Outcome = result is null ? "now valid" : result.Code == Code ? "raises " + Code : "now raises " + result.Code;
    }
}

public sealed record CodeGroup(string Title, string Summary, IReadOnlyList<CodeCase> Cases);

/// <summary>Every validation code of Mathesis.Validation, grouped by the attributes that raise it.</summary>
public sealed class ErrorCodesViewModel : PageViewModel
{
    public ErrorCodesViewModel()
    {
        Groups =
        [
            new("Any attribute", "Text longer than MaxLength is refused before it is parsed, so hostile input is bounded.",
            [
                new(MathValidationCode.TooLong, "[RationalNumber(MaxLength = 5)]", new RationalNumberAttribute { MaxLength = 5 }, "1234567"),
            ]),
            new("Numbers", "RationalNumber, ExactRange and NonZero read text as an exact rational number.",
            [
                new(MathValidationCode.NotANumber, "[RationalNumber]", new RationalNumberAttribute(), "0,5"),
                new(MathValidationCode.NotAnInteger, "[RationalNumber(IntegerOnly = true)]", new RationalNumberAttribute { IntegerOnly = true }, "1/2"),
                new(MathValidationCode.FractionNotAllowed, "[RationalNumber(AllowFractions = false)]", new RationalNumberAttribute { AllowFractions = false }, "3/4"),
                new(MathValidationCode.DecimalNotAllowed, "[RationalNumber(AllowDecimals = false)]", new RationalNumberAttribute { AllowDecimals = false }, "0.75"),
                new(MathValidationCode.OutOfRange, "[ExactRange(\"0\", \"1\")]", new ExactRangeAttribute("0", "1"), "3/2"),
                new(MathValidationCode.Zero, "[NonZero]", new NonZeroAttribute(), "0.0"),
            ]),
            new("Expressions and equations", "MathExpression and MathEquation parse the text (never evaluate it) and inspect the tree.",
            [
                new(MathValidationCode.Syntax, "[MathExpression]", new MathExpressionAttribute(), "2x +"),
                new(MathValidationCode.Ambiguous, "[MathExpression(WarningsAreErrors = true)]", new MathExpressionAttribute { WarningsAreErrors = true }, "1/2x"),
                new(MathValidationCode.UnknownFunction, "[MathExpression(WarningsAreErrors = true)]", new MathExpressionAttribute { WarningsAreErrors = true }, "sqr(x)"),
                new(MathValidationCode.IllSorted, "[MathExpression(CheckSorts = true)]", new MathExpressionAttribute { CheckSorts = true }, "[[1, 2], [3, 4]] + 1"),
                new(MathValidationCode.WrongShape, "[MathEquation]", new MathEquationAttribute(), "x^2 - 4"),
                new(MathValidationCode.UnknownVariable, "[MathExpression(Variables = [\"x\"])]", new MathExpressionAttribute { Variables = ["x"] }, "x + y"),
                new(MathValidationCode.MissingVariable, "[MathExpression(RequiredVariables = [\"x\"])]", new MathExpressionAttribute { RequiredVariables = ["x"] }, "5 + 3"),
                new(MathValidationCode.DisallowedFunction, "[MathExpression(DisallowedFamilies = OperatorFamily.Trig)]", new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Trig }, "sin(x) + 1"),
            ]),
            new("Polynomials", "PolynomialExpression reads a polynomial in one variable with rational coefficients.",
            [
                new(MathValidationCode.NotAPolynomial, "[PolynomialExpression(\"x\")]", new PolynomialExpressionAttribute("x"), "1/x"),
                new(MathValidationCode.DegreeTooHigh, "[PolynomialExpression(\"x\", MaxDegree = 3)]", new PolynomialExpressionAttribute("x") { MaxDegree = 3 }, "x^7 - 1"),
            ]),
            new("Matrices", "MathMatrix reads a matrix literal and checks its size and entries.",
            [
                new(MathValidationCode.NotAMatrix, "[MathMatrix]", new MathMatrixAttribute(), "[1, 2]"),
                new(MathValidationCode.WrongDimensions, "[MathMatrix(Rows = 2, Columns = 2)]", new MathMatrixAttribute { Rows = 2, Columns = 2 }, "[[1, 2, 3], [4, 5, 6]]"),
                new(MathValidationCode.NotSquare, "[MathMatrix(Square = true)]", new MathMatrixAttribute { Square = true }, "[[1, 2, 3], [4, 5, 6]]"),
                new(MathValidationCode.NonNumericEntry, "[MathMatrix]", new MathMatrixAttribute(), "[[1, x], [2, 3]]"),
                new(MathValidationCode.DimensionTooLarge, "[MathMatrix(MaxDimension = 3)]", new MathMatrixAttribute { MaxDimension = 3 }, "[[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]"),
            ]),
        ];
        Total = Groups.Sum(g => g.Cases.Count);
        Defined = Enum.GetValues<MathValidationCode>().Length;
    }

    public IReadOnlyList<CodeGroup> Groups { get; }

    public int Total { get; }

    public int Defined { get; }

    public string Summary => (Total == Defined ? $"All {Total} codes of the enum are raised below." : $"{Total} of the {Defined} codes are raised below.") + " Edit any input to see which code it raises instead.";
}
