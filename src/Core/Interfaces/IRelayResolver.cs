namespace Relay.Core.Interfaces;

/// <summary>
/// Resolver for getting the appropriate relay based on context
/// </summary>
public interface IRelayResolver
{
    /// <summary>
    ///  Resolves a relay instance based on the provided key.
    /// </summary>
    /// <param name="context"></param>
    /// <typeparam name="TInterface"></typeparam>
    /// <returns></returns>
    TInterface Resolve<TInterface>(IRelayContext? context = null)
        where TInterface : class;
}
