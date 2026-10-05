using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

/// <summary>Rows of "label, value" with an optional note under the value.</summary>
public partial class FactTable : ContentView
{
    public static readonly BindableProperty FactsProperty =
        BindableProperty.Create(nameof(Facts), typeof(IEnumerable<Fact>), typeof(FactTable), null);

    public FactTable()
    {
        InitializeComponent();
    }

    public IEnumerable<Fact>? Facts
    {
        get => (IEnumerable<Fact>?)GetValue(FactsProperty);
        set => SetValue(FactsProperty, value);
    }
}
