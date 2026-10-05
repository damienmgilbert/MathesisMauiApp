namespace MathesisMauiApp.Controls;

/// <summary>A captioned text box without validation, for options and plain inputs.</summary>
public partial class FieldEntry : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(FieldEntry), string.Empty);
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(FieldEntry), null, BindingMode.TwoWay);
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(FieldEntry), string.Empty);
    public static readonly BindableProperty HintProperty = BindableProperty.Create(nameof(Hint), typeof(string), typeof(FieldEntry), string.Empty);
    public static readonly BindableProperty InputKeyboardProperty = BindableProperty.Create(nameof(InputKeyboard), typeof(Keyboard), typeof(FieldEntry), Keyboard.Default);

    public FieldEntry()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

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

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public Keyboard InputKeyboard
    {
        get => (Keyboard)GetValue(InputKeyboardProperty);
        set => SetValue(InputKeyboardProperty, value);
    }
}
