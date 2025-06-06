using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Relay.Adapters;

/// <summary>
/// Extensions for registering adapter patterns with Microsoft.Extensions.DependencyInjection
/// </summary>
public static class AdapterRegistrationExtensions
{
    /// <summary>
    /// Register an adapter that wraps an incompatible service (Adaptee) to implement the target interface
    /// </summary>
    public static AdapterBuilder<TTarget, TAdaptee> AddAdapter<TTarget, TAdaptee>(
        this IServiceCollection services
    )
        where TTarget : class
        where TAdaptee : class
    {
        ArgumentNullException.ThrowIfNull(services);
        return new AdapterBuilder<TTarget, TAdaptee>(services);
    }

    /// <summary>
    /// Register multiple adapters from assembly scanning based on naming conventions
    /// </summary>
    public static IServiceCollection AddAdaptersFromAssembly<TTargetInterface>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        params Assembly[] assemblies
    )
        where TTargetInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembliesToScan =
            assemblies.Length > 0 ? assemblies : [typeof(TTargetInterface).Assembly];
        var targetInterface = typeof(TTargetInterface);

        foreach (var assembly in assembliesToScan)
        {
            var adapterTypes = assembly
                .GetTypes()
                .Where(t =>
                    t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                )
                .Where(t => targetInterface.IsAssignableFrom(t))
                .Where(t => t.Name.EndsWith("Adapter", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var adapterType in adapterTypes)
            {
                RegisterAdapterWithDependencies(services, targetInterface, adapterType, lifetime);
            }
        }

        return services;
    }

    private static void RegisterAdapterWithDependencies(
        IServiceCollection services,
        Type targetInterface,
        Type adapterType,
        ServiceLifetime lifetime
    )
    {
        var constructors = adapterType.GetConstructors();
        var primaryConstructor = constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (primaryConstructor != null)
        {
            foreach (
                var parameterType in primaryConstructor
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType)
            )
            {
                if (
                    parameterType != targetInterface
                    && !IsFrameworkType(parameterType)
                    && !IsServiceRegistered(services, parameterType)
                )
                {
                    services.Add(new ServiceDescriptor(parameterType, parameterType, lifetime));
                }
            }
        }

        services.Add(new ServiceDescriptor(targetInterface, adapterType, lifetime));
    }

    private static bool IsFrameworkType(Type type)
    {
        return type.Namespace?.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) == true
            || type.Namespace?.StartsWith("System", StringComparison.OrdinalIgnoreCase) == true
            || type == typeof(IServiceProvider);
    }

    private static bool IsServiceRegistered(IServiceCollection services, Type serviceType)
    {
        return services.Any(s => s.ServiceType == serviceType);
    }
}
