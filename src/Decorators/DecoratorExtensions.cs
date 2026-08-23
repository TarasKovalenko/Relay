using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Relay.Decorators;

/// <summary>
/// Decorator registration for <see cref="IServiceCollection"/>: wrap an already registered
/// service without touching the call sites that depend on it.
/// </summary>
public static class DecoratorExtensions
{
    /// <summary>
    /// Wraps every non-keyed registration of <typeparamref name="TInterface"/> with
    /// <paramref name="decoratorType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="TInterface"/> is not registered.</exception>
    public static IServiceCollection Decorate<TInterface>(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decoratorType);

        if (!TryDecorate<TInterface>(services, decoratorType))
        {
            throw new InvalidOperationException($"Service {typeof(TInterface).Name} not registered");
        }

        return services;
    }

    /// <summary>
    /// Wraps every non-keyed registration of <typeparamref name="TInterface"/> with
    /// <typeparamref name="TDecorator"/>.
    /// </summary>
    public static IServiceCollection Decorate<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator
    >(
        this IServiceCollection services
    )
        where TInterface : class
        where TDecorator : class, TInterface
    {
        return services.Decorate<TInterface>(typeof(TDecorator));
    }

    /// <summary>
    /// Wraps every non-keyed registration of <typeparamref name="TInterface"/> using a factory
    /// that receives the inner instance.
    /// </summary>
    public static IServiceCollection Decorate<TInterface>(
        this IServiceCollection services,
        Func<TInterface, IServiceProvider, TInterface> decorator
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        if (!TryDecorate(services, decorator))
        {
            throw new InvalidOperationException($"Service {typeof(TInterface).Name} not registered");
        }

        return services;
    }

    /// <summary>
    /// Wraps <typeparamref name="TInterface"/> when it is registered, and reports whether
    /// anything was decorated instead of throwing.
    /// </summary>
    public static bool TryDecorate<TInterface>(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decoratorType);

        return Decorate(
            services,
            typeof(TInterface),
            serviceKey: null,
            (provider, inner) =>
                ActivatorUtilities.CreateInstance(provider, decoratorType, inner)
        );
    }

    /// <summary>
    /// Wraps <typeparamref name="TInterface"/> with <typeparamref name="TDecorator"/> when it is
    /// registered, and reports whether anything was decorated instead of throwing.
    /// </summary>
    public static bool TryDecorate<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator
    >(
        this IServiceCollection services
    )
        where TInterface : class
        where TDecorator : class, TInterface
    {
        return services.TryDecorate<TInterface>(typeof(TDecorator));
    }

    /// <summary>
    /// Wraps <typeparamref name="TInterface"/> using a factory when it is registered, and reports
    /// whether anything was decorated instead of throwing.
    /// </summary>
    public static bool TryDecorate<TInterface>(
        this IServiceCollection services,
        Func<TInterface, IServiceProvider, TInterface> decorator
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        return Decorate(
            services,
            typeof(TInterface),
            serviceKey: null,
            (provider, inner) => decorator((TInterface)inner, provider)
        );
    }

    /// <summary>
    /// Wraps a keyed registration of <typeparamref name="TInterface"/>. Keyed services are
    /// invisible to the non-keyed overloads, which is a common source of "my decorator never runs".
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">
    /// The key to decorate, or <c>null</c>/<see cref="KeyedService.AnyKey"/> to decorate every
    /// keyed registration of the service.
    /// </param>
    /// <param name="decoratorType">The decorator type; its constructor receives the inner instance.</param>
    public static IServiceCollection DecorateKeyed<TInterface>(
        this IServiceCollection services,
        object? serviceKey,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decoratorType);

        var decorated = Decorate(
            services,
            typeof(TInterface),
            serviceKey ?? KeyedService.AnyKey,
            (provider, inner) =>
                ActivatorUtilities.CreateInstance(provider, decoratorType, inner)
        );

        if (!decorated)
        {
            throw new InvalidOperationException(
                $"Service {typeof(TInterface).Name} is not registered with key '{serviceKey}'"
            );
        }

        return services;
    }

    /// <summary>
    /// Wraps a keyed registration of <typeparamref name="TInterface"/> with <typeparamref name="TDecorator"/>.
    /// </summary>
    public static IServiceCollection DecorateKeyed<
        TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator
    >(
        this IServiceCollection services,
        object? serviceKey
    )
        where TInterface : class
        where TDecorator : class, TInterface
    {
        return services.DecorateKeyed<TInterface>(serviceKey, typeof(TDecorator));
    }

    /// <summary>
    /// Replaces every matching descriptor with one that builds the original instance and hands
    /// it to <paramref name="decorate"/>. Returns <c>false</c> when nothing matched.
    /// </summary>
    private static bool Decorate(
        IServiceCollection services,
        Type serviceType,
        object? serviceKey,
        Func<IServiceProvider, object, object> decorate
    )
    {
        var matches = services
            .Where(descriptor =>
                descriptor.ServiceType == serviceType
                && (
                    serviceKey is null
                        ? !descriptor.IsKeyedService
                        : descriptor.IsKeyedService
                            && (
                                ReferenceEquals(serviceKey, KeyedService.AnyKey)
                                || Equals(descriptor.ServiceKey, serviceKey)
                            )
                )
            )
            .ToList();

        if (matches.Count == 0)
        {
            return false;
        }

        foreach (var registration in matches)
        {
            var index = services.IndexOf(registration);
            services[index] = registration.IsKeyedService
                ? new ServiceDescriptor(
                    serviceType,
                    registration.ServiceKey,
                    (provider, key) =>
                        decorate(provider, CreateKeyedInner(provider, key, registration)),
                    registration.Lifetime
                )
                : new ServiceDescriptor(
                    serviceType,
                    provider => decorate(provider, CreateInner(provider, registration)),
                    registration.Lifetime
                );
        }

        return true;
    }

    private static object CreateInner(IServiceProvider provider, ServiceDescriptor registration)
    {
        if (registration.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(provider, registration.ImplementationType);
        }

        if (registration.ImplementationFactory is not null)
        {
            return registration.ImplementationFactory(provider);
        }

        return registration.ImplementationInstance
            ?? throw new InvalidOperationException("Invalid service registration");
    }

    private static object CreateKeyedInner(
        IServiceProvider provider,
        object? serviceKey,
        ServiceDescriptor registration
    )
    {
        if (registration.KeyedImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(
                provider,
                registration.KeyedImplementationType
            );
        }

        if (registration.KeyedImplementationFactory is not null)
        {
            return registration.KeyedImplementationFactory(provider, serviceKey);
        }

        return registration.KeyedImplementationInstance
            ?? throw new InvalidOperationException("Invalid keyed service registration");
    }
}
