using CommunityToolkit.Mvvm.ComponentModel;
using Mathesis;
using Mathesis.Explanation;
using Mathesis.Knowledge;
using Mathesis.Symbolics;

namespace MathesisMauiApp.Services;

/// <summary>Keeps the settings in <see cref="IPreferences"/>, so they survive a restart and the class itself needs no platform code.</summary>
public sealed class SettingsService : ObservableObject, ISettingsService
{
    private const string FieldKey = "math.field";
    private const string LevelKey = "math.level";
    private const string FormatKey = "explain.format";
    private const string VerbosityKey = "explain.verbosity";
    private const string StepsKey = "budget.steps";
    private const string SecondsKey = "budget.seconds";
    private const string ThemeKey = "app.theme";
    private const string AssumptionsKey = "math.assumptions";
    private const int NoLevel = -1;

    private readonly IPreferences _preferences;
    private NumberField _field;
    private CurriculumLevel? _level;
    private ExplanationFormat _format;
    private Verbosity _verbosity;
    private int _maxSteps;
    private int _maxSeconds;
    private AppTheme _theme;
    private IReadOnlyList<string> _assumptions;

    public SettingsService(IPreferences preferences)
    {
        _preferences = preferences;
        _field = (NumberField)preferences.Get(FieldKey, (int)NumberField.Real);
        var level = preferences.Get(LevelKey, NoLevel);
        _level = level == NoLevel ? null : (CurriculumLevel)level;
        _format = (ExplanationFormat)preferences.Get(FormatKey, (int)ExplanationFormat.Text);
        _verbosity = (Verbosity)preferences.Get(VerbosityKey, (int)Verbosity.Standard);
        _maxSteps = preferences.Get(StepsKey, 100_000);
        _maxSeconds = preferences.Get(SecondsKey, 10);
        _theme = (AppTheme)preferences.Get(ThemeKey, (int)AppTheme.Unspecified);
        _assumptions = preferences.Get(AssumptionsKey, string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    public NumberField Field
    {
        get => _field;
        set { if (SetProperty(ref _field, value)) _preferences.Set(FieldKey, (int)value); }
    }

    public CurriculumLevel? Level
    {
        get => _level;
        set { if (SetProperty(ref _level, value)) _preferences.Set(LevelKey, value is { } l ? (int)l : NoLevel); }
    }

    public ExplanationFormat ExplanationFormat
    {
        get => _format;
        set { if (SetProperty(ref _format, value)) _preferences.Set(FormatKey, (int)value); }
    }

    public Verbosity ExplanationVerbosity
    {
        get => _verbosity;
        set { if (SetProperty(ref _verbosity, value)) _preferences.Set(VerbosityKey, (int)value); }
    }

    public int MaxSteps
    {
        get => _maxSteps;
        set { if (SetProperty(ref _maxSteps, value)) _preferences.Set(StepsKey, value); }
    }

    public int MaxSeconds
    {
        get => _maxSeconds;
        set { if (SetProperty(ref _maxSeconds, value)) _preferences.Set(SecondsKey, value); }
    }

    public AppTheme Theme
    {
        get => _theme;
        set { if (SetProperty(ref _theme, value)) _preferences.Set(ThemeKey, (int)value); }
    }

    public IReadOnlyList<string> Assumptions
    {
        get => _assumptions;
        set
        {
            if (SetProperty(ref _assumptions, value)) _preferences.Set(AssumptionsKey, string.Join('\n', value));
        }
    }

    public MathContext CreateContext()
    {
        var context = new MathContext { Field = _field, Level = _level };
        foreach (var text in _assumptions)
        {
            var parsed = Expr.TryParse(text);
            if (parsed.Expr is { } fact) context = context.Assume(fact);
        }

        return context;
    }

    public Budget CreateBudget(CancellationToken cancellationToken = default) =>
        new(maxSteps: _maxSteps, maxTime: TimeSpan.FromSeconds(_maxSeconds), cancellationToken: cancellationToken);
}
