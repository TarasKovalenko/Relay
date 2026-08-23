using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

/// <inheritdoc cref="IRelayScopeRunner" />
public sealed class RelayScopeRunner(IServiceScopeFactory scopeFactory) : IRelayScopeRunner
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public void Run<TService>(Action<TService> action)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = _scopeFactory.CreateScope();
        action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public TResult Run<TService, TResult>(Func<TService, TResult> func)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(func);

        using var scope = _scopeFactory.CreateScope();
        return func(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task RunAsync<TService>(
        Func<TService, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = _scopeFactory.CreateAsyncScope();
        await operation(
                scope.ServiceProvider.GetRequiredService<TService>(),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<TResult> RunAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = _scopeFactory.CreateAsyncScope();
        return await operation(
                scope.ServiceProvider.GetRequiredService<TService>(),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task RunAsync(
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = _scopeFactory.CreateAsyncScope();
        await operation(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }
}
