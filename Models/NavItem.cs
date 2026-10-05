using CommunityToolkit.Mvvm.ComponentModel;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.Models;

/// <summary>One page in the menu and on the home page. <see cref="Glyph"/> is a plain text symbol, so the app needs no icon assets.</summary>
public sealed partial class NavItem(string title, string glyph, string route, string summary) : ObservableObject
{
    public string Title { get; } = title;

    public string Glyph { get; } = glyph;

    public string Route { get; } = route;

    public string Summary { get; } = summary;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed record NavGroup(string Title, string Summary, IReadOnlyList<NavItem> Items);

/// <summary>The pages of the app by group. Each call creates new items, because selection is per menu.</summary>
public static class NavigationCatalog
{
    public static IReadOnlyList<NavGroup> Create() =>
    [
        new("Data annotations", "Seven validation attributes that check mathematical input as it is typed.",
        [
            new("Numbers", "½", Routes.Numbers, "RationalNumber, ExactRange and NonZero compare exactly, never with doubles."),
            new("Expressions", "ƒ", Routes.Expressions, "MathExpression and MathEquation: shape, variables, function families, LaTeX."),
            new("Polynomials", "x²", Routes.Polynomials, "PolynomialExpression: one variable, rational coefficients, a maximum degree."),
            new("Matrices", "▦", Routes.Matrices, "MathMatrix: dimensions, squareness and numeric entries, then exact linear algebra."),
            new("Form demo", "✎", Routes.Form, "All seven attributes on one model, with IValidatableObject and a submit summary."),
            new("Playground", "◈", Routes.Playground, "Choose an attribute, set its options and watch the verdict, code and C#."),
            new("Error codes", "#", Routes.Codes, "Every MathValidationCode with an input that triggers it."),
        ]),
        new("Computer algebra", "Exact results with the steps that led to them and the laws they cite.",
        [
            new("Algebra", "∑", Routes.Algebra, "Simplify, expand, factor, cancel, trigonometric and logarithmic forms."),
            new("Solve", "=", Routes.Solve, "Equations, inequalities and systems, with extraneous roots rejected."),
            new("Calculus", "∫", Routes.Calculus, "Derivatives, integrals, limits and series, checked by the opposite operation."),
            new("Evaluate", "≈", Routes.Evaluate, "One expression in exact, double, complex, interval and dual numbers."),
        ]),
        new("Numerics", "Root finding, quadrature, differentiation, ODEs and interpolation.",
        [
            new("Roots & minima", "√", Routes.Roots, "Bisection, Brent, Newton (symbolic, numeric and automatic), secant, golden section, Nelder–Mead."),
            new("Quadrature", "∬", Routes.Quadrature, "Integrals and derivatives by six methods, compared with the exact answer."),
            new("ODE solver", "y′", Routes.Ode, "Runge–Kutta 4 and Dormand–Prince with dense output."),
            new("Interpolation", "∿", Routes.Interpolation, "Newton, barycentric, Chebyshev and cubic splines."),
        ]),
        new("Knowledge", "The catalog of verified laws behind every step.",
        [
            new("Catalog", "§", Routes.Catalog, "Search and browse the laws; apply any of them to your own expression."),
            new("Settings", "⚙", Routes.Settings, "Number field, assumptions, curriculum level, budget and theme."),
        ]),
    ];
}
