using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Knowledge;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Symbolics.Rewriting;
using MathesisMauiApp.Models;

namespace MathesisMauiApp.Services;

public sealed class OutcomePresenter(ISettingsService settings, IFeedbackService feedback, INavigationService navigation) : IOutcomePresenter
{
    public OutcomeDisplay Present<T>(Outcome<T> outcome, Func<T, string> print, Func<T, string?>? latex = null, Func<T, string?>? input = null, string heading = "Result")
    {
        switch (outcome)
        {
            case Outcome<T>.Success success:
                return Build(OutcomeKind.Success, heading, print(success.Value), latex?.Invoke(success.Value), input?.Invoke(success.Value), null,
                    success.Check, success.Provisos, success.Steps);
            case Outcome<T>.Partial partial:
                return Build(OutcomeKind.Partial, heading, print(partial.Value), latex?.Invoke(partial.Value), input?.Invoke(partial.Value), partial.Reason,
                    Verification.NotChecked, partial.Provisos, partial.Steps);
            case Outcome<T>.Unevaluated unevaluated:
                return Build(OutcomeKind.Unevaluated, heading, string.Empty, null, null, unevaluated.Reason, Verification.NotChecked, Provisos.None, null);
            case Outcome<T>.Failed failed:
                return Build(OutcomeKind.Failed, heading, string.Empty, null, null, failed.Error.ToString(), Verification.NotChecked, Provisos.None, null);
            default:
                return Message(OutcomeKind.Failed, heading, "Unknown outcome.");
        }
    }

    public OutcomeDisplay PresentExpr(Outcome<Expr> outcome, string heading = "Result") =>
        Present(outcome, e => TextPrinter.Print(e, PrintOptions.Presentation), e => e.ToLatex(), e => e.ToString(), heading);

    public OutcomeDisplay Message(OutcomeKind kind, string heading, string message) =>
        Build(kind, heading, string.Empty, null, null, message, Verification.NotChecked, Provisos.None, null);

    private OutcomeDisplay Build(OutcomeKind kind, string heading, string value, string? latex, string? input, string? reason, Verification check, Provisos provisos, IDerivation? steps)
    {
        var laws = new List<LawReference>();
        if (steps is Derivation derivation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in derivation.Flatten())
            {
                if (step.Entry is not { } id || !seen.Add(id.Value) || !KnowledgeBase.Default.TryGet(id.Value, out var entry)) continue;
                var law = entry.Id.Value;
                ICommand open = new AsyncRelayCommand(() => navigation.OpenCatalogEntryAsync(law));
                laws.Add(new LawReference(law, entry.Name, entry.Level?.ToString() ?? string.Empty, open));
            }
        }

        return new OutcomeDisplay(kind, heading, value, latex, input, reason, check,
            provisos.Count == 0 ? null : provisos.ToString(), steps, settings.Level, laws,
            settings.ExplanationFormat, settings.ExplanationVerbosity,
            (text, what) => _ = feedback.CopyAsync(text, what));
    }
}
