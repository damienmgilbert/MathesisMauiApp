using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Mathesis.Validation;

namespace MathesisMauiApp.Controls;

/// <summary>
/// A text box bound to a property of a form ViewModel that is an <see cref="INotifyDataErrorInfo"/> (an <c>ObservableValidator</c>).
/// Below the box a <see cref="FeedbackView"/> shows the first failure of that property: the stable <see cref="MathValidationCode"/>, the
/// message, the part of the text the parser blames, and the suggestion, all read from the <see cref="MathValidationResult"/> the attribute produced.
/// </summary>
public partial class ValidatedInput : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ValidatedInput), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ValidatedInput), null, BindingMode.TwoWay,
            propertyChanged: static (bindable, _, _) => ((ValidatedInput)bindable).RefreshLater());

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ValidatedInput), string.Empty);

    public static readonly BindableProperty AnnotationProperty =
        BindableProperty.Create(nameof(Annotation), typeof(string), typeof(ValidatedInput), string.Empty);

    public static readonly BindableProperty FieldProperty =
        BindableProperty.Create(nameof(Field), typeof(string), typeof(ValidatedInput), string.Empty,
            propertyChanged: static (bindable, _, _) => ((ValidatedInput)bindable).RefreshLater());

    public static readonly BindableProperty InputKeyboardProperty =
        BindableProperty.Create(nameof(InputKeyboard), typeof(Keyboard), typeof(ValidatedInput), Keyboard.Default);

    public static readonly BindableProperty IsMultilineProperty =
        BindableProperty.Create(nameof(IsMultiline), typeof(bool), typeof(ValidatedInput), false);

    public static readonly BindableProperty ResultProperty =
        BindableProperty.Create(nameof(Result), typeof(ValidationResult), typeof(ValidatedInput), null);

    public static readonly BindableProperty ExtraErrorsProperty =
        BindableProperty.Create(nameof(ExtraErrors), typeof(int), typeof(ValidatedInput), 0);

    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(nameof(HasError), typeof(bool), typeof(ValidatedInput), false);

    public static readonly BindableProperty IsOkProperty =
        BindableProperty.Create(nameof(IsOk), typeof(bool), typeof(ValidatedInput), false);

    private INotifyDataErrorInfo? _source;

    public ValidatedInput()
    {
        InitializeComponent();
    }

    /// <summary>The caption above the box.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The text; bind it to the property that carries the validation attributes.</summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>The attribute as written in code, shown in small type so the page documents itself.</summary>
    public string Annotation
    {
        get => (string)GetValue(AnnotationProperty);
        set => SetValue(AnnotationProperty, value);
    }

    /// <summary>The name of the property of the BindingContext whose errors are shown.</summary>
    public string Field
    {
        get => (string)GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    public Keyboard InputKeyboard
    {
        get => (Keyboard)GetValue(InputKeyboardProperty);
        set => SetValue(InputKeyboardProperty, value);
    }

    /// <summary>Uses a multi-line editor instead of a one-line entry.</summary>
    public bool IsMultiline
    {
        get => (bool)GetValue(IsMultilineProperty);
        set => SetValue(IsMultilineProperty, value);
    }

    /// <summary>The first failure of <see cref="Field"/>, or <c>null</c>.</summary>
    public ValidationResult? Result
    {
        get => (ValidationResult?)GetValue(ResultProperty);
        private set => SetValue(ResultProperty, value);
    }

    public int ExtraErrors
    {
        get => (int)GetValue(ExtraErrorsProperty);
        private set => SetValue(ExtraErrorsProperty, value);
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        private set => SetValue(HasErrorProperty, value);
    }

    /// <summary>True for a non-empty text without failures.</summary>
    public bool IsOk
    {
        get => (bool)GetValue(IsOkProperty);
        private set => SetValue(IsOkProperty, value);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_source is not null) _source.ErrorsChanged -= OnErrorsChanged;
        _source = BindingContext as INotifyDataErrorInfo;
        if (_source is not null) _source.ErrorsChanged += OnErrorsChanged;
        Refresh();
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == Field) Refresh();
    }

    // The text reaches the ViewModel after this control's own property changed; look at the errors once that has happened.
    private void RefreshLater() => Dispatcher.Dispatch(Refresh);

    private void Refresh()
    {
        var errors = _source is null || string.IsNullOrEmpty(Field)
            ? []
            : _source.GetErrors(Field).OfType<ValidationResult>().ToList();
        Result = errors.Count > 0 ? errors[0] : null;
        ExtraErrors = Math.Max(0, errors.Count - 1);
        HasError = errors.Count > 0;
        IsOk = errors.Count == 0 && !string.IsNullOrWhiteSpace(Text);
    }
}
