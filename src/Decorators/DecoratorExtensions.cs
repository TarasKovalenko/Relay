using Microsoft.Extensions.DependencyInjection;

namespace Relay.Decorators;

public static class DecoratorExtensions
{
    public static IServiceCollection Decorate<TInterface>(
        this IServiceCollection services,
        Type decoratorType
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decoratorType);

        var registration = services.LastOrDefault(s => s.ServiceType == typeof(TInterface));
        if (registration is null)
        {
            throw new InvalidOperationException(
                $"Service {typeof(TInterface).Name} not registered"
            );
        }

        services.Remove(registration);

        services.Add(
            new ServiceDescriptor(
                typeof(TInterface),
                provider =>
                {
                    var instance =
                        registration.ImplementationType != null
                            ? ActivatorUtilities.CreateInstance(
                                provider,
                                registration.ImplementationType
                            )
                            : registration.ImplementationFactory?.Invoke(provider)
                                ?? registration.ImplementationInstance;

                    return ActivatorUtilities.CreateInstance(provider, decoratorType, instance!);
                },
                registration.Lifetime
            )
        );

        return services;
    }

    public static IServiceCollection Decorate<TInterface>(
        this IServiceCollection services,
        Func<TInterface, IServiceProvider, TInterface> decorator
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        var registration = services.LastOrDefault(s => s.ServiceType == typeof(TInterface));
        if (registration is null)
        {
            throw new InvalidOperationException(
                $"Service {typeof(TInterface).Name} not registered"
            );
        }

        services.Remove(registration);

        services.Add(
            new ServiceDescriptor(
                typeof(TInterface),
                provider =>
                {
                    TInterface instance;
                    if (registration.ImplementationType != null)
                    {
                        instance = (TInterface)
                            ActivatorUtilities.CreateInstance(
                                provider,
                                registration.ImplementationType
                            );
                    }
                    else if (registration.ImplementationFactory != null)
                    {
                        instance = (TInterface)registration.ImplementationFactory(provider);
                    }
                    else
                    {
                        instance = (TInterface)(
                            registration.ImplementationInstance
                            ?? throw new InvalidOperationException("Invalid service registration")
                        );
                    }

                    return decorator(instance, provider);
                },
                registration.Lifetime
            )
        );

        return services;
    }
}
