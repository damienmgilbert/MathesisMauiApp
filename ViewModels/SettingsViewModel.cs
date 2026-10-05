using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Explanation;
using Mathesis.Knowledge;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>One assumption in the list, with the command that removes it.</summary>
public sealed record AssumptionItem(string Text, ICommand Remove);

/// <summary>
/// The settings every page shares: the number field, the assumptions, the curriculum level, the budget and how steps are explained. Even the
/// budget limits and the assumptions are validated with the Mathesis attributes: an assumption must be a relation, a limit a whole number in range.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private const string NoLevel = "No restriction";

    private readonly ISettingsService _settings;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        Field = settings.Field;
        SelectedLevel = settings.Level?.ToString() ?? NoLevel;
        Format = settings.ExplanationFormat;
        Detail = settings.ExplanationVerbosity;
        Theme = settings.Theme;
        MaxSteps = settings.MaxSteps.ToString(CultureInfo.InvariantCulture);
        MaxSeconds = settings.MaxSeconds.ToString(CultureInfo.InvariantCulture);
        Question = "x > -1";
        foreach (var text in settings.Assumptions) Assumptions.Add(MakeItem(text));
        Ask();
    }

    public IReadOnlyList<NumberField> Fields { get; } = Enum.GetValues<NumberField>();

    public IReadOnlyList<string> Levels { get; } = [NoLevel, .. Enum.GetNames<CurriculumLevel>()];

    public IReadOnlyList<ExplanationFormat> Formats { get; } = Enum.GetValues<ExplanationFormat>();

    public IReadOnlyList<Verbosity> Details { get; } = Enum.GetValues<Verbosity>();

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    public ObservableCollection<AssumptionItem> Assumptions { get; } = [];

    public IReadOnlyList<Fact> About { get; } =
    [
        new("Mathesis", typeof(Cas).Assembly.GetName().Version?.ToString() ?? "unknown", "the CAS façade and the engines"),
        new("Mathesis.Validation", typeof(MathValidationAttribute).Assembly.GetName().Version?.ToString() ?? "unknown", "the seven DataAnnotations attributes"),
        new("Catalog", "loaded on first use", "the embedded .mlaw files of Mathesis.Knowledge"),
        new(".NET", RuntimeInformation.FrameworkDescription),
        new("Platform", $"{DeviceInfo.Platform} {DeviceInfo.VersionString}, {DeviceInfo.Idiom}"),
    ];

    [ObservableProperty]
    public partial NumberField Field { get; set; }

    [ObservableProperty]
    public partial string SelectedLevel { get; set; }

    [ObservableProperty]
    public partial ExplanationFormat Format { get; set; }

    [ObservableProperty]
    public partial Verbosity Detail { get; set; }

    [ObservableProperty]
    public partial AppTheme Theme { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Step limit")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "10000000")]
    public partial string? MaxSteps { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Time limit")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "600")]
    public partial string? MaxSeconds { get; set; }

    /// <summary>A fact the algebra system may rely on: one relation such as <c>x &gt; 0</c> or <c>y != 0</c>.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Assumption")]
    [MathExpression(Shape = ExpressionShape.Inequality, CheckSorts = true)]
    public partial string? NewAssumption { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Question")]
    [MathExpression(Shape = ExpressionShape.Inequality)]
    public partial string? Question { get; set; }

    [ObservableProperty]
    public partial string? Answer { get; set; }

    partial void OnFieldChanged(NumberField value) { _settings.Field = value; Ask(); }

    partial void OnSelectedLevelChanged(string value) => _settings.Level = Enum.TryParse<CurriculumLevel>(value, out var level) ? level : null;

    partial void OnFormatChanged(ExplanationFormat value) => _settings.ExplanationFormat = value;

    partial void OnDetailChanged(Verbosity value) => _settings.ExplanationVerbosity = value;

    partial void OnThemeChanged(AppTheme value) => _settings.Theme = value;

    // Validation runs after the change callback, so a limit is stored only when the text is a valid number in range.
    partial void OnMaxStepsChanged(string? value)
    {
        if (new ExactRangeAttribute("1", "10000000").Check(value) is null && NumberInputs.TryRead(value, out var n) && n.IsInteger) _settings.MaxSteps = (int)n.Numerator;
    }

    partial void OnMaxSecondsChanged(string? value)
    {
        if (new ExactRangeAttribute("1", "600").Check(value) is null && NumberInputs.TryRead(value, out var n) && n.IsInteger) _settings.MaxSeconds = (int)n.Numerator;
    }

    partial void OnQuestionChanged(string? value) => Ask();

    [RelayCommand]
    private void AddAssumption()
    {
        ValidateProperty(NewAssumption, nameof(NewAssumption));
        if (HasErrorsOn(nameof(NewAssumption)) || string.IsNullOrWhiteSpace(NewAssumption)) return;
        var text = NewAssumption!.Trim();
        if (Assumptions.Any(a => a.Text == text)) return;
        Assumptions.Add(MakeItem(text));
        NewAssumption = string.Empty;
        Store();
        Ask();
    }

    [RelayCommand]
    private void Reset()
    {
        Field = NumberField.Real;
        SelectedLevel = NoLevel;
        Format = ExplanationFormat.Text;
        Detail = Verbosity.Standard;
        Theme = AppTheme.Unspecified;
        MaxSteps = "100000";
        MaxSeconds = "10";
        Assumptions.Clear();
        Store();
        Ask();
    }

    private bool HasErrorsOn(string property) => GetErrors(property).Any();

    private AssumptionItem MakeItem(string text)
    {
        AssumptionItem? item = null;
        item = new AssumptionItem(text, new RelayCommand(() =>
        {
            Assumptions.Remove(item!);
            Store();
            Ask();
        }));
        return item;
    }

    private void Store() => _settings.Assumptions = [.. Assumptions.Select(a => a.Text)];

    /// <summary>Asks the assumption set whether a relation holds: true, false, or unknown when the assumptions do not decide it.</summary>
    private void Ask()
    {
        if (string.IsNullOrWhiteSpace(Question) || Expr.TryParse(Question).Expr is not { } question)
        {
            Answer = null;
            return;
        }

        var truth = _settings.CreateContext().Ask(question);
        Answer = truth switch
        {
            Truth.True => "True: the assumptions imply it",
            Truth.False => "False: the assumptions contradict it",
            _ => "Unknown: the assumptions do not decide it",
        };
    }
}
