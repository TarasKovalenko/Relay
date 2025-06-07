using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay;

/// <summary>
/// Extensions for IServiceCollection to register relay services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add relay configuration with fluent API
    /// </summary>
    public static IServiceCollection AddRelay(
        this IServiceCollection services,
        Action<RelayConfigurationBuilder> configureRelay
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureRelay);

        var builder = new RelayConfigurationBuilder(services);
        configureRelay(builder);
        return services;
    }

    /// <summary>
    /// Register a single relay with lifetime configuration
    /// </summary>
    public static RelayRegistrationBuilder<TInterface> AddRelay<TInterface, TImplementation>(
        this IServiceCollection services
    )
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        return new RelayRegistrationBuilder<TInterface>(services, typeof(TImplementation));
    }

    /// <summary>
    /// Register conditional relay that routes based on context
    /// </summary>
    public static ConditionalRelayBuilder<TInterface> AddConditionalRelay<TInterface>(
        this IServiceCollection services
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);

        return new ConditionalRelayBuilder<TInterface>(services);
    }

    /// <summary>
    /// Register multi-relay for broadcasting or routing strategies
    /// </summary>
    public static MultiRelayBuilder<TInterface> AddMultiRelay<TInterface>(
        this IServiceCollection services,
        Action<MultiRelayBuilder<TInterface>> configure
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MultiRelayBuilder<TInterface>(services);
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Register relay factory for key-based relay creation
    /// </summary>
    public static RelayFactoryBuilder<TInterface> AddRelayFactory<TInterface>(
        this IServiceCollection services,
        Action<RelayFactoryBuilder<TInterface>> configure
    )
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new RelayFactoryBuilder<TInterface>(services);
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Add core relay services
    /// </summary>
    public static IServiceCollection AddRelayServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRelayContext, DefaultRelayContext>();
        services.AddScoped<IRelayResolver, RelayResolver>();
        return services;
    }

    /// <summary>
    /// Register an adapter chain for complex transformation pipelines
    /// </summary>
    public static AdapterChainBuilder<TResult> AddAdapterChain<TResult>(
        this IServiceCollection services
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        return new AdapterChainBuilder<TResult>(services);
    }

    /// <summary>
    /// Register a strongly-typed adapter chain with known source and target types
    /// </summary>
    public static TypedAdapterChainBuilder<TSource, TTarget> AddTypedAdapterChain<TSource, TTarget>(
        this IServiceCollection services
    )
        where TSource : class
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(services);
        return new TypedAdapterChainBuilder<TSource, TTarget>(services);
    }
}
