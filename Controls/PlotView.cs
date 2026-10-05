using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

/// <summary>A small plotting surface drawn with Microsoft.Maui.Graphics: curves, point sets and markers over automatic axes.</summary>
public sealed class PlotView : GraphicsView
{
    public static readonly BindableProperty ModelProperty =
        BindableProperty.Create(nameof(Model), typeof(PlotModel), typeof(PlotView), null,
            propertyChanged: static (bindable, _, _) => ((PlotView)bindable).OnModelChanged());

    private readonly PlotDrawable _drawable = new();

    public PlotView()
    {
        Drawable = _drawable;
        HeightRequest = 300;
        HorizontalOptions = LayoutOptions.Fill;
    }

    public PlotModel? Model
    {
        get => (PlotModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Application.Current is not { } app) return;
        app.RequestedThemeChanged -= OnThemeChanged;
        if (Handler is not null) app.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Invalidate();

    private void OnModelChanged()
    {
        _drawable.Model = Model;
        Invalidate();
    }
}
