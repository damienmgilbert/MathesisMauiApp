using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Explanation;
using Mathesis.Knowledge;

namespace MathesisMauiApp.Models;

/// <summary>How an <see cref="Outcome{T}"/> ended, as the page shows it.</summary>
public enum OutcomeKind
{
    Success,
    Partial,
    Unevaluated,
    Failed,
}

/// <summary>A catalog law that one of the steps cited, with the command that opens its page.</summary>
public sealed record LawReference(string Id, string Name, string Level, ICommand Open);

/// <summary>
/// Everything a page shows about one <see cref="Outcome{T}"/>: the value, why there is none, the provisos, the verification status and the
/// derivation, which can be re-rendered in another format or verbosity without computing it again.
/// </summary>
public sealed partial class OutcomeDisplay : ObservableObject
{
    private readonly IDerivation? _derivation;
    private readonly CurriculumLevel? _level;

    public OutcomeDisplay(OutcomeKind kind, string heading, string value, string? latex, string? inputNotation, string? reason,
        Verification check, string? provisos, IDerivation? derivation, CurriculumLevel? level, IReadOnlyList<LawReference> laws,
        ExplanationFormat format, Verbosity verbosity, Action<string, string> copy)
    {
        Kind = kind;
        Heading = heading;
        Value = value;
        ValueLatex = latex;
        InputNotation = inputNotation;
        Reason = reason;
        Check = check;
        Provisos = provisos;
        Laws = laws;
        _derivation = derivation;
        _level = level;
        Format = format;
        Verbosity = verbosity;
        CopyValueCommand = new RelayCommand(() => copy(Value, "Result"));
        CopyInputCommand = new RelayCommand(() => copy(InputNotation ?? Value, "Result in input notation"));
        CopyLatexCommand = new RelayCommand(() => copy(ValueLatex ?? Value, "LaTeX"));
        CopyStepsCommand = new RelayCommand(() => copy(StepsText, "Steps"));
        Render();
    }

    public OutcomeKind Kind { get; }

    public string Heading { get; }

    public string Value { get; }

    public string? ValueLatex { get; }

    public string? InputNotation { get; }

    public string? Reason { get; }

    public Verification Check { get; }

    public string? Provisos { get; }

    public IReadOnlyList<LawReference> Laws { get; }

    public ICommand CopyValueCommand { get; }

    public ICommand CopyInputCommand { get; }

    public ICommand CopyLatexCommand { get; }

    public ICommand CopyStepsCommand { get; }

    public bool IsOk => Kind == OutcomeKind.Success;

    public bool IsWarning => Kind is OutcomeKind.Partial or OutcomeKind.Unevaluated;

    public bool IsError => Kind == OutcomeKind.Failed;

    public bool HasValue => !string.IsNullOrEmpty(Value);

    public bool HasLatex => !string.IsNullOrEmpty(ValueLatex);

    public bool HasReason => !string.IsNullOrEmpty(Reason);

    public bool HasProvisos => !string.IsNullOrEmpty(Provisos);

    public bool HasLaws => Laws.Count > 0;

    public bool HasSteps => _derivation is not null;

    public string StatusText => Kind switch
    {
        OutcomeKind.Success => "Success",
        OutcomeKind.Partial => "Partial: a budget ran out",
        OutcomeKind.Unevaluated => "Unevaluated: no method applies",
        _ => "Failed",
    };

    public string VerificationText => Check switch
    {
        Verification.Verified => "Verified independently",
        Verification.NumericallyConsistent => "Numerically consistent",
        Verification.Failed => "Verification failed",
        _ => string.Empty,
    };

    public bool HasVerification => Check != Verification.NotChecked;

    public IReadOnlyList<ExplanationFormat> Formats { get; } = Enum.GetValues<ExplanationFormat>();

    public IReadOnlyList<Verbosity> Verbosities { get; } = Enum.GetValues<Verbosity>();

    [ObservableProperty]
    public partial ExplanationFormat Format { get; set; }

    [ObservableProperty]
    public partial Verbosity Verbosity { get; set; }

    [ObservableProperty]
    public partial string StepsText { get; set; }

    partial void OnFormatChanged(ExplanationFormat value) => Render();

    partial void OnVerbosityChanged(Verbosity value) => Render();

    private void Render() => StepsText = _derivation is null ? string.Empty : _derivation.Render(Format, Verbosity, _level);
}
