namespace Relay.Core.Interfaces;

/// <summary>
/// Multi-relay for broadcasting or routing to multiple implementations
/// </summary>
public interface IMultiRelay<TInterface>
    where TInterface : class
{
    /// <summary>
    ///  Gets all registered relays of the specified interface type.
    /// </summary>
    IEnumerable<TInterface> GetRelays();

    /// <summary>
    ///  Executes an operation on all relays and returns results.
    /// </summary>
    Task<IEnumerable<TResult>> RelayToAllWithResults<TResult>(
        Func<TInterface, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///  Executes an operation on all relays without returning results.
    /// </summary>
    Task RelayToAll(
        Func<TInterface, Task> operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///  Gets the next relay based on the configured strategy.
    /// </summary>
    Task<TInterface> GetNextRelay();
}
