namespace FlowBoard.Web.Utilities;

public sealed class DebouncedAction : IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    public async Task ExecuteAsync(
        Func<Task> action,
        int delayMilliseconds = 300)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        var cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource = cancellationTokenSource;

        try
        {
            await Task.Delay(
                delayMilliseconds,
                cancellationTokenSource.Token);

            if (!cancellationTokenSource.IsCancellationRequested)
            {
                await action();
            }
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            // A newer search replaced this operation.
        }
        finally
        {
            if (ReferenceEquals(
                _cancellationTokenSource,
                cancellationTokenSource))
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }

    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}