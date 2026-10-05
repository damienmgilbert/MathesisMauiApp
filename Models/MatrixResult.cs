namespace MathesisMauiApp.Models;

/// <summary>One part of a linear algebra result: a titled matrix and/or text.</summary>
public sealed record MatrixResult(string Title, MatrixDisplay? Matrix = null, string? Text = null)
{
    public bool HasMatrix => Matrix is not null;

    public bool HasText => !string.IsNullOrEmpty(Text);
}
