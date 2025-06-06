namespace Relay.Core.Interfaces;

/// <summary>
/// Factory for creating relays by key or context
/// </summary>
public interface IRelayFactory<TInterface>
    where TInterface : class
{
    /// <summary>
    ///  Creates a relay instance based on the provided key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    TInterface CreateRelay(string key);

    /// <summary>
    ///  Creates a relay instance based on the provided context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    TInterface CreateRelay(IRelayContext context);

    /// <summary>
    ///  Gets the default relay instance configured for the application.
    /// </summary>
    /// <returns></returns>
    TInterface GetDefaultRelay();

    /// <summary>
    ///  Returns a list of available relay keys that can be used to create relays.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetAvailableKeys();
}
