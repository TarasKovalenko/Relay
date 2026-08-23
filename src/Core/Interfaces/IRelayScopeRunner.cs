namespace Relay.Core.Interfaces;

/// <summary>
/// Runs work inside a fresh dependency injection scope.
/// </summary>
/// <remarks>
/// Removes the most common lifetime mistake in .NET applications: reaching for a scoped
/// service (a <c>DbContext</c>, a unit of work, a request-bound accessor) from a singleton
/// such as a hosted service, a background queue consumer or a cache warmer. Instead of
/// capturing the scoped instance, ask the runner for one and let it dispose the scope.
/// </remarks>
public interface IRelayScopeRunner
{
    /// <summary>
    /// Creates a scope, resolves <typeparamref name="TService"/> and runs <paramref name="action"/>.
    /// </summary>
    void Run<TService>(Action<TService> action)
        where TService : notnull;

    /// <summary>
    /// Creates a scope, resolves <typeparamref name="TService"/> and returns the result of
    /// <paramref name="func"/>. Do not return the scoped service itself: it is disposed with the scope.
    /// </summary>
    TResult Run<TService, TResult>(Func<TService, TResult> func)
        where TService : notnull;

    /// <summary>
    /// Creates an async scope, resolves <typeparamref name="TService"/> and awaits <paramref name="operation"/>.
    /// </summary>
    Task RunAsync<TService>(
        Func<TService, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull;

    /// <summary>
    /// Creates an async scope, resolves <typeparamref name="TService"/> and awaits
    /// <paramref name="operation"/>, returning its result.
    /// </summary>
    Task<TResult> RunAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull;

    /// <summary>
    /// Creates an async scope and awaits <paramref name="operation"/> with the scoped
    /// <see cref="IServiceProvider"/>, for work that needs more than one scoped service.
    /// </summary>
    Task RunAsync(
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    );
}
