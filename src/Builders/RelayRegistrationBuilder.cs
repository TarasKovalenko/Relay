using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Relay.Decorators;

namespace Relay.Builders;

public sealed class RelayRegistrationBuilder<TInterface>(
    IServiceCollection services,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType
)
    where TInterface : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _implementationType =
        implementationType ?? throw new ArgumentNullException(nameof(implementationType));

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    // Decorators are stored as ready-made registration steps so the decorator type stays
    // statically known (and therefore trim-safe) at the point DecorateWith was called.
    private readonly List<Action<IServiceCollection>> _decorators = [];

    public RelayRegistrationBuilder<TInterface> WithSingletonLifetime()
    {
        _lifetime = ServiceLifetime.Singleton;
        return this;
    }

    public RelayRegistrationBuilder<TInterface> WithScopedLifetime()
    {
        _lifetime = ServiceLifetime.Scoped;
        return this;
    }

    public RelayRegistrationBuilder<TInterface> WithTransientLifetime()
    {
        _lifetime = ServiceLifetime.Transient;
        return this;
    }

    public RelayRegistrationBuilder<TInterface> WithLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    public RelayRegistrationBuilder<TInterface> DecorateWith<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator
    >()
        where TDecorator : class, TInterface
    {
        _decorators.Add(static services => services.Decorate<TInterface, TDecorator>());
        return this;
    }

    public IServiceCollection Build()
    {
        var descriptor = new ServiceDescriptor(typeof(TInterface), _implementationType, _lifetime);
        _services.Add(descriptor);

        // Apply decorators
        foreach (var decorate in _decorators)
        {
            decorate(_services);
        }

        return _services;
    }

    // Method chaining
    public IServiceCollection ToServiceCollection()
    {
        return Build();
    }
}
