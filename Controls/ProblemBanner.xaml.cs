namespace MathesisMauiApp.Controls;

/// <summary>Shows the unexpected exception of a background computation (see <c>PageViewModel.Problem</c>).</summary>
public partial class ProblemBanner : ContentView
{
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(ProblemBanner), null);

    public ProblemBanner()
    {
        InitializeComponent();
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
