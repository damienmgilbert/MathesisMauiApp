using System.Windows.Input;
using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

/// <summary>A wrapping row of small buttons; tapping one runs <see cref="Command"/> with the sample's text.</summary>
public partial class ExampleChips : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ExampleChips), "Try one of these");

    public static readonly BindableProperty SamplesProperty =
        BindableProperty.Create(nameof(Samples), typeof(IEnumerable<Sample>), typeof(ExampleChips), null);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ExampleChips), null);

    public ExampleChips()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable<Sample>? Samples
    {
        get => (IEnumerable<Sample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>Runs with the text of the tapped sample as its parameter.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void OnChipClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string text } || Command is not { } command) return;
        if (command.CanExecute(text)) command.Execute(text);
    }
}
