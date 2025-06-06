using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

public sealed class RelayFactory<TInterface>(
    IReadOnlyDictionary<string, Func<IServiceProvider, TInterface>> factories,
    IServiceProvider serviceProvider,
    string? defaultKey
) : IRelayFactory<TInterface>
    where TInterface : class
{
    private readonly IReadOnlyDictionary<string, Func<IServiceProvider, TInterface>> _factories =
        factories ?? throw new ArgumentNullException(nameof(factories));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public TInterface CreateRelay(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        if (!_factories.TryGetValue(key, out var factory))
        {
            throw new ArgumentException($"No relay registered for key '{key}'", nameof(key));
        }

        return factory(_serviceProvider);
    }

    public TInterface CreateRelay(IRelayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Default implementation - can be overridden for context-based selection
        return GetDefaultRelay();
    }

    public TInterface GetDefaultRelay()
    {
        if (string.IsNullOrEmpty(defaultKey))
        {
            throw new InvalidOperationException("No default relay configured");
        }

        return CreateRelay(defaultKey);
    }

    public IEnumerable<string> GetAvailableKeys() => _factories.Keys;
}
