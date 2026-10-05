using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

/// <summary>Rows of "input, kind, verdict", used to compare how an attribute judges different inputs.</summary>
public partial class VerdictTable : ContentView
{
    public static readonly BindableProperty RowsProperty =
        BindableProperty.Create(nameof(Rows), typeof(IEnumerable<VerdictRow>), typeof(VerdictTable), null);

    public VerdictTable()
    {
        InitializeComponent();
    }

    public IEnumerable<VerdictRow>? Rows
    {
        get => (IEnumerable<VerdictRow>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }
}
