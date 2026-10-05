using MathesisMauiApp.Models;

namespace MathesisMauiApp.Controls;

/// <summary>A matrix drawn with brackets around a grid of right-aligned cells.</summary>
public sealed class MatrixView : ContentView
{
    public static readonly BindableProperty MatrixProperty =
        BindableProperty.Create(nameof(Matrix), typeof(MatrixDisplay), typeof(MatrixView), null,
            propertyChanged: static (bindable, _, _) => ((MatrixView)bindable).Rebuild());

    public MatrixView()
    {
        HorizontalOptions = LayoutOptions.Start;
    }

    public MatrixDisplay? Matrix
    {
        get => (MatrixDisplay?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    private void Rebuild()
    {
        if (Matrix is not { Rows: > 0, Columns: > 0 } matrix)
        {
            Content = null;
            return;
        }

        var grid = new Grid { ColumnSpacing = 0, RowSpacing = 0, Padding = new Thickness(2, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var c = 0; c < matrix.Columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < matrix.Rows; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var leftBracket = Bracket(left: true);
        Grid.SetRowSpan(leftBracket, matrix.Rows);
        grid.Add(leftBracket, 0, 0);
        for (var r = 0; r < matrix.Rows; r++)
        {
            for (var c = 0; c < matrix.Columns; c++)
            {
                var label = new Label
                {
                    Text = matrix.Cells[r * matrix.Columns + c],
                    HorizontalTextAlignment = TextAlignment.End,
                    VerticalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(10, 3),
                };
                label.StyleClass = ["Code"];
                grid.Add(label, c + 1, r);
            }
        }

        var rightBracket = Bracket(left: false);
        Grid.SetRowSpan(rightBracket, matrix.Rows);
        grid.Add(rightBracket, matrix.Columns + 1, 0);
        Content = grid;
    }

    // A bracket is a vertical bar with a short serif at the top and at the bottom.
    private static View Bracket(bool left)
    {
        var side = left ? LayoutOptions.Start : LayoutOptions.End;
        var cell = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, WidthRequest = 8 };

        BoxView Line(double width, double height, LayoutOptions horizontal)
        {
            var box = new BoxView { WidthRequest = width, HeightRequest = height, HorizontalOptions = horizontal, VerticalOptions = LayoutOptions.Fill };
            box.SetAppThemeColor(BoxView.ColorProperty, (Color)Application.Current!.Resources["TextSecondary"], (Color)Application.Current.Resources["TextSecondaryDark"]);
            return box;
        }

        cell.Add(Line(8, 2, side), 0, 0);
        cell.Add(Line(2, 1, side), 0, 1);
        cell.Add(Line(8, 2, side), 0, 2);
        return cell;
    }
}
