namespace MathesisMauiApp.Controls;

/// <summary>The heading of a page: a small eyebrow line, the title and one sentence about what the page shows.</summary>
public partial class PageHeader : ContentView
{
    public static readonly BindableProperty EyebrowProperty = BindableProperty.Create(nameof(Eyebrow), typeof(string), typeof(PageHeader), string.Empty);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeader), string.Empty);
    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(nameof(Description), typeof(string), typeof(PageHeader), string.Empty);

    public PageHeader()
    {
        InitializeComponent();
    }

    public string Eyebrow
    {
        get => (string)GetValue(EyebrowProperty);
        set => SetValue(EyebrowProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
