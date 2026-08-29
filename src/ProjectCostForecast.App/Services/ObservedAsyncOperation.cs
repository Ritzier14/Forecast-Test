namespace ProjectCostForecast.App.Services;

public enum ObservedAsyncOperationStatus
{
    Completed,
    Canceled,
    Failed
}

public sealed record ObservedAsyncOperationResult(
    ObservedAsyncOperationStatus Status,
    Exception? Exception = null);

/// <summary>
/// Provides an observed task boundary for UI-owned asynchronous work. Expected
/// cancellation is kept separate from failures, and the failure callback is
/// isolated so diagnostics cannot create a second unobserved exception.
/// </summary>
public static class ObservedAsyncOperation
{
    public static async Task<ObservedAsyncOperationResult> RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            await operation(cancellationToken);
            return new ObservedAsyncOperationResult(ObservedAsyncOperationStatus.Completed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ObservedAsyncOperationResult(ObservedAsyncOperationStatus.Canceled);
        }
        catch (Exception exception)
        {
            try
            {
                onFailure?.Invoke(exception);
            }
            catch
            {
                // Diagnostics and other failure observers must not escape this boundary.
            }

            return new ObservedAsyncOperationResult(
                ObservedAsyncOperationStatus.Failed,
                exception);
        }
    }
}
