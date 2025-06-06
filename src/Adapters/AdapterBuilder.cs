using Microsoft.Extensions.DependencyInjection;

namespace Relay.Adapters;

/// <summary>
/// Builder for configuring adapter registration with fluent API
/// </summary>
public sealed class AdapterBuilder<TTarget, TAdaptee>
    where TTarget : class
    where TAdaptee : class
{
    private readonly IServiceCollection _services;
    private ServiceLifetime _adapterLifetime = ServiceLifetime.Scoped;
    private ServiceLifetime _adapteeLifetime = ServiceLifetime.Scoped;

    internal AdapterBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Set the lifetime for the adapter
    /// </summary>
    public AdapterBuilder<TTarget, TAdaptee> WithLifetime(ServiceLifetime lifetime)
    {
        _adapterLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Set the lifetime for the adaptee (the wrapped service)
    /// </summary>
    public AdapterBuilder<TTarget, TAdaptee> WithAdapteeLifetime(ServiceLifetime lifetime)
    {
        _adapteeLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Configure adapter as singleton
    /// </summary>
    public AdapterBuilder<TTarget, TAdaptee> WithSingletonLifetime()
    {
        _adapterLifetime = ServiceLifetime.Singleton;
        return this;
    }

    /// <summary>
    /// Configure adapter as scoped
    /// </summary>
    public AdapterBuilder<TTarget, TAdaptee> WithScopedLifetime()
    {
        _adapterLifetime = ServiceLifetime.Scoped;
        return this;
    }

    /// <summary>
    /// Configure adapter as transient
    /// </summary>
    public AdapterBuilder<TTarget, TAdaptee> WithTransientLifetime()
    {
        _adapterLifetime = ServiceLifetime.Transient;
        return this;
    }

    /// <summary>
    /// Use a specific adapter class that implements TTarget and wraps TAdaptee
    /// </summary>
    public IServiceCollection Using<TAdapter>()
        where TAdapter : class, TTarget
    {
        // Register adaptee
        _services.Add(new ServiceDescriptor(typeof(TAdaptee), typeof(TAdaptee), _adapteeLifetime));

        // Register adapter
        _services.Add(new ServiceDescriptor(typeof(TTarget), typeof(TAdapter), _adapterLifetime));

        return _services;
    }

    /// <summary>
    /// Use a factory function to create the adapter
    /// </summary>
    public IServiceCollection Using(Func<TAdaptee, TTarget> adapterFactory)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);

        // Register adaptee
        _services.Add(new ServiceDescriptor(typeof(TAdaptee), typeof(TAdaptee), _adapteeLifetime));

        // Register adapter using factory
        _services.Add(
            new ServiceDescriptor(
                typeof(TTarget),
                provider =>
                {
                    var adaptee = provider.GetRequiredService<TAdaptee>();
                    return adapterFactory(adaptee);
                },
                _adapterLifetime
            )
        );

        return _services;
    }

    /// <summary>
    /// Use a factory function with access to service provider
    /// </summary>
    public IServiceCollection Using(Func<TAdaptee, IServiceProvider, TTarget> adapterFactory)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);

        // Register adaptee
        _services.Add(new ServiceDescriptor(typeof(TAdaptee), typeof(TAdaptee), _adapteeLifetime));

        // Register adapter using factory with provider
        _services.Add(
            new ServiceDescriptor(
                typeof(TTarget),
                provider =>
                {
                    var adaptee = provider.GetRequiredService<TAdaptee>();
                    return adapterFactory(adaptee, provider);
                },
                _adapterLifetime
            )
        );

        return _services;
    }
}
