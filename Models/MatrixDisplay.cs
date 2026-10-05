using System.Numerics;
using Mathesis.LinearAlgebra;

namespace MathesisMauiApp.Models;

/// <summary>A matrix as text cells, ready for a <c>MatrixView</c>.</summary>
public sealed record MatrixDisplay(int Rows, int Columns, IReadOnlyList<string> Cells)
{
    public static MatrixDisplay From<T>(DenseMatrix<T> matrix, Func<T, string>? format = null)
        where T : INumberBase<T>
    {
        format ??= static value => value?.ToString() ?? string.Empty;
        var cells = new List<string>(matrix.Rows * matrix.Columns);
        for (var r = 0; r < matrix.Rows; r++)
        {
            for (var c = 0; c < matrix.Columns; c++) cells.Add(format(matrix[r, c]));
        }

        return new MatrixDisplay(matrix.Rows, matrix.Columns, cells);
    }

    /// <summary>A vector as a single column.</summary>
    public static MatrixDisplay FromVector<T>(DenseVector<T> vector, Func<T, string>? format = null)
        where T : INumberBase<T>
    {
        format ??= static value => value?.ToString() ?? string.Empty;
        return new MatrixDisplay(vector.Length, 1, [.. vector.Select(format)]);
    }

    /// <summary>Vectors side by side, each as one column.</summary>
    public static MatrixDisplay FromVectors<T>(IReadOnlyList<DenseVector<T>> vectors, Func<T, string>? format = null)
        where T : INumberBase<T>
    {
        format ??= static value => value?.ToString() ?? string.Empty;
        var rows = vectors.Count == 0 ? 0 : vectors[0].Length;
        var cells = new List<string>();
        for (var r = 0; r < rows; r++)
        {
            foreach (var vector in vectors) cells.Add(format(vector[r]));
        }

        return new MatrixDisplay(rows, vectors.Count, cells);
    }
}
