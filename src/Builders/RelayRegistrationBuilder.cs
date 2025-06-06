using Microsoft.Extensions.DependencyInjection;
using Relay.Decorators;

namespace Relay.Builders;

public sealed class RelayRegistrationBuilder<TInterface>(
    IServiceCollection services,
    Type implementationType
)
    where TInterface : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly Type _implementationType =
        implementationType ?? throw new ArgumentNullException(nameof(implementationType));

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    private readonly List<Type> _decorators = [];

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

    public RelayRegistrationBuilder<TInterface> DecorateWith<TDecorator>()
        where TDecorator : class, TInterface
    {
        _decorators.Add(typeof(TDecorator));
        return this;
    }

    public IServiceCollection Build()
    {
        var descriptor = new ServiceDescriptor(typeof(TInterface), _implementationType, _lifetime);
        _services.Add(descriptor);

        // Apply decorators
        foreach (var decoratorType in _decorators)
        {
            _services.Decorate<TInterface>(decoratorType);
        }

        return _services;
    }

    // Method chaining
    public IServiceCollection ToServiceCollection()
    {
        return Build();
    }
}
