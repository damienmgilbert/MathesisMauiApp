using Mathesis;
using Mathesis.Symbolics;
using MathesisMauiApp.Models;

namespace MathesisMauiApp.Services;

/// <summary>Turns the <see cref="Outcome{T}"/> of any Mathesis operation into an <see cref="OutcomeDisplay"/>, using the explanation settings.</summary>
public interface IOutcomePresenter
{
    /// <summary>Presents <paramref name="outcome"/>; <paramref name="print"/> writes a value for display, <paramref name="latex"/> and <paramref name="input"/> are optional alternative notations.</summary>
    OutcomeDisplay Present<T>(Outcome<T> outcome, Func<T, string> print, Func<T, string?>? latex = null, Func<T, string?>? input = null, string heading = "Result");

    /// <summary>Presents an expression result in presentation notation, LaTeX and input notation.</summary>
    OutcomeDisplay PresentExpr(Outcome<Expr> outcome, string heading = "Result");

    /// <summary>A result that is not an <see cref="Outcome{T}"/> of its own: a message of the given kind.</summary>
    OutcomeDisplay Message(OutcomeKind kind, string heading, string message);
}
