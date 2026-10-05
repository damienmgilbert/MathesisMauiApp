using CommunityToolkit.Mvvm.ComponentModel;

namespace MathesisMauiApp.Controls;

/// <summary>What a <see cref="ValidatedInput"/> shows below its text box: the first failure of the field, or the "valid" mark.</summary>
public sealed partial class ValidatedInputState : ObservableObject
{
    [ObservableProperty]
    public partial bool IsValid { get; set; }

    [ObservableProperty]
    public partial bool IsInvalid { get; set; }

    /// <summary>The <c>MathValidationCode</c>, or "Invalid" for a failure of a standard attribute such as <c>[Required]</c>.</summary>
    [ObservableProperty]
    public partial string Code { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; }

    [ObservableProperty]
    public partial string Suggestion { get; set; }

    [ObservableProperty]
    public partial bool HasSuggestion { get; set; }

    /// <summary>The input with the span the failure concerns marked.</summary>
    [ObservableProperty]
    public partial FormattedString? Highlight { get; set; }

    [ObservableProperty]
    public partial bool HasHighlight { get; set; }

    [ObservableProperty]
    public partial string Location { get; set; }

    [ObservableProperty]
    public partial string More { get; set; }

    public ValidatedInputState()
    {
        Code = string.Empty;
        Message = string.Empty;
        Suggestion = string.Empty;
        Location = string.Empty;
        More = string.Empty;
    }

    public void Clear(bool valid)
    {
        IsValid = valid;
        IsInvalid = false;
        Code = Message = Suggestion = Location = More = string.Empty;
        HasSuggestion = HasHighlight = false;
        Highlight = null;
    }
}
