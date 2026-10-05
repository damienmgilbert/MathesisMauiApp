namespace MathesisMauiApp.Services;

/// <summary>
/// Runs the latest request after a pause and abandons the ones it replaced: typing fast starts many requests, only the last one that
/// survives the delay does any work.
/// </summary>
public sealed class Debouncer
{
    private CancellationTokenSource? _current;

    public async Task RunAsync(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        _current?.Cancel();
        _current?.Dispose();
        var source = _current = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, source.Token);
            await action(source.Token);
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer request, or cancelled by the budget of the work itself.
        }
    }
}
