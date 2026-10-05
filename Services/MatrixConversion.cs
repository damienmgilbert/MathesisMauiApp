using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace MathesisMauiApp.Services;

/// <summary>Turns the text of a matrix that <c>MathMatrix</c> has accepted into the exact matrix type of Mathesis.LinearAlgebra.</summary>
public static class MatrixConversion
{
    /// <summary>Parses <paramref name="text"/> as a matrix literal whose entries are rational numbers.</summary>
    public static bool TryParse(string? text, out DenseMatrix<BigRational> matrix)
    {
        matrix = null!;
        if (string.IsNullOrWhiteSpace(text) || Expr.TryParse(text).Expr is not MatrixLiteral literal) return false;

        var values = new BigRational[literal.Entries.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (!TryToRational(literal.Entries[i], out values[i])) return false;
        }

        matrix = DenseMatrix.FromRowMajor<BigRational>(literal.Rows, literal.Columns, values);
        return true;
    }

    /// <summary>The same shapes <c>MathMatrix</c> calls numeric: a number, a negated number, or a fraction of those.</summary>
    public static bool TryToRational(Expr entry, out BigRational value)
    {
        switch (entry)
        {
            case Number number:
                value = number.Value;
                return true;
            case Float { Value: var f } when double.IsFinite(f):
                value = BigRational.FromShortestDecimal(f);
                return true;
            case Apply { Operator.Id: "neg", Arguments: [var inner] } when TryToRational(inner, out var positive):
                value = -positive;
                return true;
            case Apply { Operator.Id: "div", Arguments: [var top, var bottom] } when TryToRational(top, out var numerator) && TryToRational(bottom, out var denominator) && denominator != BigRational.Zero:
                value = numerator / denominator;
                return true;
            default:
                value = default;
                return false;
        }
    }

    /// <summary>The same matrix with double entries, for the floating-point factorizations.</summary>
    public static DenseMatrix<double> ToDouble(DenseMatrix<BigRational> matrix) => DenseMatrix.Create(matrix.Rows, matrix.Columns, (r, c) => matrix[r, c].ToDouble());
}
