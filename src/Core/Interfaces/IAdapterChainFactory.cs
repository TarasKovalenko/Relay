namespace Relay.Core.Interfaces;

/// <summary>
/// Factory for creating adapter chains by name
/// </summary>
public interface IAdapterChainFactory<TTarget>
    where TTarget : class
{
    /// <summary>
    ///  Creates an instance of the target type from a named chain.
    /// </summary>
    /// <param name="chainName"></param>
    /// <returns></returns>
    TTarget CreateFromChain(string chainName);

    /// <summary>
    ///  Returns a list of available adapter chain names.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetAvailableChains();
}
