using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Relay.Builders;

public sealed class RelayTypeSelector(
    IServiceCollection services,
    List<Assembly> assemblies,
    ServiceLifetime defaultLifetime = ServiceLifetime.Scoped
)
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly List<Assembly> _assemblies =
        assemblies ?? throw new ArgumentNullException(nameof(assemblies));

    private Func<Type, bool> _typeFilter = _ => true;

    private ServiceLifetime _lifetime = defaultLifetime;

    public RelayTypeSelector Where(Func<Type, bool> predicate)
    {
        _typeFilter = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RelayTypeSelector ForInterface<TInterface>()
    {
        var interfaceType = typeof(TInterface);
        var implementations = _assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => interfaceType.IsAssignableFrom(t))
            .Where(_typeFilter)
            .ToList();

        foreach (var implementation in implementations)
        {
            _services.Add(new ServiceDescriptor(interfaceType, implementation, _lifetime));
        }

        return this;
    }

    public RelayTypeSelector WithSingletonLifetime()
    {
        _lifetime = ServiceLifetime.Singleton;
        return this;
    }

    public RelayTypeSelector WithScopedLifetime()
    {
        _lifetime = ServiceLifetime.Scoped;
        return this;
    }

    public RelayTypeSelector WithTransientLifetime()
    {
        _lifetime = ServiceLifetime.Transient;
        return this;
    }

    public RelayTypeSelector WithLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    public RelayTypeSelector AsSingleton<TInterface>()
    {
        return WithSingletonLifetime().ForInterface<TInterface>();
    }

    public RelayTypeSelector AsScoped<TInterface>()
    {
        return WithScopedLifetime().ForInterface<TInterface>();
    }

    public RelayTypeSelector AsTransient<TInterface>()
    {
        return WithTransientLifetime().ForInterface<TInterface>();
    }
}
