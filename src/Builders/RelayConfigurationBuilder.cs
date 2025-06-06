using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Relay.Builders;

public sealed class RelayConfigurationBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Assembly> _assemblies = [];
    private ServiceLifetime _defaultLifetime = ServiceLifetime.Scoped;

    public RelayConfigurationBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _services.AddRelayServices();
    }

    public RelayConfigurationBuilder FromAssemblyOf<T>()
    {
        _assemblies.Add(typeof(T).Assembly);
        return this;
    }

    public RelayConfigurationBuilder FromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        _assemblies.AddRange(assemblies);
        return this;
    }

    public RelayConfigurationBuilder WithDefaultLifetime(ServiceLifetime lifetime)
    {
        _defaultLifetime = lifetime;
        return this;
    }

    public RelayTypeSelector AddRelays(Action<RelayTypeSelector> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var selector = new RelayTypeSelector(_services, _assemblies, _defaultLifetime);
        configure(selector);
        return selector;
    }

    public RelayConfigurationBuilder RegisterRelays()
    {
        foreach (var assembly in _assemblies)
        {
            var implementationTypes = assembly
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false })
                .ToList();

            foreach (var implementationType in implementationTypes)
            {
                var interfaces = implementationType
                    .GetInterfaces()
                    .Where(i =>
                        !i.IsGenericTypeDefinition
                        && i != typeof(IDisposable)
                        && !IsFrameworkInterface(i)
                        && i.IsPublic
                        && !i.IsSpecialName
                    )
                    .ToList();

                foreach (var interfaceType in interfaces)
                {
                    _services.Add(
                        new ServiceDescriptor(interfaceType, implementationType, _defaultLifetime)
                    );
                }
            }
        }
        return this;
    }

    private static bool IsFrameworkInterface(Type type)
    {
        return type.Namespace?.StartsWith("System", StringComparison.OrdinalIgnoreCase) == true
            || type.Namespace?.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) == true;
    }
}
