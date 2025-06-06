using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

public sealed class ConditionalRelayBuilder<TInterface>(IServiceCollection services)
    where TInterface : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly List<ConditionalRelayRegistration> _registrations = [];

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    public ConditionalRelayBuilder<TInterface> WithSingletonLifetime()
    {
        _lifetime = ServiceLifetime.Singleton;
        return this;
    }

    public ConditionalRelayBuilder<TInterface> WithScopedLifetime()
    {
        _lifetime = ServiceLifetime.Scoped;
        return this;
    }

    public ConditionalRelayBuilder<TInterface> WithTransientLifetime()
    {
        _lifetime = ServiceLifetime.Transient;
        return this;
    }

    public ConditionalRelayBuilder<TInterface> WithLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    public ConditionalRelayStep<TInterface> When(Func<IRelayContext, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new ConditionalRelayStep<TInterface>(this, condition);
    }

    public ConditionalRelayBuilder<TInterface> Otherwise<TImplementation>()
        where TImplementation : class, TInterface
    {
        return When(_ => true).RelayTo<TImplementation>();
    }

    public IServiceCollection Build()
    {
        _services.Add(
            new ServiceDescriptor(
                typeof(TInterface),
                provider =>
                {
                    var context =
                        provider.GetService<IRelayContext>() ?? new DefaultRelayContext(provider);

                    var registration = _registrations.FirstOrDefault(r => r.Condition(context));
                    if (registration is null)
                    {
                        throw new InvalidOperationException(
                            $"No suitable relay found for {typeof(TInterface).Name}"
                        );
                    }
                    var implementationType =
                        registration.ImplementationType ?? registration.TypeSelector!(context);
                    return (TInterface)
                        ActivatorUtilities.CreateInstance(provider, implementationType);
                },
                _lifetime
            )
        );

        return _services;
    }

    internal void AddRegistration(ConditionalRelayRegistration registration)
    {
        _registrations.Add(registration);
    }
}

public sealed class ConditionalRelayStep<TInterface>(
    ConditionalRelayBuilder<TInterface> parent,
    Func<IRelayContext, bool> condition
)
    where TInterface : class
{
    private readonly ConditionalRelayBuilder<TInterface> _parent =
        parent ?? throw new ArgumentNullException(nameof(parent));

    private readonly Func<IRelayContext, bool> _condition =
        condition ?? throw new ArgumentNullException(nameof(condition));

    public ConditionalRelayBuilder<TInterface> RelayTo<TImplementation>()
        where TImplementation : class, TInterface
    {
        _parent.AddRegistration(
            new ConditionalRelayRegistration
            {
                Condition = _condition,
                ImplementationType = typeof(TImplementation),
            }
        );
        return _parent;
    }

    public ConditionalRelayBuilder<TInterface> RelayTo(Func<IRelayContext, Type> typeSelector)
    {
        ArgumentNullException.ThrowIfNull(typeSelector);

        _parent.AddRegistration(
            new ConditionalRelayRegistration { Condition = _condition, TypeSelector = typeSelector }
        );
        return _parent;
    }
}

internal sealed class ConditionalRelayRegistration
{
    public Func<IRelayContext, bool> Condition { get; set; } = null!;
    public Type? ImplementationType { get; set; }
    public Func<IRelayContext, Type>? TypeSelector { get; set; }
}
