namespace MathesisMauiApp.Services;

/// <summary>Every Shell route in one place: the content routes of the menu and the one pushed route.</summary>
public static class Routes
{
    public const string Home = "home";
    public const string Numbers = "numbers";
    public const string Expressions = "expressions";
    public const string Polynomials = "polynomials";
    public const string Matrices = "matrices";
    public const string Form = "form";
    public const string Playground = "playground";
    public const string Codes = "codes";
    public const string Algebra = "algebra";
    public const string Solve = "solve";
    public const string Calculus = "calculus";
    public const string Evaluate = "evaluate";
    public const string Roots = "roots";
    public const string Quadrature = "quadrature";
    public const string Ode = "ode";
    public const string Interpolation = "interpolation";
    public const string Catalog = "catalog";
    public const string Settings = "settings";

    /// <summary>The catalog entry page is pushed on top of the current page. Its name must not start with the route of a content page, or Shell reads it as a relative route to that page.</summary>
    public const string CatalogEntry = "catalogentry";

    public const string EntryIdKey = "id";
}
