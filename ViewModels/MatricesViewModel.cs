using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.LinearAlgebra;
using Mathesis.LinearAlgebra.Exact;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Symbolics.Representations;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

public enum MatrixOperation
{
    RowReduce,
    Determinant,
    Inverse,
    Rank,
    NullSpace,
    ColumnSpace,
    Eigen,
    CharacteristicPolynomial,
    SolveSystem,
    LuDecomposition,
    QrDecomposition,
    Cholesky,
    SymmetricEigen,
}

/// <summary>
/// Matrices typed as text. <c>MathMatrix</c> checks the shape, then the entries are read exactly as rationals and fed to the linear algebra
/// of Mathesis: exact row reduction, determinants, inverses and eigenvalues, and the floating-point LU, QR, Cholesky and Jacobi factorizations.
/// </summary>
public sealed partial class MatricesViewModel : PageViewModel
{
    private const int MaxDimension = 6;
    private static readonly MathMatrixAttribute SquareRule = new() { Square = true, MaxDimension = MaxDimension };

    private readonly Debouncer _debounce = new();

    public MatricesViewModel()
    {
        MatrixA = "[[2, 1, 1], [1, 3, 2], [1, 0, 0]]";
        VectorB = "[[1], [2], [3]]";
        Operation = MatrixOperation.RowReduce;
    }

    public IReadOnlyList<MatrixOperation> Operations { get; } = Enum.GetValues<MatrixOperation>();

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("2×2", "[[2, 1], [1, 3]]"),
        new("3×3", "[[2, 1, 1], [1, 3, 2], [1, 0, 0]]"),
        new("symmetric", "[[4, 1, 2], [1, 3, 0], [2, 0, 5]]"),
        new("singular", "[[1, 2], [2, 4]]"),
        new("rotation", "[[0, -1], [1, 0]]"),
        new("fractions", "[[1, -1/2], [0.25, 3]]"),
        new("2×3", "[[1, 2, 3], [4, 5, 6]]"),
        new("[1, 2]", "[1, 2]"),
        new("symbolic", "[[1, x], [2, 3]]"),
        new("ragged", "[[1, 2], [3]]"),
        new("1×7", "[[1, 2, 3, 4, 5, 6, 7]]"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Matrix A")]
    [Required]
    [MathMatrix(MaxDimension = MaxDimension)]
    public partial string? MatrixA { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Vector b")]
    [MathMatrix(Columns = 1, MaxDimension = MaxDimension)]
    public partial string? VectorB { get; set; }

    [ObservableProperty]
    public partial MatrixOperation Operation { get; set; }

    /// <summary>The failure of a rule that only some operations add (A must be square), shown like any other verdict.</summary>
    [ObservableProperty]
    public partial ValidationResult? OperationProblem { get; set; }

    [ObservableProperty]
    public partial string? OperationNote { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Facts { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MatrixResult>? Blocks { get; set; }

    [RelayCommand]
    private void UseSample(string text) => MatrixA = text;

    partial void OnMatrixAChanged(string? value) => _ = Refresh();

    partial void OnVectorBChanged(string? value) => _ = Refresh();

    partial void OnOperationChanged(MatrixOperation value) => _ = Refresh();

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(200), async token =>
    {
        var operation = Operation;
        OperationNote = Describe(operation);
        var needsSquare = operation is not (MatrixOperation.RowReduce or MatrixOperation.Rank or MatrixOperation.NullSpace or MatrixOperation.ColumnSpace or MatrixOperation.QrDecomposition);
        OperationProblem = needsSquare && !string.IsNullOrWhiteSpace(MatrixA) && !GetErrors(nameof(MatrixA)).Any()
            ? SquareRule.Check(MatrixA, "Matrix A", nameof(MatrixA))
            : null;

        if (HasErrorsOn(nameof(MatrixA)) || OperationProblem is not null || !MatrixConversion.TryParse(MatrixA, out var a))
        {
            Facts = null;
            Blocks = null;
            return;
        }

        MatrixConversion.TryParse(VectorB, out var b);
        var output = await Task.Run(() => Compute(operation, a, HasErrorsOn(nameof(VectorB)) ? null : b), token);
        Facts = output.Facts;
        Blocks = output.Blocks;
    });

    private bool HasErrorsOn(string property) => GetErrors(property).Any();

    private sealed record Output(IReadOnlyList<Fact> Facts, IReadOnlyList<MatrixResult> Blocks);

    private static Output Compute(MatrixOperation operation, DenseMatrix<BigRational> a, DenseMatrix<BigRational>? b)
    {
        var facts = new List<Fact> { new("Shape", a.Shape.ToString(), $"{a.Rows * a.Columns} exact rational entries") };
        var blocks = new List<MatrixResult>();
        static string R(BigRational value) => value.ToString();
        MatrixDisplay Show(DenseMatrix<BigRational> m) => MatrixDisplay.From(m, R);
        static string G(double value) => Math.Abs(value) < 1e-12 ? "0" : value.ToString("0.#####", CultureInfo.InvariantCulture);

        switch (operation)
        {
            case MatrixOperation.RowReduce:
            {
                var reduction = Cas.RowReduce(a);
                var replayed = reduction.Replay(a).Equals(reduction.Reduced);
                blocks.Add(new("Reduced row echelon form", Show(reduction.Reduced)));
                facts.Add(new("Rank", reduction.Rank.ToString(CultureInfo.InvariantCulture), "the number of pivots"));
                facts.Add(new("Pivot columns", reduction.PivotColumns.Length == 0 ? "none" : string.Join(", ", reduction.PivotColumns.Select(c => c + 1)), "one-based"));
                facts.Add(new("Row operations", reduction.Operations.Length.ToString(CultureInfo.InvariantCulture), replayed ? "replaying them on A gives the same result ✓" : "replay does not match"));
                blocks.Add(new("The operations", Text: string.Join("\n", reduction.Operations.Select((op, i) => $"{i + 1,2}. {op}"))));
                break;
            }
            case MatrixOperation.Determinant:
            {
                var elimination = Cas.Determinant(a);
                var cofactor = Cas.Determinant(a, DeterminantMethod.Cofactor);
                facts.Add(new("det A", R(elimination), "Gaussian elimination, exact"));
                facts.Add(new("By cofactor expansion", R(cofactor), cofactor == elimination ? "agrees ✓" : "disagrees"));
                facts.Add(new("Invertible?", elimination != BigRational.Zero ? "yes" : "no, the determinant is zero"));
                break;
            }
            case MatrixOperation.Inverse:
            {
                if (Cas.Inverse(a) is Outcome<DenseMatrix<BigRational>>.Success { Value: var inverse })
                {
                    blocks.Add(new("A⁻¹", Show(inverse)));
                    facts.Add(new("Check", (a * inverse).Equals(DenseMatrix.Identity<BigRational>(a.Rows)) ? "A · A⁻¹ = I ✓" : "A · A⁻¹ ≠ I", "multiplied out exactly"));
                }
                else
                {
                    facts.Add(new("Inverse", "none: the matrix is singular", $"det A = {R(Cas.Determinant(a))}"));
                }

                break;
            }
            case MatrixOperation.Rank:
            {
                var rank = Cas.Rank(a);
                facts.Add(new("Rank", rank.ToString(CultureInfo.InvariantCulture)));
                facts.Add(new("Nullity", (a.Columns - rank).ToString(CultureInfo.InvariantCulture), "columns − rank"));
                break;
            }
            case MatrixOperation.NullSpace:
            {
                var basis = Cas.NullSpace(a);
                facts.Add(new("Dimension", basis.Length.ToString(CultureInfo.InvariantCulture)));
                blocks.Add(basis.Length == 0 ? new("Null space", Text: "only the zero vector") : new("Basis of the null space (columns)", MatrixDisplay.FromVectors(basis, R)));
                break;
            }
            case MatrixOperation.ColumnSpace:
            {
                var basis = Cas.ColumnSpace(a);
                facts.Add(new("Dimension", basis.Length.ToString(CultureInfo.InvariantCulture)));
                blocks.Add(basis.Length == 0 ? new("Column space", Text: "only the zero vector") : new("Basis of the column space (columns)", MatrixDisplay.FromVectors(basis, R)));
                break;
            }
            case MatrixOperation.Eigen:
            {
                if (Cas.Eigen(a) is Outcome<System.Collections.Immutable.ImmutableArray<EigenPair>>.Success { Value: var pairs })
                {
                    foreach (var pair in pairs)
                    {
                        var vectors = pair.Eigenvectors.Length == 0 ? "no exact eigenvector (irrational eigenvalue)" : string.Join("   ", pair.Eigenvectors.Select(v => v.ToString()));
                        facts.Add(new($"λ = {TextPrinter.Print(pair.Value, PrintOptions.Presentation)}", vectors, $"multiplicity {pair.Multiplicity}"));
                    }

                    if (pairs.IsEmpty) facts.Add(new("Eigenvalues", "none in the reals", "the characteristic polynomial has no real root"));
                }
                else
                {
                    facts.Add(new("Eigenvalues", "could not be found exactly"));
                }

                break;
            }
            case MatrixOperation.CharacteristicPolynomial:
            {
                var p = ExactLinearAlgebra.CharacteristicPolynomial(a);
                facts.Add(new("det(λI − A)", TextPrinter.Print(PolynomialConversion.FromPolynomial(p, new Symbol("lambda")), PrintOptions.Presentation), "an exact polynomial over the rationals"));
                break;
            }
            case MatrixOperation.SolveSystem:
            {
                if (b is null || b.Rows != a.Rows)
                {
                    facts.Add(new("A·x = b", "needs a valid vector b with one entry per row of A"));
                    break;
                }

                if (Cas.Inverse(a) is Outcome<DenseMatrix<BigRational>>.Success { Value: var inverse })
                {
                    var exact = inverse * b;
                    blocks.Add(new("x, exactly (x = A⁻¹ b)", Show(exact)));
                    facts.Add(new("Residual", (a * exact).Equals(b) ? "A · x = b ✓" : "A · x ≠ b"));
                }
                else
                {
                    facts.Add(new("A·x = b", "no unique solution: A is singular"));
                }

                var vector = DenseVector.Create(b.Rows, i => b[i, 0].ToDouble());
                var numeric = MatrixConversion.ToDouble(a).Solve(vector);
                if (numeric is Outcome<DenseVector<double>>.Success { Value: var x }) blocks.Add(new("x, by LU in double precision", MatrixDisplay.FromVector(x, G)));
                break;
            }
            case MatrixOperation.LuDecomposition:
            {
                var lu = MatrixConversion.ToDouble(a).Lu();
                facts.Add(new("Singular?", lu.IsSingular ? "yes" : "no"));
                facts.Add(new("det A", G(lu.Determinant), "from the product of the pivots and the permutation sign"));
                facts.Add(new("Residual", lu.Residual(MatrixConversion.ToDouble(a)).ToString("0.###E+0", CultureInfo.InvariantCulture), "‖A − Pᵀ L U‖ in the Frobenius norm"));
                blocks.Add(new("P (permutation)", MatrixDisplay.From(lu.PermutationMatrix, G)));
                blocks.Add(new("L (unit lower)", MatrixDisplay.From(lu.Lower, G)));
                blocks.Add(new("U (upper)", MatrixDisplay.From(lu.Upper, G)));
                break;
            }
            case MatrixOperation.QrDecomposition:
            {
                if (a.Rows < a.Columns)
                {
                    facts.Add(new("QR", "needs at least as many rows as columns"));
                    break;
                }

                var d = MatrixConversion.ToDouble(a);
                var qr = d.Qr();
                facts.Add(new("Rank deficient?", qr.IsRankDeficient ? "yes" : "no"));
                facts.Add(new("Residual", qr.Residual(d).ToString("0.###E+0", CultureInfo.InvariantCulture), "‖A − Q R‖"));
                blocks.Add(new("Q", MatrixDisplay.From(qr.Q, G)));
                blocks.Add(new("R", MatrixDisplay.From(qr.R, G)));
                break;
            }
            case MatrixOperation.Cholesky:
            {
                var d = MatrixConversion.ToDouble(a);
                var cholesky = d.Cholesky();
                facts.Add(new("Positive definite?", cholesky.IsPositiveDefinite ? "yes" : "no"));
                if (cholesky.IsPositiveDefinite)
                {
                    facts.Add(new("det A", G(cholesky.Determinant)));
                    facts.Add(new("Residual", cholesky.Residual(d).ToString("0.###E+0", CultureInfo.InvariantCulture), "‖A − L Lᵀ‖"));
                    blocks.Add(new("L", MatrixDisplay.From(cholesky.L, G)));
                }

                break;
            }
            case MatrixOperation.SymmetricEigen:
            {
                try
                {
                    var eigen = MatrixConversion.ToDouble(a).SymmetricEigen();
                    facts.Add(new("Eigenvalues", string.Join(",  ", eigen.Values.Select(G)), $"ascending; Jacobi rotations, {eigen.Sweeps} sweeps, {(eigen.Converged ? "converged" : "not converged")}"));
                    blocks.Add(new("Eigenvectors (columns, orthonormal)", MatrixDisplay.From(eigen.Vectors, G)));
                }
                catch (ArgumentException)
                {
                    facts.Add(new("Symmetric eigenvalues", "need a symmetric matrix: A must equal Aᵀ"));
                }

                break;
            }
        }

        return new Output(facts, blocks);
    }

    private static string Describe(MatrixOperation operation) => operation switch
    {
        MatrixOperation.RowReduce => "Gauss–Jordan elimination over the rationals, with every row operation recorded and replayable.",
        MatrixOperation.Determinant => "Exact determinant by elimination and by cofactor expansion; the square rule of MathMatrix applies.",
        MatrixOperation.Inverse => "Exact inverse, verified by multiplying it back; a singular matrix is a Failed outcome, not an exception.",
        MatrixOperation.Rank => "The number of pivots after exact row reduction.",
        MatrixOperation.NullSpace => "A basis of all x with A·x = 0.",
        MatrixOperation.ColumnSpace => "A basis of the span of the columns.",
        MatrixOperation.Eigen => "Real eigenvalues from the characteristic polynomial, exact where they are rational or quadratic, with their eigenspaces.",
        MatrixOperation.CharacteristicPolynomial => "det(λI − A) as an exact polynomial in λ.",
        MatrixOperation.SolveSystem => "Solves A·x = b exactly and, for comparison, by LU in double precision. Vector b is validated by its own MathMatrix.",
        MatrixOperation.LuDecomposition => "P·A = L·U with partial pivoting, in double precision.",
        MatrixOperation.QrDecomposition => "Householder QR in double precision; needs at least as many rows as columns.",
        MatrixOperation.Cholesky => "A = L·Lᵀ for a symmetric positive definite matrix.",
        MatrixOperation.SymmetricEigen => "Eigenvalues and orthonormal eigenvectors of a symmetric matrix by Jacobi rotations.",
        _ => string.Empty,
    };
}
