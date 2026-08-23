using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Relay.Extensions;

/// <summary>
/// Adds deferred (<see cref="Lazy{T}"/>) resolution to the container.
/// </summary>
/// <remarks>
/// Solves two everyday dependency injection problems:
/// <list type="bullet">
/// <item>a dependency that is expensive to build but rarely used;</item>
/// <item>a dependency cycle that can only be broken by resolving one side later.</item>
/// </list>
/// After calling <see cref="AddLazyResolution"/> any registered service can be injected as
/// <c>Lazy&lt;TService&gt;</c> without registering the closed type explicitly.
/// </remarks>
public static class LazyResolutionExtensions
{
    /// <summary>
    /// Registers the open generic <see cref="Lazy{T}"/> so that <c>Lazy&lt;TService&gt;</c>
    /// resolves any service registered in the container.
    /// </summary>
    public static IServiceCollection AddLazyResolution(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(
            new ServiceDescriptor(
                typeof(Lazy<>),
                typeof(LazyResolution<>),
                ServiceLifetime.Transient
            )
        );

        return services;
    }

    /// <summary>
    /// Registers a lazily resolved <typeparamref name="TService"/> for the cases where only one
    /// closed <see cref="Lazy{T}"/> is needed and the open generic registration is unwanted.
    /// </summary>
    public static IServiceCollection AddLazyResolution<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            TService
    >(this IServiceCollection services)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<Lazy<TService>>(provider => new Lazy<TService>(
            provider.GetRequiredService<TService>
        ));

        return services;
    }
}

/// <summary>
/// <see cref="Lazy{T}"/> whose value factory resolves <typeparamref name="T"/> from the
/// service provider on first access.
/// </summary>
internal sealed class LazyResolution<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T
>(IServiceProvider provider) : Lazy<T>(provider.GetRequiredService<T>)
    where T : notnull;
