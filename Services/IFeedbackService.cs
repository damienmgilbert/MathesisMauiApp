namespace MathesisMauiApp.Services;

/// <summary>Small acknowledgements: copying text and telling the user about it.</summary>
public interface IFeedbackService
{
    /// <summary>Puts <paramref name="text"/> on the clipboard and shows a toast naming <paramref name="what"/>.</summary>
    Task CopyAsync(string text, string what);

    /// <summary>Shows a short message that goes away by itself.</summary>
    Task ToastAsync(string message);
}
