using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

/// <summary>
/// Builder for an <see cref="IAdapterChainFactory{TTarget}"/> that exposes several named
/// adapter chains, all producing the same <typeparamref name="TTarget"/>.
/// </summary>
public sealed class AdapterChainFactoryBuilder<TTarget>(IServiceCollection services)
    where TTarget : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly Dictionary<string, Func<IServiceProvider, TTarget>> _chains = new();

    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    internal IServiceCollection Services => _services;

    /// <summary>
    /// Register a named chain expressed directly as a producer of <typeparamref name="TTarget"/>.
    /// </summary>
    public AdapterChainFactoryBuilder<TTarget> AddChain(
        string name,
        Func<IServiceProvider, TTarget> producer
    )
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Chain name cannot be null or empty", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(producer);
        _chains[name] = producer;
        return this;
    }

    /// <summary>
    /// Register a named chain built from a sequence of adapters. The source instance is
    /// resolved from the container when the chain is created.
    /// </summary>
    public NamedAdapterChainSource<TTarget> AddChain(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Chain name cannot be null or empty", nameof(name));
        }

        return new NamedAdapterChainSource<TTarget>(this, name);
    }

    /// <summary>
    /// Set the lifetime of the registered <see cref="IAdapterChainFactory{TTarget}"/>.
    /// </summary>
    public AdapterChainFactoryBuilder<TTarget> WithLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    internal void AddChainProducer(string name, Func<IServiceProvider, TTarget> producer) =>
        _chains[name] = producer;

    public IServiceCollection Build()
    {
        _services.Add(
            new ServiceDescriptor(
                typeof(IAdapterChainFactory<TTarget>),
                provider => new AdapterChainFactory<TTarget>(_chains, provider),
                _lifetime
            )
        );

        return _services;
    }
}

/// <summary>
/// Selects the source type for a named adapter chain.
/// </summary>
public sealed class NamedAdapterChainSource<TTarget>(
    AdapterChainFactoryBuilder<TTarget> factory,
    string name
)
    where TTarget : class
{
    /// <summary>
    /// Specifies the source type for the chain. The instance is resolved from the container
    /// when <see cref="IAdapterChainFactory{TTarget}.CreateFromChain"/> is called.
    /// </summary>
    public NamedAdapterChainBuilder<TSource, TTarget> From<TSource>()
        where TSource : class
    {
        return new NamedAdapterChainBuilder<TSource, TTarget>(factory, name, typeof(TSource), []);
    }
}

/// <summary>
/// Collects the steps of a named adapter chain.
/// </summary>
public sealed class NamedAdapterChainBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly AdapterChainFactoryBuilder<TTarget> _factory;
    private readonly string _name;
    private readonly Type _originSourceType;
    private readonly List<AdapterChainStep> _steps;

    internal NamedAdapterChainBuilder(
        AdapterChainFactoryBuilder<TTarget> factory,
        string name,
        Type originSourceType,
        List<AdapterChainStep> steps
    )
    {
        _factory = factory;
        _name = name;
        _originSourceType = originSourceType;
        _steps = steps;
    }

    /// <summary>
    /// Adds an intermediate transformation step to the chain.
    /// </summary>
    public NamedAdapterChainBuilder<TTarget1, TTarget> Then<TTarget1, TAdapter>()
        where TTarget1 : class
        where TAdapter : class, IAdapter<TSource, TTarget1>
    {
        _steps.Add(
            new AdapterChainStep
            {
                SourceType = typeof(TSource),
                TargetType = typeof(TTarget1),
                AdapterType = typeof(TAdapter),
                IsFinalStep = false,
            }
        );
        _factory.Services.AddScoped<TAdapter>();
        return new NamedAdapterChainBuilder<TTarget1, TTarget>(
            _factory,
            _name,
            _originSourceType,
            _steps
        );
    }

    /// <summary>
    /// Adds the final transformation step (to <typeparamref name="TTarget"/>) and registers
    /// the named chain with the factory.
    /// </summary>
    public AdapterChainFactoryBuilder<TTarget> Finally<TAdapter>()
        where TAdapter : class, IAdapter<TSource, TTarget>
    {
        _steps.Add(
            new AdapterChainStep
            {
                SourceType = typeof(TSource),
                TargetType = typeof(TTarget),
                AdapterType = typeof(TAdapter),
                IsFinalStep = true,
            }
        );
        _factory.Services.AddScoped<TAdapter>();

        var steps = _steps;
        var originType = _originSourceType;
        _factory.AddChainProducer(
            _name,
            provider =>
            {
                var source = provider.GetRequiredService(originType);
                return new AdapterChain<TTarget>(provider, steps).ExecuteCore(source);
            }
        );

        return _factory;
    }
}
