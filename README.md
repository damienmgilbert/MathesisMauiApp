# Mathesis Showcase

A .NET MAUI app that shows what the [Mathesis](https://github.com/damienmgilbert/Mathesis) library can do, and above all how its
`System.ComponentModel.DataAnnotations` attributes (`Mathesis.Validation`) turn a text box into a place where only meaningful
mathematics can be entered.

It targets Windows, Android, iOS and Mac Catalyst (the Windows build is what has been run; the Android build compiles). The UI is XAML with
MVVM: [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) for view models and validation,
[CommunityToolkit.Maui](https://learn.microsoft.com/dotnet/communitytoolkit/maui/) for converters, the `Expander` and toasts, and
`Microsoft.Maui.Graphics` for the plots. Nothing else.

## Pages

| Group | Page | What it shows |
| --- | --- | --- |
| Data annotations | Numbers | `RationalNumber` (`IntegerOnly`, `AllowFractions`, `AllowDecimals`), `ExactRange` (exclusive bounds), `NonZero`; edge cases such as `0,5`, `1/0`, `1e999999999`; typed values (`double`, `decimal`, `BigRational`) |
| | Expressions | `MathExpression` and `MathEquation`: `Shape`, `Variables`, `RequiredVariables`, `DisallowedFamilies`, `WarningsAreErrors`, `CheckSorts`, `SingleLetterVariables`, LaTeX input; an inspector (kind, free variables, families, sort, JSON) |
| | Polynomials | `PolynomialExpression` (`MaxDegree`, cancellation honoured), then exact analysis: roots, square-free parts, division, factoring, a plot |
| | Matrices | `MathMatrix` (`Rows`, `Columns`, `Square`, `NumericEntries`, `MaxDimension`), then exact row reduction, determinant, inverse, null space, eigenvalues and the LU, QR, Cholesky and Jacobi factorizations |
| | Form demo | All seven attributes on one `ObservableValidator`, two attributes on one property, `IValidatableObject` across properties, a submit summary |
| | Playground | Any attribute built at run time from its options, handed text or a typed value; the verdict, the generated C#, `GetConfigurationError()` and API misuse |
| | Error codes | All 22 `MathValidationCode` values, each with an input that raises it (editable) |
| Computer algebra | Algebra, Solve, Calculus | `Cas.Simplify`…`ExpToTrig`, `Solve` (equations, inequalities, systems, `NSolve`), derivatives, integrals, limits, series; steps in text, Markdown or LaTeX, provisos, verification status, the catalog laws each step cites |
| | Evaluate | One expression in `BigRational`, `double`, `Complex<double>`, `Interval<double>` and `Dual<double>` |
| Numerics | Roots & minima, Quadrature, ODE solver, Interpolation | `Roots`, `Minimize`, `Quadrature`, `FiniteDifferences`, `OdeSolver`, `Interpolate`, each compared with the exact answer where there is one |
| Knowledge | Catalog, Settings | The 548-entry catalog of laws (search, filters, an entry page that applies the law); `MathContext` (number field, assumptions, curriculum level), `Budget` and explanation settings |

Every input that goes into Mathesis is validated by a Mathesis attribute first, so the pages double as examples of the attributes in
realistic forms.

## How the validation is wired

```csharp
[ObservableProperty]
[NotifyDataErrorInfo]
[Display(Name = "Probability")]
[ExactRange("0", "1")]
public partial string? Probability { get; set; }
```

* `ObservableValidator` runs the attributes and keeps the `MathValidationResult` objects, with their `Code`, `Span` and `Suggestion`.
* `ValidatedInput` (a `ContentView`) binds a text box to such a property and a `FeedbackView` shows the first failure: the code, the
  message, the part of the text the parser blames (underlined) and the suggestion. `FeedbackView` also works on a plain
  `ValidationResult`, which is how the Playground and the error-code gallery use it.
* Calculator pages compute only for valid input, off the UI thread, behind a `Debouncer`, with a `Budget` that can be cancelled.

## Structure

```text
Services/      settings (IPreferences), navigation, feedback, outcome presenter, expression compilation, debouncer
Models/        OutcomeDisplay, plot and matrix models, small records for tables
ViewModels/    one view model per page; PageViewModel is an ObservableValidator
Views/         the pages, the menu
Controls/      ValidatedInput, FeedbackView, OutcomeView, PlotView, MatrixView, FactTable, VerdictTable, ExampleChips, ...
```

Pages and view models are transient services, resolved by Shell; platform APIs (`IPreferences`, `IClipboard`, `IDeviceInfo`) are
registered behind their interfaces; view models never touch Shell or statics directly. The one pushed route (the catalog entry) receives
its ID through `IQueryAttributable` and unescapes it explicitly.

## Building

The app references the published Mathesis assemblies by path (`..\Mathesis\src\Mathesis\bin\Release\net10.0\publish`). Publish Mathesis
first:

```text
dotnet publish ..\Mathesis\src\Mathesis -c Release
dotnet build -f net10.0-windows10.0.19041.0
```

`System.Numerics.Tensors` 10.0.12 is a package reference on purpose: the Windows App SDK brings 9.0.0, which would win over a plain file
reference and break Mathesis at run time.

In a Debug build a script can start on a page and in a theme with the environment variables `MATHESIS_ROUTE` (`numbers`, `solve`,
`catalog`, ...) and `MATHESIS_THEME` (`light` or `dark`). On Windows an unhandled exception is also written to
`%TEMP%\mathesis-crash.log`.

## Notes

* LaTeX is shown as source (and copyable); nothing in the app renders it.
* `ObservableValidator` finds attributes by reflection, so this app is not trim-safe in that respect. For NativeAOT use
  `attribute.Check(value, displayName)` or `GetValidationResult` with an explicit `ValidationContext`, as `samples/AotSmoke` in Mathesis does.
