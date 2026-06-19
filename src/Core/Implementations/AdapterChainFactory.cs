using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

/// <summary>
/// Factory that resolves and executes one of several named adapter chains, all producing
/// the same <typeparamref name="TTarget"/>.
/// </summary>
public sealed class AdapterChainFactory<TTarget>(
    IReadOnlyDictionary<string, Func<IServiceProvider, TTarget>> chains,
    IServiceProvider provider
) : IAdapterChainFactory<TTarget>
    where TTarget : class
{
    private readonly IReadOnlyDictionary<string, Func<IServiceProvider, TTarget>> _chains =
        chains ?? throw new ArgumentNullException(nameof(chains));

    private readonly IServiceProvider _provider =
        provider ?? throw new ArgumentNullException(nameof(provider));

    public TTarget CreateFromChain(string chainName)
    {
        if (string.IsNullOrEmpty(chainName))
        {
            throw new ArgumentException("Chain name cannot be null or empty", nameof(chainName));
        }

        if (!_chains.TryGetValue(chainName, out var chain))
        {
            throw new ArgumentException($"Chain '{chainName}' not found", nameof(chainName));
        }

        return chain(_provider);
    }

    public IEnumerable<string> GetAvailableChains() => _chains.Keys;
}
