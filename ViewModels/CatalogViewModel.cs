using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Knowledge;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>One catalog entry as a row of the list.</summary>
public sealed record CatalogRow(string Id, string Name, string Kind, string Level, string Domain);

/// <summary>Search and browse the knowledge catalog: the verified laws, theorems and methods that every step of a derivation cites.</summary>
public sealed partial class CatalogViewModel : PageViewModel
{
    private const string All = "All";

    private readonly INavigationService _navigation;
    private readonly Debouncer _typing = new();

    public CatalogViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        Domain = All;
        Kind = All;
        Level = All;
        _ = LoadAsync();
    }

    public IReadOnlyList<string> Domains { get; private set; } = [All];

    public IReadOnlyList<string> Kinds { get; } = [All, .. Enum.GetNames<EntryKind>()];

    public IReadOnlyList<string> Levels { get; } = [All, .. Enum.GetNames<CurriculumLevel>()];

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial string Domain { get; set; }

    [ObservableProperty]
    public partial string Kind { get; set; }

    [ObservableProperty]
    public partial string Level { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CatalogRow>? Results { get; set; }

    [ObservableProperty]
    public partial string? Summary { get; set; }

    // Search once typing pauses, not on every key; the filters search at once.
    partial void OnSearchTextChanged(string? value) => _ = Debounced(_typing, TimeSpan.FromMilliseconds(300), _ => SearchAsync());

    partial void OnDomainChanged(string value) => _ = SearchAsync();

    partial void OnKindChanged(string value) => _ = SearchAsync();

    partial void OnLevelChanged(string value) => _ = SearchAsync();

    [RelayCommand]
    private Task Open(CatalogRow row) => _navigation.OpenCatalogEntryAsync(row.Id);

    private async Task LoadAsync()
    {
        // The catalog is parsed from embedded resources the first time it is used, so keep that off the UI thread.
        var domains = await RunAsync(() => KnowledgeBase.Default.Entries.Select(e => e.Domain.Split('.')[0]).Distinct().Order(StringComparer.Ordinal).ToList());
        Domains = [All, .. domains];
        OnPropertyChanged(nameof(Domains));
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var (text, domain, kind, level) = (SearchText, Domain, Kind, Level);
        var rows = await RunAsync(() =>
        {
            var catalog = KnowledgeBase.Default;
            IEnumerable<Mathesis.Knowledge.Entry> entries = string.IsNullOrWhiteSpace(text) ? catalog.Entries.OrderBy(e => e.Id.Value, StringComparer.Ordinal) : catalog.Search(text);
            if (domain != All) entries = entries.Where(e => e.Domain == domain || e.Domain.StartsWith(domain + ".", StringComparison.Ordinal));
            if (kind != All) entries = entries.Where(e => e.Kind.ToString() == kind);
            if (level != All) entries = entries.Where(e => e.Level?.ToString() == level);
            return (Rows: entries.Select(e => new CatalogRow(e.Id.Value, e.Name, e.Kind.ToString(), e.Level?.ToString() ?? string.Empty, e.Domain)).ToList(), Total: catalog.Count);
        });
        Results = rows.Rows;
        Summary = $"{rows.Rows.Count} of {rows.Total} entries";
    }
}
