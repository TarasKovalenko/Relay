using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

public sealed class MultiRelayBuilder<TInterface>(IServiceCollection services)
    where TInterface : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly List<RelayRegistration> _relayRegistrations = [];

    private RelayStrategy _strategy = RelayStrategy.Broadcast;

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    public MultiRelayBuilder<TInterface> AddRelay<TRelay>(ServiceLifetime? lifetime = null)
        where TRelay : class, TInterface
    {
        var relayLifetime = lifetime ?? _lifetime;
        _relayRegistrations.Add(
            new RelayRegistration { ImplementationType = typeof(TRelay), Lifetime = relayLifetime }
        );

        _services.Add(new ServiceDescriptor(typeof(TRelay), typeof(TRelay), relayLifetime));
        return this;
    }

    public MultiRelayBuilder<TInterface> WithStrategy(RelayStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public MultiRelayBuilder<TInterface> WithSingletonLifetime()
    {
        _lifetime = ServiceLifetime.Singleton;
        return this;
    }

    public MultiRelayBuilder<TInterface> WithScopedLifetime()
    {
        _lifetime = ServiceLifetime.Scoped;
        return this;
    }

    public MultiRelayBuilder<TInterface> WithTransientLifetime()
    {
        _lifetime = ServiceLifetime.Transient;
        return this;
    }

    public MultiRelayBuilder<TInterface> WithDefaultLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    public MultiRelayBuilder<TInterface> WithParallelExecution()
    {
        _strategy = RelayStrategy.Parallel;
        return this;
    }

    public IServiceCollection Build()
    {
        _services.Add(
            new ServiceDescriptor(
                typeof(IMultiRelay<TInterface>),
                provider =>
                {
                    var relays = _relayRegistrations
                        .Select(reg => provider.GetService(reg.ImplementationType))
                        .OfType<TInterface>()
                        .ToList();

                    return new MultiRelay<TInterface>(relays, _strategy);
                },
                _lifetime
            )
        );

        return _services;
    }

    private sealed class RelayRegistration
    {
        public Type ImplementationType { get; set; } = null!;
        public ServiceLifetime Lifetime { get; set; }
    }
}
