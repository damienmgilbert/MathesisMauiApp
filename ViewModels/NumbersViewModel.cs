using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Numbers;
using Mathesis.Validation;
using MathesisMauiApp.Models;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// Numbers typed into a form, validated by the three numeric attributes. Every property below carries the attribute that documents it;
/// the page binds them like any other validated model, and <c>ObservableValidator</c> collects the <see cref="MathValidationResult"/>s.
/// </summary>
public sealed partial class NumbersViewModel : PageViewModel
{
    public NumbersViewModel()
    {
        Quantity = "12";
        Ratio = "3/4";
        Weight = "0.125";
        Probability = "0.3";
        Offset = "-99.5";
        Divisor = "-7/3";
        TypedValues = BuildTypedValues();
    }

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("0,5", "0,5"),
        new("1 000", "1 000"),
        new("1/0", "1/0"),
        new("3/2", "3/2"),
        new("abc", "abc"),
        new("1e999999999", "1e999999999"),
        new("−0.25 (U+2212)", "−0.25"),
        new("0.(3)", "0.(3)"),
        new("1.5e-3", "1.5e-3"),
        new("1/3", "1/3"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Quantity")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    public partial string? Quantity { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Ratio")]
    [RationalNumber(AllowDecimals = false)]
    public partial string? Ratio { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Weight")]
    [RationalNumber(AllowFractions = false)]
    public partial string? Weight { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Probability")]
    [ExactRange("0", "1")]
    public partial string? Probability { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Offset")]
    [ExactRange("-100", "100", MinimumIsExclusive = true)]
    public partial string? Offset { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Divisor")]
    [Required]
    [NonZero]
    public partial string? Divisor { get; set; }

    /// <summary>What the library read the Probability field as; empty while it is not a number.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Reading { get; set; }

    /// <summary>The same range checked against typed values: a <see cref="double"/> that is not 0.3 exactly, the text "0.3" and a <see cref="decimal"/>.</summary>
    public IReadOnlyList<VerdictRow> TypedValues { get; }

    [RelayCommand]
    private void UseSample(string text) => Probability = text;

    partial void OnProbabilityChanged(string? value) => Reading = Read(value);

    private static IReadOnlyList<Fact> Read(string? text)
    {
        var trimmed = text?.Trim().Replace('−', '-');
        if (string.IsNullOrEmpty(trimmed) || !BigRational.TryParse(trimmed, CultureInfo.InvariantCulture, out var value)) return [];
        var binary = value.ToDouble();
        return
        [
            new("Exact value", value.ToString(), "BigRational, in lowest terms"),
            new("Numerator / denominator", $"{value.Numerator} / {value.Denominator}"),
            new("Decimal expansion", value.ToDecimalString(30), "ToDecimalString(30): exact digits, never a binary approximation"),
            new("As a double", binary.ToString("G17", CultureInfo.InvariantCulture), "what a double stores for it, printed with 17 digits"),
            new("Whole number?", value.IsInteger ? "yes" : "no"),
        ];
    }

    private static IReadOnlyList<VerdictRow> BuildTypedValues()
    {
        var range = new ExactRangeAttribute("0", "3/10");
        VerdictRow Row(string input, string kind, object value)
        {
            var failure = range.Check(value, "Total");
            return new VerdictRow(input, kind, failure is null ? "valid" : $"{failure.Code}: {failure.ErrorMessage}", failure is null);
        }

        return
        [
            Row("0.1 + 0.2", "double", 0.1 + 0.2),
            Row("0.3", "double", 0.3),
            Row("0.3", "decimal", 0.3m),
            Row("\"0.3\"", "string", "0.3"),
            Row("3/10", "BigRational", BigRational.Create(3, 10)),
            Row("\"1e-100000\"", "string", "1e-100000"),
            Row("\"3/10\"", "string", "3/10"),
            Row("\"0.30000000000000001\"", "string", "0.30000000000000001"),
        ];
    }
}
