using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

/// <summary>
/// Builder for configuring adapter chains
/// </summary>
public sealed class AdapterChainBuilder<TResult> : IAdapterChainBuilder<TResult>
{
    private readonly IServiceCollection _services;

    internal AdapterChainBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IAdapterChainFromBuilder<TSource, TResult> From<TSource>()
        where TSource : class
    {
        return new AdapterChainFromBuilder<TSource, TResult>(_services);
    }
}

/// <summary>
/// Builder for configuring adapter chains after specifying the source
/// </summary>
internal sealed class AdapterChainFromBuilder<TSource, TResult>
    : IAdapterChainFromBuilder<TSource, TResult>
    where TSource : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps = [];

    internal AdapterChainFromBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IAdapterChainThenBuilder<TTarget, TResult> Then<
        TTarget,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TTarget : class
        where TAdapter : class, IAdapter<TSource, TTarget>
    {
        _steps.Add(AdapterChainStepFactory.Create<TSource, TTarget, TAdapter>(isFinalStep: false));
        _services.TryAddAdapter<TAdapter>();

        return new AdapterChainThenBuilder<TTarget, TResult>(_services, _steps);
    }

    public IAdapterChainFinalBuilder<TResult> Finally<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TAdapter : class, IAdapter<TSource, TResult>
    {
        _steps.Add(AdapterChainStepFactory.Create<TSource, TResult, TAdapter>(isFinalStep: true));
        _services.TryAddAdapter<TAdapter>();

        return new AdapterChainFinalBuilder<TResult>(_services, _steps);
    }
}

/// <summary>
/// Builder for configuring subsequent steps in the adapter chain
/// </summary>
internal sealed class AdapterChainThenBuilder<TSource, TResult>
    : IAdapterChainThenBuilder<TSource, TResult>
    where TSource : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;

    internal AdapterChainThenBuilder(IServiceCollection services, List<AdapterChainStep> steps)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public IAdapterChainThenBuilder<TTarget, TResult> Then<
        TTarget,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TTarget : class
        where TAdapter : class, IAdapter<TSource, TTarget>
    {
        _steps.Add(AdapterChainStepFactory.Create<TSource, TTarget, TAdapter>(isFinalStep: false));
        _services.TryAddAdapter<TAdapter>();

        return new AdapterChainThenBuilder<TTarget, TResult>(_services, _steps);
    }

    public IAdapterChainFinalBuilder<TResult> Finally<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TAdapter : class, IAdapter<TSource, TResult>
    {
        _steps.Add(AdapterChainStepFactory.Create<TSource, TResult, TAdapter>(isFinalStep: true));
        _services.TryAddAdapter<TAdapter>();

        return new AdapterChainFinalBuilder<TResult>(_services, _steps);
    }
}

/// <summary>
/// Final builder for completing the adapter chain configuration
/// </summary>
internal sealed class AdapterChainFinalBuilder<TResult> : IAdapterChainFinalBuilder<TResult>
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;

    internal AdapterChainFinalBuilder(IServiceCollection services, List<AdapterChainStep> steps)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public void Build()
    {
        _services.AddScoped<IAdapterChain<TResult>>(provider => new AdapterChain<TResult>(
            provider,
            _steps
        ));
    }
}
