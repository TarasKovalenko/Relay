using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Core.Options;

namespace Relay.Builders;

public sealed class MultiRelayBuilder<TInterface>(IServiceCollection services)
    where TInterface : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly List<RelayRegistration> _relayRegistrations = [];

    private RelayStrategy _strategy = RelayStrategy.Broadcast;

    private RelayResilienceOptions _resilience = RelayResilienceOptions.None;

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    public MultiRelayBuilder<TInterface> AddRelay<TRelay>(ServiceLifetime? lifetime = null)
        where TRelay : class, TInterface
    {
        // Registration is materialized in Build() so a later WithDefaultLifetime still applies
        // to relays added without an explicit lifetime.
        _relayRegistrations.Add(
            new RelayRegistration { ImplementationType = typeof(TRelay), Lifetime = lifetime }
        );
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

    /// <summary>
    /// Retry each relay up to <paramref name="maxAttempts"/> times (with optional delay and
    /// exponential backoff) before failing over to the next relay. Applies to the
    /// <see cref="RelayStrategy.Failover"/> and <see cref="RelayStrategy.FirstSuccessful"/> strategies.
    /// </summary>
    public MultiRelayBuilder<TInterface> WithRetry(
        int maxAttempts,
        TimeSpan? delay = null,
        double backoffFactor = 1.0
    )
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                "maxAttempts must be at least 1"
            );
        }

        _resilience = new RelayResilienceOptions
        {
            MaxAttempts = maxAttempts,
            Delay = delay ?? TimeSpan.Zero,
            BackoffFactor = backoffFactor,
        };
        return this;
    }

    public IServiceCollection Build()
    {
        // Materialize each relay registration with its explicit lifetime, or the final default.
        foreach (var reg in _relayRegistrations)
        {
            _services.Add(
                new ServiceDescriptor(
                    reg.ImplementationType,
                    reg.ImplementationType,
                    reg.Lifetime ?? _lifetime
                )
            );
        }

        var registrations = _relayRegistrations.ToList();
        _services.Add(
            new ServiceDescriptor(
                typeof(IMultiRelay<TInterface>),
                provider =>
                {
                    // Fail loud if a relay cannot be resolved rather than silently dropping it.
                    var relays = registrations
                        .Select(reg => (TInterface)provider.GetRequiredService(reg.ImplementationType))
                        .ToList();

                    return new MultiRelay<TInterface>(relays, _strategy, _resilience);
                },
                _lifetime
            )
        );

        return _services;
    }

    private sealed class RelayRegistration
    {
        public Type ImplementationType { get; set; } = null!;
        public ServiceLifetime? Lifetime { get; set; }
    }
}
