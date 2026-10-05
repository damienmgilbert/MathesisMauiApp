using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mathesis.Numerics;
using Mathesis.Numerics.Ode;
using Mathesis.Symbolics;
using Mathesis.Validation;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// An initial value problem y′ = f(t, y), or a system of two equations y′ = f(t, y, z), z′ = g(t, y, z), solved by classical Runge–Kutta (fixed steps)
/// and by Dormand–Prince (adaptive steps with dense output), and compared with a closed-form solution when one is given.
/// </summary>
public sealed partial class OdeViewModel : PageViewModel
{
    private static readonly Symbol T = new("t");
    private static readonly Symbol Y = new("y");
    private static readonly Symbol Z = new("z");

    private readonly Debouncer _debounce = new();

    private sealed record Preset(string Label, string Dy, string Dz, string Y0, string Z0, string T1, string Exact);

    private static readonly Preset[] Presets =
    [
        new("decay", "-2*y", "", "1", "0", "3", "exp(-2*t)"),
        new("logistic", "y*(1 - y)", "", "0.1", "0", "10", "1/(1 + 9*exp(-t))"),
        new("forced", "sin(t) - y", "", "0", "0", "12", "(sin(t) - cos(t) + exp(-t))/2"),
        new("oscillator (system)", "z", "-y", "1", "0", "12", "cos(t)"),
        new("damped oscillator", "z", "-y - z/5", "1", "0", "20", ""),
        new("stiff", "-50*(y - cos(t))", "", "0", "0", "2", ""),
    ];

    public OdeViewModel()
    {
        T0 = "0";
        Steps = "30";
        RelativeTolerance = "1e-6";
        AbsoluteTolerance = "1e-9";
        Apply(Presets[0]);
    }

    public IReadOnlyList<Sample> PresetSamples { get; } = [.. Presets.Select((p, i) => new Sample(p.Label, i.ToString(CultureInfo.InvariantCulture)))];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "y′")]
    [Required]
    [MathExpression(Variables = ["t", "y", "z"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.Set | OperatorFamily.LinearAlgebra)]
    public partial string? Dy { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "z′")]
    [MathExpression(Variables = ["t", "y", "z"], DisallowedFamilies = OperatorFamily.Calculus | OperatorFamily.Set | OperatorFamily.LinearAlgebra)]
    public partial string? Dz { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Start time")]
    [Required]
    [ExactRange("-1000", "1000")]
    public partial string? T0 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "End time")]
    [Required]
    [ExactRange("-1000", "1000")]
    public partial string? T1 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "y(start)")]
    [Required]
    [ExactRange("-1000000", "1000000")]
    public partial string? Y0 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "z(start)")]
    [ExactRange("-1000000", "1000000")]
    public partial string? Z0 { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Runge–Kutta steps")]
    [Required]
    [RationalNumber(IntegerOnly = true)]
    [ExactRange("1", "100000")]
    public partial string? Steps { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Relative tolerance")]
    [Required]
    [ExactRange("0", "1", MinimumIsExclusive = true)]
    public partial string? RelativeTolerance { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Absolute tolerance")]
    [Required]
    [ExactRange("0", "1", MinimumIsExclusive = true)]
    public partial string? AbsoluteTolerance { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Display(Name = "Exact y(t)")]
    [MathExpression(Variables = ["t"])]
    public partial string? Exact { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Fact>? Facts { get; set; }

    [ObservableProperty]
    public partial PlotModel? Plot { get; set; }

    [RelayCommand]
    private void UsePreset(string index) => Apply(Presets[int.Parse(index, CultureInfo.InvariantCulture)]);

    partial void OnDyChanged(string? value) => _ = Refresh();

    partial void OnDzChanged(string? value) => _ = Refresh();

    partial void OnT0Changed(string? value) => _ = Refresh();

    partial void OnT1Changed(string? value) => _ = Refresh();

    partial void OnY0Changed(string? value) => _ = Refresh();

    partial void OnZ0Changed(string? value) => _ = Refresh();

    partial void OnStepsChanged(string? value) => _ = Refresh();

    partial void OnRelativeToleranceChanged(string? value) => _ = Refresh();

    partial void OnAbsoluteToleranceChanged(string? value) => _ = Refresh();

    partial void OnExactChanged(string? value) => _ = Refresh();

    private void Apply(Preset preset)
    {
        Dy = preset.Dy;
        Dz = preset.Dz;
        Y0 = preset.Y0;
        Z0 = preset.Z0;
        T1 = preset.T1;
        Exact = preset.Exact;
    }

    private Task Refresh() => Debounced(_debounce, TimeSpan.FromMilliseconds(300), async token =>
    {
        if (HasErrors || string.IsNullOrWhiteSpace(Dy) || string.IsNullOrWhiteSpace(T0) || string.IsNullOrWhiteSpace(T1) || string.IsNullOrWhiteSpace(Y0))
        {
            Facts = null;
            Plot = null;
            return;
        }

        var input = (Dy!, Dz ?? string.Empty, T0!, T1!, Y0!, Z0 ?? string.Empty, Steps ?? "30", RelativeTolerance ?? "1e-6", AbsoluteTolerance ?? "1e-9", Exact ?? string.Empty);
        var work = await RunAsync(() => Compute(input));
        if (token.IsCancellationRequested) return;
        Facts = work.Facts;
        Plot = work.Plot;
    });

    private sealed record Work(IReadOnlyList<Fact> Facts, PlotModel? Plot);

    private static Work Compute((string Dy, string Dz, string T0, string T1, string Y0, string Z0, string Steps, string RelTol, string AbsTol, string Exact) input)
    {
        static string G(double value) => value.ToString("G10", CultureInfo.InvariantCulture);
        var f = ExprFunctions.Of(Expr.Parse(input.Dy), T, Y, Z);
        var system = !string.IsNullOrWhiteSpace(input.Dz);
        var g = system ? ExprFunctions.Of(Expr.Parse(input.Dz), T, Y, Z) : null;
        var t0 = ExprFunctions.Constant(input.T0) ?? 0;
        var t1 = ExprFunctions.Constant(input.T1) ?? 1;
        var y0 = ExprFunctions.Constant(input.Y0) ?? 0;
        var z0 = ExprFunctions.Constant(input.Z0) ?? 0;
        if (f is null || (system && g is null) || t1 <= t0) return new Work([new("Problem", t1 <= t0 ? "the end time must be after the start time" : "the equations cannot be compiled")], null);

        var start = system ? new[] { y0, z0 } : new[] { y0 };
        OdeFunction<double> rhs = (t, y, dydt) =>
        {
            var z = system ? y[1] : 0;
            dydt[0] = f(t, y[0], z);
            if (system) dydt[1] = g!(t, y[0], z);
        };

        var steps = int.Parse(input.Steps.Trim(), CultureInfo.InvariantCulture);
        var options = new OdeOptions(RelativeTolerance: NumberInputs.ReadDouble(input.RelTol), AbsoluteTolerance: NumberInputs.ReadDouble(input.AbsTol));
        var rk4 = OdeSolver.Rk4(rhs, t0, start, t1, steps);
        var dp = OdeSolver.DormandPrince(rhs, t0, start, t1, options);
        var exact = string.IsNullOrWhiteSpace(input.Exact) ? null : ExprFunctions.Of(Expr.Parse(input.Exact), T);

        var facts = new List<Fact>
        {
            new("Runge–Kutta 4", $"y({G(t1)}) = {G(rk4.FinalState[0])}", $"{steps} fixed steps, {rk4.Evaluations} evaluations"),
            new("Dormand–Prince", $"y({G(t1)}) = {G(dp.FinalState[0])}", $"{dp.AcceptedSteps} accepted and {dp.RejectedSteps} rejected steps, {dp.Evaluations} evaluations, {(dp.Converged ? "converged" : dp.Reason.ToString())}"),
        };
        if (system) facts.Add(new("z at the end", $"RK4 {G(rk4.FinalState[1])},  Dormand–Prince {G(dp.FinalState[1])}"));
        if (exact is not null)
        {
            var truth = exact(t1);
            facts.Add(new("Exact y(end)", G(truth), $"error of RK4 {Math.Abs(rk4.FinalState[0] - truth):0.##E+0}, of Dormand–Prince {Math.Abs(dp.FinalState[0] - truth):0.##E+0}"));
        }

        var middle = t0 + (t1 - t0) / 3;
        if (dp.Dense is not null) facts.Add(new($"Dense output at t = {G(middle)}", string.Join(",  ", dp.Evaluate(middle).Select(G)), "Evaluate(t) interpolates between the accepted steps, no new solve"));

        var series = new List<PlotSeries>();
        if (exact is not null) series.Add(new PlotSeries("exact", PlotModel.Sample(exact, t0, t1), SeriesStyle.Line, 5));
        series.Add(new PlotSeries(system ? "y (Dormand–Prince)" : "Dormand–Prince", PlotModel.Sample(tt => dp.Evaluate(tt)[0], t0, t1, 300), SeriesStyle.Line, 0));
        if (system) series.Add(new PlotSeries("z (Dormand–Prince)", PlotModel.Sample(tt => dp.Evaluate(tt)[1], t0, t1, 300), SeriesStyle.Line, 2));
        series.Add(new PlotSeries("RK4 steps", [.. rk4.Times.Select((time, i) => new PlotPoint(time, rk4.States[i][0]))], SeriesStyle.Points, 1));

        // Scale the plot to the accurate solution: a fixed-step method that diverges (RK4 on a stiff problem) must not flatten it.
        var reference = series.Where(p => p.Style == SeriesStyle.Line).SelectMany(p => p.Points).Select(p => p.Y).Where(double.IsFinite).ToList();
        var low = reference.Count == 0 ? -1 : reference.Min();
        var high = reference.Count == 0 ? 1 : reference.Max();
        var pad = Math.Max((high - low) * 0.1, 1e-9);
        if (Math.Abs(rk4.FinalState[0] - dp.FinalState[0]) > 100 * (1 + Math.Abs(dp.FinalState[0]))) facts.Add(new("RK4 diverged", "the fixed step is too large for this problem", "the problem is stiff: Dormand–Prince shrinks its step, Runge–Kutta 4 with 30 steps does not"));
        return new Work(facts, new PlotModel { Title = "Solution", XLabel = "t", Series = series, XMin = t0, XMax = t1, YMin = low - pad, YMax = high + pad });
    }
}
