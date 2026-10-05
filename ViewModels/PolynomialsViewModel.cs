using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Printing;
using Mathesis.Symbolics.Representations;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// Polynomials in x with rational coefficients. <c>PolynomialExpression</c> decides what may be typed; the polynomial it lets through is then
/// converted to a <c>Polynomial&lt;BigRational&gt;</c> and analysed exactly, with the numeric roots found by Aberth's method.
/// </summary>
public sealed partial class PolynomialsViewModel : PageViewModel
{
    private static readonly Symbol X = new("x");

    private readonly ISettingsService _settings;
    private readonly IOutcomePresenter _presenter;
    private readonly Debouncer _debounce = new();

    public PolynomialsViewModel(ISettingsService settings, IOutcomePresenter presenter)
    {
        _settings = settings;
        _presenter = presenter;
        DivisionStyle = DivisionStyle.LongDivision;
        Dividend = "x^3 - 6x^2 + 11x - 6";
        Divisor = "x - 2";
    }

    public IReadOnlyList<DivisionStyle> DivisionStyles { get; } = Enum.GetValues<DivisionStyle>();

    public IReadOnlyList<Sample> Samples { get; } =
    [
        new("repeated root", "(x - 1)^2*(x + 2)"),
        new("cancels", "(x + 1)^4 - x^4"),
        new("irreducible", "x^2 + 1"),
        new("quintic", "x^5 - x - 1"),
        new("x^7 - 1", "x^7 - 1"),
        new("1/x", "1/x"),
        new("sqrt(x) + 1", "sqrt(x) + 1"),
        new("x*y", "x*y"),
        new("pi*x", "pi*x"),
        new("sin(x)", "sin(x)"),
        new("x^2 = 4", "x^2 = 4"),
        new("(x + 1)^65", "(x + 1)^65"),
    ];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Polynomial")]
    [Required]
    [PolynomialExpression("x", MaxDegree = 6)]
    public partial string? Dividend { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Divisor")]
    [Required]
    [PolynomialExpression("x", MaxDegree = 4)]
    public partial string? Divisor { get; set; }

    [ObservableProperty]
    public partial DivisionStyle DivisionStyle { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Analysis { get; set; }

    [ObservableProperty]
    public partial PlotModel? Plot { get; set; }

    [ObservableProperty]
    public partial OutcomeDisplay? Factorization { get; set; }

    [ObservableProperty]
    public partial OutcomeDisplay? Division { get; set; }

    [RelayCommand]
    private void UseSample(string text) => Dividend = text;

    partial void OnDividendChanged(string? value) => _ = Refresh();

    partial void OnDivisorChanged(string? value) => _ = Refresh();

    partial void OnDivisionStyleChanged(DivisionStyle value) => _ = Refresh();

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(250), async token =>
    {
        // Validation of the property runs after the change callback, so errors are read here, after the pause.
        var ok = !HasErrors && !string.IsNullOrWhiteSpace(Dividend) && !string.IsNullOrWhiteSpace(Divisor);
        if (!ok)
        {
            Analysis = null;
            Plot = null;
            Factorization = null;
            Division = null;
            return;
        }

        var (dividend, divisor, style) = (Dividend!, Divisor!, DivisionStyle);
        var context = _settings.CreateContext();
        var work = await Task.Run(() => Compute(dividend, divisor, style, context, token), token);
        Analysis = work.Facts;
        Plot = work.Plot;
        Factorization = work.Factor is null ? null : _presenter.PresentExpr(work.Factor, "Factored form");
        Division = work.Division is null ? null : _presenter.Present(work.Division, d => $"quotient  {Pretty(d.Quotient)}\nremainder {Pretty(d.Remainder)}", heading: "Division");
    });

    private sealed record Computation(IReadOnlyList<Fact> Facts, PlotModel? Plot, Outcome<Expr>? Factor, Outcome<DivisionResult>? Division);

    private Computation Compute(string dividendText, string divisorText, DivisionStyle style, MathContext context, CancellationToken token)
    {
        var dividend = Expr.Parse(dividendText);
        var divisor = Expr.Parse(divisorText);
        if (!PolynomialConversion.TryToPolynomial(Normalizer.Canonical(dividend), X, out var p)) return new Computation([], null, null, null);

        var facts = new List<Fact>
        {
            new("Expanded", Pretty(PolynomialConversion.FromPolynomial(p, X)), "PolynomialConversion.TryToPolynomial, then FromPolynomial"),
            new("Degree", p.IsZero ? "−∞ (the zero polynomial)" : p.Degree.ToString(CultureInfo.InvariantCulture), p.IsZero ? null : $"leading coefficient {p.LeadingCoefficient}"),
            new("Coefficients", p.IsZero ? "(none)" : string.Join(", ", p.Coefficients), "ascending: constant term first, all exact BigRational"),
        };

        PlotModel? plot = null;
        if (!p.IsZero && p.Degree >= 1)
        {
            facts.Add(new("Derivative", Pretty(PolynomialConversion.FromPolynomial(p.Derivative(), X))));

            var rational = PolynomialAlgorithms.RationalRoots(p);
            facts.Add(new("Rational roots", rational.Roots.Length == 0 ? "none" : string.Join(", ", rational.Roots.Select(r => r.Multiplicity > 1 ? $"{r.Root} (×{r.Multiplicity})" : r.Root.ToString())),
                rational.Complete ? "all roots are rational" : "the cofactor has no further rational roots"));

            var squareFree = PolynomialAlgorithms.SquareFree(p);
            facts.Add(new("Square-free parts", string.Join("  ·  ", squareFree.Factors.Select(f => f.Multiplicity > 1 ? $"({Pretty(PolynomialConversion.FromPolynomial(f.Factor, X))})^{f.Multiplicity}" : $"({Pretty(PolynomialConversion.FromPolynomial(f.Factor, X))})")),
                $"content {squareFree.Content}; gcd(p, p′) = {Pretty(PolynomialConversion.FromPolynomial(PolynomialAlgorithms.Gcd(p, p.Derivative()), X))}"));

            var numeric = p.Map(c => c.ToDouble());
            var roots = PolynomialAlgorithms.AberthRoots(numeric);
            facts.Add(new("All roots (numeric)", string.Join(",  ", roots.Roots.Select(Describe)), roots.Converged ? "Aberth's method, converged; complex roots come in conjugate pairs" : "Aberth's method did not converge"));

            var real = roots.Roots.Where(r => Math.Abs(r.Imaginary) <= 1e-7 * (1 + Math.Abs(r.Real))).Select(r => r.Real).ToArray();
            var bound = Math.Clamp(1.2 * (real.Length == 0 ? 2 : real.Max(Math.Abs) + 1), 2, 100);
            plot = new PlotModel
            {
                Title = "p(x)",
                Series = [new PlotSeries("p(x)", PlotModel.Sample(numeric.Evaluate, -bound, bound))],
                Markers = [.. real.Select(r => new PlotMarker(r, 0, r.ToString("0.###", CultureInfo.InvariantCulture)))],
            };
        }

        token.ThrowIfCancellationRequested();
        var factor = Cas.Factor(dividend, context, _settings.CreateBudget());

        Outcome<DivisionResult>? division = null;
        if (PolynomialConversion.TryToPolynomial(Normalizer.Canonical(divisor), X, out var q) && !q.IsZero)
        {
            division = Cas.Divide(dividend, divisor, X, style);
            var (quotient, remainder) = p.DivRem(q);
            var holds = (quotient * q + remainder).Equals(p);
            facts.Add(new("p ÷ divisor", $"quotient {Pretty(PolynomialConversion.FromPolynomial(quotient, X))}, remainder {Pretty(PolynomialConversion.FromPolynomial(remainder, X))}",
                holds ? "Polynomial.DivRem; quotient·divisor + remainder = p ✓" : "check failed"));
        }

        return new Computation(facts, plot, factor, division);
    }

    private static string Pretty(Expr e) => TextPrinter.Print(e, PrintOptions.Presentation);

    private static string Describe(Complex<double> z)
    {
        var re = Math.Abs(z.Real) < 1e-9 ? 0 : z.Real;
        var im = Math.Abs(z.Imaginary) < 1e-9 ? 0 : z.Imaginary;
        var real = re.ToString("0.#####", CultureInfo.InvariantCulture);
        return im == 0 ? real : $"{real} {(im < 0 ? "−" : "+")} {Math.Abs(im).ToString("0.#####", CultureInfo.InvariantCulture)}i";
    }
}
