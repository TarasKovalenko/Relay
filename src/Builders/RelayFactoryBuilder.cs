using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

public sealed class RelayFactoryBuilder<TInterface>(IServiceCollection services)
    where TInterface : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly Dictionary<string, Func<IServiceProvider, TInterface>> _factories = new();

    // Type-based registrations are materialized in Build() so the configured lifetime applies
    // regardless of the order WithLifetime/RegisterRelay are called in.
    private readonly List<(Type ImplType, bool Keyed, string Key)> _typeRegistrations = [];

    private string? _defaultKey;

    private Func<IRelayContext, string>? _contextKeySelector;

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    /// <summary>
    /// Register a relay via a factory delegate. Escape hatch for types that are not resolvable
    /// from the container (e.g. third-party objects). Prefer <see cref="RegisterRelay{TImplementation}"/>
    /// so the relay and its dependencies are created by DI.
    /// </summary>
    public RelayFactoryBuilder<TInterface> RegisterRelay(
        string key,
        Func<IServiceProvider, TInterface> factory
    )
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        ArgumentNullException.ThrowIfNull(factory);

        _factories[key] = factory;
        return this;
    }

    /// <summary>
    /// Register a relay implementation resolved from the container. The implementation and its
    /// dependencies are created by DI using the configured lifetime.
    /// </summary>
    public RelayFactoryBuilder<TInterface> RegisterRelay<TImplementation>(string key)
        where TImplementation : class, TInterface
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        _factories[key] = static provider => provider.GetRequiredService<TImplementation>();
        _typeRegistrations.Add((typeof(TImplementation), false, key));
        return this;
    }

    /// <summary>
    /// Register a relay implementation using native .NET keyed dependency injection.
    /// The implementation is registered as a keyed <typeparamref name="TInterface"/> and can
    /// be resolved either through the factory or directly via
    /// <c>[FromKeyedServices(key)]</c> / <c>GetRequiredKeyedService&lt;TInterface&gt;(key)</c>.
    /// </summary>
    public RelayFactoryBuilder<TInterface> RegisterKeyedRelay<TImplementation>(string key)
        where TImplementation : class, TInterface
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        _factories[key] = provider => provider.GetRequiredKeyedService<TInterface>(key);
        _typeRegistrations.Add((typeof(TImplementation), true, key));
        return this;
    }

    public RelayFactoryBuilder<TInterface> SetDefaultRelay(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        _defaultKey = key;
        return this;
    }

    public RelayFactoryBuilder<TInterface> WithLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Select which registered key to use based on the <see cref="IRelayContext"/> when
    /// resolving via <see cref="IRelayFactory{TInterface}.CreateRelay(IRelayContext)"/>.
    /// </summary>
    public RelayFactoryBuilder<TInterface> SelectKeyByContext(Func<IRelayContext, string> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        _contextKeySelector = selector;
        return this;
    }

    public IServiceCollection Build()
    {
        // Materialize type-based registrations with the final configured lifetime.
        foreach (var (implType, keyed, key) in _typeRegistrations)
        {
            _services.Add(
                keyed
                    ? new ServiceDescriptor(typeof(TInterface), key, implType, _lifetime)
                    : new ServiceDescriptor(implType, implType, _lifetime)
            );
        }

        _services.Add(
            new ServiceDescriptor(
                typeof(IRelayFactory<TInterface>),
                provider =>
                    new RelayFactory<TInterface>(
                        _factories,
                        provider,
                        _defaultKey,
                        _contextKeySelector
                    ),
                _lifetime
            )
        );

        return _services;
    }
}
