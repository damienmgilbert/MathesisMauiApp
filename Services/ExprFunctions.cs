using Mathesis;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Evaluation;

namespace MathesisMauiApp.Services;

/// <summary>Turns expressions into plain functions for the numerical methods and the plots, through <c>Compile&lt;double&gt;</c> (no reflection, no expression trees).</summary>
public static class ExprFunctions
{
    /// <summary>The expression as a function of <paramref name="x"/>, or <c>null</c> when it has another free symbol or an operator that cannot be compiled.</summary>
    public static Func<double, double>? Of(Expr expr, Symbol x)
    {
        if (expr.Compile<double>(x) is not Outcome<CompiledExpr<double>>.Success { Value: var code }) return null;
        return value => code.Invoke(value);
    }

    /// <summary>The expression as a function of two symbols.</summary>
    public static Func<double, double, double>? Of(Expr expr, Symbol x, Symbol y)
    {
        if (expr.Compile<double>(x, y) is not Outcome<CompiledExpr<double>>.Success { Value: var code }) return null;
        return (a, b) => code.Invoke(a, b);
    }

    /// <summary>The expression as a function of three symbols.</summary>
    public static Func<double, double, double, double>? Of(Expr expr, Symbol x, Symbol y, Symbol z)
    {
        if (expr.Compile<double>(x, y, z) is not Outcome<CompiledExpr<double>>.Success { Value: var code }) return null;
        return (a, b, c) => code.Invoke(a, b, c);
    }

    /// <summary>The value of a constant expression such as <c>pi/2</c> or <c>-oo</c> as a double; infinities are returned as infinities.</summary>
    public static double? Constant(Expr expr)
    {
        if (expr is Constant { Id: ConstantId.PositiveInfinity }) return double.PositiveInfinity;
        if (expr is Constant { Id: ConstantId.NegativeInfinity }) return double.NegativeInfinity;
        if (expr is Apply { Operator.Id: "neg", Arguments: [Constant { Id: ConstantId.PositiveInfinity }] }) return double.NegativeInfinity;
        return Cas.N(expr) is Outcome<double>.Success { Value: var value } ? value : null;
    }

    /// <summary>Parses text and returns its constant value, or <c>null</c> when it does not parse or has free symbols.</summary>
    public static double? Constant(string? text) => !string.IsNullOrWhiteSpace(text) && Expr.TryParse(text).Expr is { } expr ? Constant(expr) : null;
}
