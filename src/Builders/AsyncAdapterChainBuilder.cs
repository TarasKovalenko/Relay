using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;

namespace Relay.Builders;

/// <summary>
/// Builder for configuring asynchronous adapter chains.
/// </summary>
public sealed class AsyncAdapterChainBuilder<TResult> : IAsyncAdapterChainBuilder<TResult>
{
    private readonly IServiceCollection _services;

    internal AsyncAdapterChainBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IAsyncAdapterChainFromBuilder<TSource, TResult> From<TSource>()
        where TSource : class
    {
        return new AsyncAdapterChainFromBuilder<TSource, TResult>(_services);
    }
}

internal sealed class AsyncAdapterChainFromBuilder<TSource, TResult>
    : IAsyncAdapterChainFromBuilder<TSource, TResult>
    where TSource : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps = [];

    internal AsyncAdapterChainFromBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IAsyncAdapterChainThenBuilder<TTarget, TResult> Then<
        TTarget,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TTarget : class
        where TAdapter : class, IAsyncAdapter<TSource, TTarget>
    {
        _steps.Add(
            AdapterChainStepFactory.CreateAsync<TSource, TTarget, TAdapter>(isFinalStep: false)
        );
        _services.TryAddAdapter<TAdapter>();
        return new AsyncAdapterChainThenBuilder<TTarget, TResult>(_services, _steps);
    }

    public IAsyncAdapterChainFinalBuilder<TResult> Finally<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TAdapter : class, IAsyncAdapter<TSource, TResult>
    {
        _steps.Add(
            AdapterChainStepFactory.CreateAsync<TSource, TResult, TAdapter>(isFinalStep: true)
        );
        _services.TryAddAdapter<TAdapter>();
        return new AsyncAdapterChainFinalBuilder<TResult>(_services, _steps);
    }
}

internal sealed class AsyncAdapterChainThenBuilder<TSource, TResult>
    : IAsyncAdapterChainThenBuilder<TSource, TResult>
    where TSource : class
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;

    internal AsyncAdapterChainThenBuilder(IServiceCollection services, List<AdapterChainStep> steps)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public IAsyncAdapterChainThenBuilder<TTarget, TResult> Then<
        TTarget,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TTarget : class
        where TAdapter : class, IAsyncAdapter<TSource, TTarget>
    {
        _steps.Add(
            AdapterChainStepFactory.CreateAsync<TSource, TTarget, TAdapter>(isFinalStep: false)
        );
        _services.TryAddAdapter<TAdapter>();
        return new AsyncAdapterChainThenBuilder<TTarget, TResult>(_services, _steps);
    }

    public IAsyncAdapterChainFinalBuilder<TResult> Finally<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TAdapter : class, IAsyncAdapter<TSource, TResult>
    {
        _steps.Add(
            AdapterChainStepFactory.CreateAsync<TSource, TResult, TAdapter>(isFinalStep: true)
        );
        _services.TryAddAdapter<TAdapter>();
        return new AsyncAdapterChainFinalBuilder<TResult>(_services, _steps);
    }
}

internal sealed class AsyncAdapterChainFinalBuilder<TResult> : IAsyncAdapterChainFinalBuilder<TResult>
{
    private readonly IServiceCollection _services;
    private readonly List<AdapterChainStep> _steps;

    internal AsyncAdapterChainFinalBuilder(IServiceCollection services, List<AdapterChainStep> steps)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public void Build()
    {
        _services.AddScoped<IAsyncAdapterChain<TResult>>(provider => new AsyncAdapterChain<TResult>(
            provider,
            _steps
        ));
    }
}
