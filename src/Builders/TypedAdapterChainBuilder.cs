using System.Diagnostics.CodeAnalysis;
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

    public ITypedAdapterChainThenBuilder<TIntermediate, TTarget> Then<
        TIntermediate,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TIntermediate : class
        where TAdapter : class, IAdapter<TSource, TIntermediate>
    {
        _steps.Add(
            AdapterChainStepFactory.Create<TSource, TIntermediate, TAdapter>(isFinalStep: false)
        );
        _services.TryAddAdapter<TAdapter>();

        // The registration closure is created here, where the original source type is still
        // statically known, so no MakeGenericType/Activator reflection is needed later on.
        return new TypedAdapterChainThenBuilder<TIntermediate, TTarget>(
            _services,
            _steps,
            Register
        );
    }

    public ITypedAdapterChainFinalBuilder<TSource, TTarget> Then<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TAdapter : class, IAdapter<TSource, TTarget>
    {
        _steps.Add(AdapterChainStepFactory.Create<TSource, TTarget, TAdapter>(isFinalStep: true));
        _services.TryAddAdapter<TAdapter>();

        return new TypedAdapterChainFinalBuilder<TSource, TTarget>(_steps, Register);
    }

    private void Register(List<AdapterChainStep> steps)
    {
        _services.AddScoped<ITypedAdapterChain<TSource, TTarget>>(provider =>
            new TypedAdapterChain<TSource, TTarget>(new AdapterChain<TTarget>(provider, steps))
        );
    }
}

/// <summary>
/// Builder for subsequent steps in typed adapter chains
/// </summary>
internal sealed class TypedAdapterChainThenBuilder<TSource, TTarget>(
    IServiceCollection services,
    List<AdapterChainStep> steps,
    Action<List<AdapterChainStep>> register
) : ITypedAdapterChainThenBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly IServiceCollection _services =
        services ?? throw new ArgumentNullException(nameof(services));

    private readonly List<AdapterChainStep> _steps =
        steps ?? throw new ArgumentNullException(nameof(steps));

    private readonly Action<List<AdapterChainStep>> _register =
        register ?? throw new ArgumentNullException(nameof(register));

    public ITypedAdapterChainThenBuilder<TIntermediate, TTarget> Then<
        TIntermediate,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TIntermediate : class
        where TAdapter : class, IAdapter<TSource, TIntermediate>
    {
        _steps.Add(
            AdapterChainStepFactory.Create<TSource, TIntermediate, TAdapter>(isFinalStep: false)
        );
        _services.TryAddAdapter<TAdapter>();

        return new TypedAdapterChainThenBuilder<TIntermediate, TTarget>(
            _services,
            _steps,
            _register
        );
    }

    public ITypedAdapterChainFinalBuilder<TSource, TTarget> Then<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >()
        where TAdapter : class, IAdapter<TSource, TTarget>
    {
        _steps.Add(AdapterChainStepFactory.Create<TSource, TTarget, TAdapter>(isFinalStep: true));
        _services.TryAddAdapter<TAdapter>();

        // Registration always targets the ORIGINAL source type captured by _register,
        // not the current intermediate type.
        return new TypedAdapterChainFinalBuilder<TSource, TTarget>(_steps, _register);
    }
}

/// <summary>
/// Final builder for completing the typed adapter chain configuration
/// </summary>
internal sealed class TypedAdapterChainFinalBuilder<TSource, TTarget>(
    List<AdapterChainStep> steps,
    Action<List<AdapterChainStep>> register
) : ITypedAdapterChainFinalBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly List<AdapterChainStep> _steps =
        steps ?? throw new ArgumentNullException(nameof(steps));

    private readonly Action<List<AdapterChainStep>> _register =
        register ?? throw new ArgumentNullException(nameof(register));

    public void Build() => _register(_steps);
}
