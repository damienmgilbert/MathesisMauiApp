using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

/// <summary>Shows an <see cref="OutcomeDisplay"/>: the value or the reason there is none, the verification status, the provisos and the derivation.</summary>
public partial class OutcomeView : ContentView
{
    public static readonly BindableProperty OutcomeProperty =
        BindableProperty.Create(nameof(Outcome), typeof(OutcomeDisplay), typeof(OutcomeView), null);

    public OutcomeView()
    {
        InitializeComponent();
    }

    public OutcomeDisplay? Outcome
    {
        get => (OutcomeDisplay?)GetValue(OutcomeProperty);
        set => SetValue(OutcomeProperty, value);
    }
}
