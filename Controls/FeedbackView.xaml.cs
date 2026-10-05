using System.ComponentModel.DataAnnotations;
using Mathesis.Symbolics.Parsing;
using Mathesis.Validation;

namespace MathesisMauiApp.Controls;

/// <summary>
/// Shows the verdict of a validation attribute on a text: the stable <see cref="MathValidationCode"/>, the message, the part of the text the
/// parser blames (underlined), and the suggestion. It reads everything from the <see cref="MathValidationResult"/>; a plain
/// <see cref="ValidationResult"/> (from <c>[Required]</c> or any other attribute) shows its message only.
/// </summary>
public partial class FeedbackView : ContentView
{
    public static readonly BindableProperty ResultProperty =
        BindableProperty.Create(nameof(Result), typeof(ValidationResult), typeof(FeedbackView), null,
            propertyChanged: static (bindable, _, _) => ((FeedbackView)bindable).Update());

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FeedbackView), null,
            propertyChanged: static (bindable, _, _) => ((FeedbackView)bindable).Update());

    public static readonly BindableProperty ExtraErrorsProperty =
        BindableProperty.Create(nameof(ExtraErrors), typeof(int), typeof(FeedbackView), 0,
            propertyChanged: static (bindable, _, _) => ((FeedbackView)bindable).Update());

    public static readonly BindableProperty ShowValidProperty =
        BindableProperty.Create(nameof(ShowValid), typeof(bool), typeof(FeedbackView), true,
            propertyChanged: static (bindable, _, _) => ((FeedbackView)bindable).Update());

    public FeedbackView()
    {
        InitializeComponent();
    }

    /// <summary>The failure to show, or <c>null</c> when the text is valid.</summary>
    public ValidationResult? Result
    {
        get => (ValidationResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    /// <summary>The text that was validated; needed to underline the span.</summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>How many more failures the property has beyond <see cref="Result"/>.</summary>
    public int ExtraErrors
    {
        get => (int)GetValue(ExtraErrorsProperty);
        set => SetValue(ExtraErrorsProperty, value);
    }

    /// <summary>Whether a valid, non-empty text shows a check mark.</summary>
    public bool ShowValid
    {
        get => (bool)GetValue(ShowValidProperty);
        set => SetValue(ShowValidProperty, value);
    }

    public ValidatedInputState State { get; } = new();

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Application.Current is not { } app) return;
        app.RequestedThemeChanged -= OnThemeChanged;
        if (Handler is not null) app.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Update();

    private void Update()
    {
        if (Result is not { } result)
        {
            State.Clear(valid: ShowValid && !string.IsNullOrWhiteSpace(Text));
            return;
        }

        var math = result as MathValidationResult;
        State.IsValid = false;
        State.IsInvalid = true;
        State.Code = math?.Code.ToString() ?? "Invalid";
        State.Message = result.ErrorMessage ?? "The value is not valid.";
        State.Suggestion = math?.Suggestion is { Length: > 0 } suggestion ? "Suggestion: " + suggestion : string.Empty;
        State.HasSuggestion = State.Suggestion.Length > 0;
        State.More = ExtraErrors > 0 ? $"+{ExtraErrors} more" : string.Empty;

        if (math?.Span is { } span && !string.IsNullOrEmpty(Text))
        {
            State.Location = Describe(span, Text.Length);
            State.Highlight = Highlight(Text, span);
            State.HasHighlight = true;
        }
        else
        {
            State.Location = string.Empty;
            State.Highlight = null;
            State.HasHighlight = false;
        }
    }

    private static string Describe(TextSpan span, int length)
    {
        if (span.Length == 0) return span.Start >= length ? "at the end" : $"at column {span.Start + 1}";
        return span.Length == 1 ? $"column {span.Start + 1}" : $"columns {span.Start + 1}–{span.End}";
    }

    /// <summary>The text with the span underlined; a span of length zero (the input ended, something is missing) becomes a marker at that position.</summary>
    private static FormattedString Highlight(string text, TextSpan span)
    {
        var start = Math.Clamp(span.Start, 0, text.Length);
        var length = Math.Clamp(span.Length, 0, text.Length - start);
        var font = ThemeColors.MonoFont;
        var plain = ThemeColors.Get("TextPrimary");
        var danger = ThemeColors.Get("Danger");

        Span Plain(string value) => new() { Text = value, FontFamily = font, TextColor = plain };
        Span Hit(string value) => new() { Text = value, FontFamily = font, TextColor = danger, TextDecorations = TextDecorations.Underline, FontAttributes = FontAttributes.Bold };

        var formatted = new FormattedString();
        if (start > 0) formatted.Spans.Add(Plain(text[..start]));
        formatted.Spans.Add(length == 0 ? Hit("▮") : Hit(text.Substring(start, length)));
        if (start + length < text.Length) formatted.Spans.Add(Plain(text[(start + length)..]));
        return formatted;
    }
}
