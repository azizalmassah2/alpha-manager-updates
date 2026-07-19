namespace Lux.Platform.Abstractions.Interfaces;

/// <summary>
/// Provides a platform-agnostic way to marshal execution back to the main UI thread.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Executes the specified action synchronously on the UI thread.
    /// </summary>
    void Invoke(Action action);

    /// <summary>
    /// Executes the specified action asynchronously on the UI thread.
    /// </summary>
    Task InvokeAsync(Action action);

    /// <summary>
    /// Executes the specified function asynchronously on the UI thread and returns the result.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<T> function);
}
