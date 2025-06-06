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

    private string? _defaultKey;

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

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

    public RelayFactoryBuilder<TInterface> RegisterRelay<TImplementation>(string key)
        where TImplementation : class, TInterface
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        _factories[key] = provider => provider.GetRequiredService<TImplementation>();
        _services.AddScoped<TImplementation>();
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

    public IServiceCollection Build()
    {
        _services.Add(
            new ServiceDescriptor(
                typeof(IRelayFactory<TInterface>),
                provider => new RelayFactory<TInterface>(_factories, provider, _defaultKey),
                _lifetime
            )
        );

        return _services;
    }
}
