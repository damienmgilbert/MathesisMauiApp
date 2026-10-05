using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Knowledge;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>One or two tiles side by side.</summary>
public sealed record HomeRow(NavItem Left, NavItem? Right)
{
    public bool HasRight => Right is not null;
}

public sealed record HomeGroup(string Title, string Summary, IReadOnlyList<HomeRow> Rows);

public sealed partial class HomeViewModel : PageViewModel
{
    private static readonly Symbol X = new("x");

    private readonly INavigationService _navigation;
    private CancellationTokenSource? _quickCancellation;

    public HomeViewModel(INavigationService navigation, IDeviceInfo deviceInfo)
    {
        _navigation = navigation;

        // Tiles are laid out in rows of two on a desktop or tablet and one on a phone, so a tile always has a definite width and its text wraps.
        var columns = deviceInfo.Idiom == DeviceIdiom.Phone ? 1 : 2;
        Groups = [.. NavigationCatalog.Create().Select(g => new HomeGroup(g.Title, g.Summary, [.. g.Items.Chunk(columns).Select(c => new HomeRow(c[0], c.Length > 1 ? c[1] : null))]))];
        Formula = "sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)";
        _ = LoadStatsAsync();
    }

    public IReadOnlyList<HomeGroup> Groups { get; }

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("Pythagoras", "sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)"),
        new("Product rule", "x^2*sin(x)"),
        new("Rational", "(x^2 - 4)/(x^2 - 5x + 6)"),
        new("Exponential", "x*exp(x)"),
        new("Typo", "sqr(x) +"),
        new("Another variable", "x + y"),
    ];

    /// <summary>The expression of the quick try. One attribute decides what may be typed: an expression in x only, without calculus operators.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Expression")]
    [Required]
    [MathExpression(Variables = ["x"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.Set)]
    public partial string? Formula { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Findings { get; set; }

    [ObservableProperty]
    public partial string? CatalogSummary { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Formula = text;

    [RelayCommand]
    private Task Open(NavItem item) => _navigation.GoToAsync($"//{item.Route}");

    partial void OnFormulaChanged(string? value) => _ = ShowFindingsAsync(value);

    // The work is cheap, but the input is arbitrary: wait for a pause in typing, bound the work with a time limit and cancel it when the text changes again.
    private async Task ShowFindingsAsync(string? text)
    {
        _quickCancellation?.Cancel();
        _quickCancellation?.Dispose();
        var source = _quickCancellation = new CancellationTokenSource();
        try
        {
            // Validation of the property runs after this callback, so look at the errors only once the pause is over.
            await Task.Delay(200, source.Token);
            if (HasErrors || string.IsNullOrWhiteSpace(text))
            {
                Findings = null;
                return;
            }

            var facts = await Task.Run(() => Analyze(text, source.Token), source.Token);
            if (!source.IsCancellationRequested) Findings = facts;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static IReadOnlyList<Fact> Analyze(string text, CancellationToken token)
    {
        var expr = Expr.Parse(text);
        string Show<T>(Outcome<T> outcome, Func<T, string> print) => outcome switch
        {
            Outcome<T>.Success s => print(s.Value),
            Outcome<T>.Partial p => print(p.Value) + "  (partial: " + p.Reason + ")",
            Outcome<T>.Unevaluated u => "unevaluated: " + u.Reason,
            Outcome<T>.Failed f => "failed: " + f.Error,
            _ => string.Empty,
        };

        string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);
        var simplified = Cas.Simplify(expr, budget: new Budget(maxTime: TimeSpan.FromSeconds(2), cancellationToken: token));
        var derivative = Cas.Differentiate(expr, X, budget: new Budget(maxTime: TimeSpan.FromSeconds(2), cancellationToken: token));
        return
        [
            new("Parsed as", Pretty(expr), "the linear input notation, printed back"),
            new("LaTeX", expr.ToLatex()),
            new("Simplified", Show(simplified, Pretty), "Cas.Simplify, with every step citing a catalog law"),
            new("d/dx", Show(derivative, Pretty), "Cas.Differentiate"),
        ];
    }

    private async Task LoadStatsAsync()
    {
        var count = await Task.Run(() => KnowledgeBase.Default.Count);
        CatalogSummary = $"{count} verified laws in the catalog · 7 validation attributes · 22 validation codes";
    }
}
