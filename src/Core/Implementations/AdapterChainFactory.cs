using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

public sealed class AdapterChainFactory<TTarget>(List<string> chainNames, IServiceProvider provider)
    : IAdapterChainFactory<TTarget>
    where TTarget : class
{
    private readonly List<string> _chainNames =
        chainNames ?? throw new ArgumentNullException(nameof(chainNames));

    private readonly IServiceProvider _provider =
        provider ?? throw new ArgumentNullException(nameof(provider));

    public TTarget CreateFromChain(string chainName)
    {
        if (string.IsNullOrEmpty(chainName))
        {
            throw new ArgumentException("Chain name cannot be null or empty", nameof(chainName));
        }

        if (!_chainNames.Contains(chainName))
        {
            throw new ArgumentException($"Chain '{chainName}' not found", nameof(chainName));
        }

        // This would need more sophisticated resolution based on the actual chain configuration
        return _provider.GetRequiredService<TTarget>();
    }

    public IEnumerable<string> GetAvailableChains() => _chainNames;
}
