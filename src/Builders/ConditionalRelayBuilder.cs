using System.Diagnostics.CodeAnalysis;
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

    public ConditionalRelayBuilder<TInterface> Otherwise<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation
    >()
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
                    // Prefer an explicit context flowed in via the resolver, then the registered
                    // scoped context, then a freshly built default.
                    var context =
                        provider.GetService<IRelayContextAccessor>()?.Current
                        ?? provider.GetService<IRelayContext>()
                        ?? new DefaultRelayContext(provider);

                    var registration = _registrations.FirstOrDefault(r => r.Condition(context));
                    if (registration is null)
                    {
                        throw new InvalidOperationException(
                            $"No suitable relay found for {typeof(TInterface).Name}"
                        );
                    }
                    var implementationType =
                        registration.ImplementationType ?? ResolveSelectedType(registration, context);
                    // Reuse an existing DI registration (and its lifetime) when the implementation
                    // is registered; otherwise create it with its constructor dependencies resolved.
                    var instance =
                        provider.GetService(implementationType)
                        ?? ActivatorUtilities.CreateInstance(provider, implementationType);
                    return (TInterface)instance;
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

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2073:UnrecognizedReflectionPattern",
        Justification = "Only reached for the RelayTo(Func<IRelayContext, Type>) overload, which "
            + "is annotated with RequiresUnreferencedCode."
    )]
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private static Type ResolveSelectedType(
        ConditionalRelayRegistration registration,
        IRelayContext context
    ) => registration.TypeSelector!(context);
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

    public ConditionalRelayBuilder<TInterface> RelayTo<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation
    >()
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

    [RequiresUnreferencedCode(
        "The selected implementation type is only known at runtime and cannot be statically "
            + "analysed; it may be removed when trimming."
    )]
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

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ImplementationType { get; set; }

    public Func<IRelayContext, Type>? TypeSelector { get; set; }
}
