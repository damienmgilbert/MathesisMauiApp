using System.ComponentModel.DataAnnotations;
using System.Windows.Input;
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

/// <summary>
/// One catalog entry. The page is pushed with the entry's ID as a query value; this ViewModel receives it through <see cref="IQueryAttributable"/>
/// (not <c>[QueryProperty]</c>, which is not trim-safe), unescapes it, and loads the entry.
/// </summary>
public sealed partial class CatalogEntryViewModel : PageViewModel, IQueryAttributable
{
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;
    private readonly IOutcomePresenter _presenter;
    private string _id = string.Empty;

    public CatalogEntryViewModel(INavigationService navigation, ISettingsService settings, IOutcomePresenter presenter)
    {
        _navigation = navigation;
        _settings = settings;
        _presenter = presenter;
    }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial string? EntryId { get; set; }

    [ObservableProperty]
    public partial string? Badges { get; set; }

    [ObservableProperty]
    public partial string? Explanation { get; set; }

    [ObservableProperty]
    public partial bool HasExplanation { get; set; }

    [ObservableProperty]
    public partial string? NotFound { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Facts { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Models.LawReference>? SeeAlso { get; set; }

    [ObservableProperty]
    public partial bool CanApply { get; set; }

    /// <summary>An expression to apply the law to; the attribute makes sure it is one.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Expression")]
    [Required]
    [MathExpression]
    public partial string? ApplyTo { get; set; }

    [ObservableProperty]
    public partial string? ApplyHint { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Query values that arrive through URI navigation are not decoded for us.
        if (query.TryGetValue(Routes.EntryIdKey, out var value) && value is string id) Load(Uri.UnescapeDataString(id));
    }

    [RelayCommand]
    private Task Back() => _navigation.GoBackAsync();

    partial void OnApplyToChanged(string? value) => _ = Apply();

    private async Task Apply()
    {
        if (!CanApply) return;
        await Task.Delay(200);
        if (HasErrors || string.IsNullOrWhiteSpace(ApplyTo))
        {
            Result = null;
            return;
        }

        var (id, text, context) = (_id, ApplyTo!, _settings.CreateContext());
        var outcome = await RunAsync(() => Cas.Apply(id, Expr.Parse(text), default, context));
        Result = _presenter.PresentExpr(outcome, "Applying " + id);
    }

    private void Load(string id)
    {
        _id = id;
        if (!KnowledgeBase.Default.TryGet(id, out var entry))
        {
            NotFound = $"There is no catalog entry '{id}'.";
            return;
        }

        NotFound = null;
        Name = entry.Name;
        EntryId = entry.Id.Value;
        Badges = string.Join("  ·  ", new[] { entry.Kind.ToString(), entry.Level?.ToString(), entry.DomainTitle }.Where(s => !string.IsNullOrEmpty(s)));
        Explanation = entry.Explain;
        HasExplanation = !string.IsNullOrWhiteSpace(entry.Explain);
        Facts = Describe(entry);
        SeeAlso = [.. entry.See.Select(see => KnowledgeBase.Default.TryGet(see.Value, out var other)
            ? new Models.LawReference(other.Id.Value, other.Name, other.Level?.ToString() ?? string.Empty, (ICommand)new AsyncRelayCommand(() => _navigation.OpenCatalogEntryAsync(other.Id.Value)))
            : null).OfType<Models.LawReference>()];

        // A law that rewrites one side into the other can be applied to an expression of the shape of its left-hand side.
        CanApply = entry.Statement is not null || entry.Match is not null;
        var left = entry.Statement is Apply { Operator.Id: "eq", Arguments: [var lhs, _] } ? lhs : entry.Match;
        ApplyHint = left is null ? "Type an expression of the form the law talks about." : $"The law's left-hand side is {TextPrinter.Print(left, PrintOptions.Presentation)}; try an expression of that shape.";
        ApplyTo = left is null ? string.Empty : left.ToString();
    }

    private static IReadOnlyList<Fact> Describe(Mathesis.Knowledge.Entry entry)
    {
        static string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);
        var facts = new List<Fact>();
        void Add(string label, string? value, string? note = null) { if (!string.IsNullOrWhiteSpace(value)) facts.Add(new Fact(label, value, note)); }

        if (entry.Statement is { } statement)
        {
            Add("Statement", Pretty(statement), "in the library's own notation");
            Add("LaTeX", statement.ToLatex());
        }

        foreach (var given in entry.Given) Add("Given", Pretty(given), "a condition the law needs");
        if (entry.Then is { } then) Add("Then", Pretty(then));
        if (entry.Defines is { } defines) Add("Defines", Pretty(defines));
        if (entry.Iff is { } iff) Add("If and only if", Pretty(iff));
        if (entry.Where is { } where) Add("Where", Pretty(where));
        if (entry.Complex is { } complex) Add("Over the complex numbers", Pretty(complex));
        if (entry.Match is { } match) Add("Matches", Pretty(match), "a pattern; the variables stand for any expression");
        if (entry.Yields is { } yields) Add("Yields", Pretty(yields));
        if (entry.AppliesTo is { } appliesTo) Add("Applies to", Pretty(appliesTo));
        if (entry.Orient is { } orient) Add("Orientation", orient.ToString(), "which way the rewriter may use it");
        if (entry.Result is { } result) Add("Result", Pretty(result));
        for (var i = 0; i < entry.Steps.Length; i++) Add($"Method step {i + 1}", entry.Steps[i].Description, Pretty(entry.Steps[i].Form));
        if (entry.SolveFor.Length > 0) Add("Solve for", string.Join(", ", entry.SolveFor));
        foreach (var quantity in entry.Quantities) Add($"Quantity {quantity.Variable}", quantity.Description);
        if (entry.Vars.Length > 0) Add("Variables", string.Join(", ", entry.Vars.Select(v => $"{v.Name}: {v.SortText}")));
        if (entry.Courses.Length > 0) Add("Courses", string.Join(", ", entry.Courses));
        if (entry.Tags.Length > 0) Add("Tags", string.Join(", ", entry.Tags));
        if (entry.Aliases.Length > 0) Add("Also known as", string.Join(", ", entry.Aliases.Select(a => a.Value)));
        foreach (var reference in entry.Refs) Add("Reference (" + reference.Kind + ")", reference.Value);
        if (entry.Verify is { } verify) Add("Verified by", verify.ToString(), verify switch
        {
            VerifyMode.Numeric => "checked numerically at random points that satisfy the conditions",
            VerifyMode.Instances => "checked on specific instances",
            VerifyMode.Proof => "backed by a proof object",
            _ => null,
        });
        Add("Defined in", $"{entry.File}:{entry.Line}", "the .mlaw file of the catalog");
        return facts;
    }
}
