using System.ComponentModel;
using Mathesis;
using Mathesis.Explanation;
using Mathesis.Knowledge;
using Mathesis.Symbolics;

namespace MathesisMauiApp.Services;

/// <summary>The settings that decide how every page calls Mathesis: the <see cref="MathContext"/>, the <see cref="Budget"/> and how steps are explained.</summary>
public interface ISettingsService : INotifyPropertyChanged
{
    /// <summary>Real mode (the default) or complex mode.</summary>
    NumberField Field { get; set; }

    /// <summary>The curriculum level that restricts which laws may be cited, or <c>null</c> for no restriction.</summary>
    CurriculumLevel? Level { get; set; }

    /// <summary>The format steps are rendered in.</summary>
    ExplanationFormat ExplanationFormat { get; set; }

    /// <summary>How much detail steps show.</summary>
    Verbosity ExplanationVerbosity { get; set; }

    /// <summary>The step limit of the <see cref="Budget"/>.</summary>
    int MaxSteps { get; set; }

    /// <summary>The time limit of the <see cref="Budget"/>, in seconds.</summary>
    int MaxSeconds { get; set; }

    /// <summary>The theme the user picked, or <see cref="AppTheme.Unspecified"/> to follow the system.</summary>
    AppTheme Theme { get; set; }

    /// <summary>The assumptions as typed (<c>x &gt; 0</c>).</summary>
    IReadOnlyList<string> Assumptions { get; set; }

    /// <summary>A <see cref="MathContext"/> with the field, level and every assumption that parses.</summary>
    MathContext CreateContext();

    /// <summary>A fresh <see cref="Budget"/>; a budget counts the work of one operation, so each call needs its own. Cancelling <paramref name="cancellationToken"/> stops the operation, which then returns a Partial outcome.</summary>
    Budget CreateBudget(CancellationToken cancellationToken = default);
}
