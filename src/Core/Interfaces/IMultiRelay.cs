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
    /// <returns></returns>
    IEnumerable<TInterface> GetRelays();

    /// <summary>
    ///  Executes an operation on all relays and returns results.
    /// </summary>
    /// <param name="operation"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    Task<IEnumerable<TResult>> RelayToAllWithResults<TResult>(
        Func<TInterface, Task<TResult>> operation
    );

    /// <summary>
    ///  Executes an operation on all relays without returning results.
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    Task RelayToAll(Func<TInterface, Task> operation);

    /// <summary>
    ///  Gets the next relay based on the configured strategy.
    /// </summary>
    /// <returns></returns>
    Task<TInterface> GetNextRelay();
}
