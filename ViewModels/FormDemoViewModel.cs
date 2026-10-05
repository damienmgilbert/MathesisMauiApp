using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// A whole model validated the way a real form would be: the seven Mathesis attributes next to the standard ones, two attributes on one
/// property, and a rule across properties in <see cref="IValidatableObject"/>. Per-property errors appear while typing; "Validate all"
/// checks everything at once, including the cross-property rule, and lists every failure with its stable code.
/// </summary>
public sealed partial class FormDemoViewModel : PageViewModel, IValidatableObject
{
    public FormDemoViewModel()
    {
        FillValid();
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Job name")]
    [Required]
    [StringLength(24, MinimumLength = 3)]
    public partial string? JobName { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Objective f(x)")]
    [Required]
    [MathExpression(Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Calculus)]
    public partial string? Objective { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Constraint")]
    [MathEquation(Variables = ["x"])]
    public partial string? Constraint { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Model polynomial")]
    [Required]
    [PolynomialExpression("x", MaxDegree = 5)]
    public partial string? ModelPolynomial { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Weights")]
    [MathMatrix(Square = true, MaxDimension = 4)]
    public partial string? Weights { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Lower bound")]
    [Required]
    [ExactRange("-100", "100")]
    public partial string? Lower { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Upper bound")]
    [Required]
    [ExactRange("-100", "100")]
    public partial string? Upper { get; set; }

    /// <summary>Two attributes on one property: a whole number, and between 1 and 1000. Text that is not a number fails both, which the page reports as "+1 more".</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Iterations")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "1000")]
    public partial string? Iterations { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Scale factor")]
    [Required]
    [NonZero]
    public partial string? Scale { get; set; }

    /// <summary>The failure of the rule across Lower and Upper, shown live under the two bounds.</summary>
    [ObservableProperty]
    public partial ValidationResult? CrossProblem { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial bool StatusIsGood { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<VerdictRow>? Problems { get; set; }

    partial void OnLowerChanged(string? value) => CrossProblem = Validate(new ValidationContext(this)).FirstOrDefault();

    partial void OnUpperChanged(string? value) => CrossProblem = Validate(new ValidationContext(this)).FirstOrDefault();

    /// <summary>The rule that involves two properties. Property attributes cannot express it, so it lives here, as for any model.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NumberInputs.TryRead(Lower, out var lower) && NumberInputs.TryRead(Upper, out var upper) && lower >= upper)
        {
            yield return new ValidationResult("The lower bound must be below the upper bound.", [nameof(Lower), nameof(Upper)]);
        }
    }

    [RelayCommand]
    private void ValidateAll()
    {
        ValidateAllProperties();
        var problems = GetErrors()
            .Select(e => new VerdictRow(string.Join(", ", e.MemberNames.DefaultIfEmpty("model")), e is MathValidationResult m ? m.Code.ToString() : "Standard attribute", e.ErrorMessage ?? "Invalid", false))
            .ToList();

        // The cross-property rule runs after the properties pass, as Validator.TryValidateObject does.
        if (problems.Count == 0)
        {
            problems.AddRange(Validate(new ValidationContext(this)).Select(e => new VerdictRow(string.Join(", ", e.MemberNames), "IValidatableObject", e.ErrorMessage ?? "Invalid", false)));
        }

        Problems = problems;
        StatusIsGood = problems.Count == 0;
        Status = problems.Count == 0
            ? "Every property passed and the cross-property rule holds. The model could be submitted."
            : $"{problems.Count} problem{(problems.Count == 1 ? string.Empty : "s")} to fix before this model can be submitted.";
    }

    [RelayCommand]
    private void FillValid()
    {
        JobName = "Cubic fit";
        Objective = "x^3 - 2x - 5";
        Constraint = "x^2 = 2";
        ModelPolynomial = "x^3 - 2x - 5";
        Weights = "[[1, 0], [0, 2]]";
        Lower = "-10";
        Upper = "10";
        Iterations = "100";
        Scale = "1/2";
        Problems = null;
        Status = null;
    }

    [RelayCommand]
    private void FillInvalid()
    {
        JobName = "x";
        Objective = "sin(x) + y";
        Constraint = "x^2 - 2";
        ModelPolynomial = "x^7 - 1";
        Weights = "[[1, 2, 3], [4, 5, 6]]";
        Lower = "5";
        Upper = "1000";
        Iterations = "abc";
        Scale = "0.0";
    }

    [RelayCommand]
    private void FillCrossProperty()
    {
        FillValid();
        Lower = "50";
        Upper = "-50";
    }

    [RelayCommand]
    private void Clear()
    {
        JobName = Objective = Constraint = ModelPolynomial = Weights = Lower = Upper = Iterations = Scale = null;
        ClearErrors();
        Problems = null;
        Status = null;
    }
}
