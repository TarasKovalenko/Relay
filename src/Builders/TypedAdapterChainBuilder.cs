using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

/// <summary>
/// Builder for typed adapter chains with known source and target types
/// </summary>
public sealed class TypedAdapterChainBuilder<TSource, TTarget>
    : ITypedAdapterChainBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps = [];

    internal TypedAdapterChainBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public ITypedAdapterChainThenBuilder<TIntermediate, TTarget> Then<TIntermediate, TAdapter>()
        where TIntermediate : class
        where TAdapter : class, IAdapter<TSource, TIntermediate>
    {
        _steps.Add(
            new AdapterChainStep
            {
                SourceType = typeof(TSource),
                TargetType = typeof(TIntermediate),
                AdapterType = typeof(TAdapter),
                IsFinalStep = false,
            }
        );

        // Register the adapter
        _services.AddScoped<TAdapter>();

        return new TypedAdapterChainThenBuilder<TIntermediate, TTarget>(
            _services,
            _steps,
            typeof(TSource),
            typeof(TTarget)
        );
    }

    public ITypedAdapterChainFinalBuilder<TSource, TTarget> Then<TAdapter>()
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

        // Register the adapter
        _services.AddScoped<TAdapter>();

        return new TypedAdapterChainFinalBuilder<TSource, TTarget>(
            _services,
            _steps,
            typeof(TSource),
            typeof(TTarget)
        );
    }
}

/// <summary>
/// Builder for subsequent steps in typed adapter chains
/// </summary>
internal sealed class TypedAdapterChainThenBuilder<TSource, TTarget>
    : ITypedAdapterChainThenBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;
    private readonly Type _originalSourceType;
    private readonly Type _finalTargetType;

    internal TypedAdapterChainThenBuilder(
        IServiceCollection services,
        List<AdapterChainStep> steps,
        Type originalSourceType,
        Type finalTargetType
    )
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _originalSourceType =
            originalSourceType ?? throw new ArgumentNullException(nameof(originalSourceType));
        _finalTargetType =
            finalTargetType ?? throw new ArgumentNullException(nameof(finalTargetType));
    }

    public ITypedAdapterChainThenBuilder<TIntermediate, TTarget> Then<TIntermediate, TAdapter>()
        where TIntermediate : class
        where TAdapter : class, IAdapter<TSource, TIntermediate>
    {
        _steps.Add(
            new AdapterChainStep
            {
                SourceType = typeof(TSource),
                TargetType = typeof(TIntermediate),
                AdapterType = typeof(TAdapter),
                IsFinalStep = false,
            }
        );

        // Register the adapter
        _services.AddScoped<TAdapter>();

        return new TypedAdapterChainThenBuilder<TIntermediate, TTarget>(
            _services,
            _steps,
            _originalSourceType,
            _finalTargetType
        );
    }

    public ITypedAdapterChainFinalBuilder<TSource, TTarget> Then<TAdapter>()
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

        // Register the adapter
        _services.AddScoped<TAdapter>();

        // Create a final builder that will register with the original types
        return new TypedAdapterChainFinalBuilderProxy<TSource, TTarget>(
            _services,
            _steps,
            _originalSourceType,
            _finalTargetType
        );
    }
}

/// <summary>
/// Final builder for completing the typed adapter chain configuration with original source types
/// </summary>
internal sealed class TypedAdapterChainFinalBuilder<TSource, TTarget>
    : ITypedAdapterChainFinalBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;
    private readonly Type _originalSourceType;
    private readonly Type _finalTargetType;

    internal TypedAdapterChainFinalBuilder(
        IServiceCollection services,
        List<AdapterChainStep> steps,
        Type originalSourceType,
        Type finalTargetType
    )
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _originalSourceType =
            originalSourceType ?? throw new ArgumentNullException(nameof(originalSourceType));
        _finalTargetType =
            finalTargetType ?? throw new ArgumentNullException(nameof(finalTargetType));
    }

    public void Build()
    {
        // Register with the original source and final target types
        var serviceType = typeof(ITypedAdapterChain<,>).MakeGenericType(
            _originalSourceType,
            _finalTargetType
        );
        var implementationType = typeof(TypedAdapterChain<,>).MakeGenericType(
            _originalSourceType,
            _finalTargetType
        );

        _services.AddScoped(
            serviceType,
            provider =>
            {
                var innerChainType = typeof(AdapterChain<>).MakeGenericType(_finalTargetType);
                var innerChain = Activator.CreateInstance(innerChainType, provider, _steps);
                return Activator.CreateInstance(implementationType, innerChain)!;
            }
        );
    }
}

/// <summary>
/// Proxy final builder that registers with the original source type instead of intermediate type
/// </summary>
internal sealed class TypedAdapterChainFinalBuilderProxy<TCurrentSource, TTarget>
    : ITypedAdapterChainFinalBuilder<TCurrentSource, TTarget>
    where TCurrentSource : class
    where TTarget : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;
    private readonly Type _originalSourceType;
    private readonly Type _finalTargetType;

    internal TypedAdapterChainFinalBuilderProxy(
        IServiceCollection services,
        List<AdapterChainStep> steps,
        Type originalSourceType,
        Type finalTargetType
    )
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _originalSourceType =
            originalSourceType ?? throw new ArgumentNullException(nameof(originalSourceType));
        _finalTargetType =
            finalTargetType ?? throw new ArgumentNullException(nameof(finalTargetType));
    }

    public void Build()
    {
        // Register with the ORIGINAL source type and final target type, not the current intermediate types
        var serviceType = typeof(ITypedAdapterChain<,>).MakeGenericType(
            _originalSourceType,
            _finalTargetType
        );
        var implementationType = typeof(TypedAdapterChain<,>).MakeGenericType(
            _originalSourceType,
            _finalTargetType
        );

        _services.AddScoped(
            serviceType,
            provider =>
            {
                var innerChainType = typeof(AdapterChain<>).MakeGenericType(_finalTargetType);
                var innerChain = Activator.CreateInstance(innerChainType, provider, _steps);
                return Activator.CreateInstance(implementationType, innerChain)!;
            }
        );
    }
}
