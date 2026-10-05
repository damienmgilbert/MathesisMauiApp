using CommunityToolkit.Mvvm.ComponentModel;
using MathesisMauiApp.Models;
using MathesisMauiApp.Services;

namespace MathesisMauiApp.ViewModels;

/// <summary>
/// The base of every page ViewModel. It is an <see cref="ObservableValidator"/> so pages with forms can annotate their properties with the
/// Mathesis validation attributes and bind them like any other validated model; pages without a form simply never validate.
/// </summary>
public abstract partial class PageViewModel : ObservableValidator
{
    protected PageViewModel()
    {
        ErrorsChanged += (_, _) => OnValidationChanged();
    }

    /// <summary>True while an operation runs on a background thread.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>The outcome of the last operation, shown by an <c>OutcomeView</c>.</summary>
    [ObservableProperty]
    public partial OutcomeDisplay? Result { get; set; }

    /// <summary>The message of an unexpected exception from a background computation, or <c>null</c>. Mathesis returns outcomes instead of throwing, so this means a bug or API misuse; it is shown, not hidden.</summary>
    [ObservableProperty]
    public partial string? Problem { get; set; }

    /// <summary>Called whenever the validation errors of any property change; override it to refresh commands whose <c>CanExecute</c> depends on <see cref="ObservableValidator.HasErrors"/>.</summary>
    protected virtual void OnValidationChanged()
    {
    }

    /// <summary>Runs <paramref name="work"/> through <paramref name="debouncer"/>; an exception that is not a cancellation becomes <see cref="Problem"/> instead of vanishing in a discarded task.</summary>
    protected Task Debounced(Debouncer debouncer, TimeSpan delay, Func<CancellationToken, Task> work) =>
        debouncer.RunAsync(delay, async token =>
        {
            try
            {
                Problem = null;
                await work(token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                IsBusy = false;
                Problem = ex.GetType().Name + ": " + ex.Message;
            }
        });

    /// <summary>Runs <paramref name="work"/> off the UI thread, with <see cref="IsBusy"/> set, and returns its result.</summary>
    protected async Task<T> RunAsync<T>(Func<T> work)
    {
        IsBusy = true;
        try
        {
            return await Task.Run(work);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
