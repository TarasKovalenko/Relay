using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

/// <summary>
/// Represents a step in an adapter chain
/// </summary>
public sealed class AdapterChainStep
{
    public Type SourceType { get; init; } = null!;
    public Type TargetType { get; init; } = null!;
    public Type AdapterType { get; init; } = null!;
    public bool IsFinalStep { get; init; }
}

/// <summary>
/// Implementation of an adapter chain that can execute a sequence of transformations
/// </summary>
public sealed class AdapterChain<TResult>(
    IServiceProvider serviceProvider,
    List<AdapterChainStep> steps
) : IAdapterChain<TResult>
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly List<AdapterChainStep> _steps =
        steps ?? throw new ArgumentNullException(nameof(steps));

    public TResult Execute<TSource>(TSource source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (_steps.Count == 0)
            throw new InvalidOperationException("Adapter chain has no steps configured");

        object current = source;
        var sourceType = typeof(TSource);

        // Verify first step matches source type
        var firstStep = _steps[0];
        if (firstStep.SourceType != sourceType)
        {
            throw new InvalidOperationException(
                $"Chain expects source type {firstStep.SourceType.Name} but received {sourceType.Name}"
            );
        }

        // Execute each step in the chain
        foreach (var step in _steps)
        {
            var adapter = _serviceProvider.GetRequiredService(step.AdapterType);

            // Use reflection to call the Adapt method
            var adaptMethod = step
                .AdapterType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAdapter<,>))
                .SelectMany(i => i.GetMethods())
                .FirstOrDefault(m => m.Name == "Adapt");

            if (adaptMethod == null)
            {
                throw new InvalidOperationException(
                    $"Adapter {step.AdapterType.Name} does not implement IAdapter<,> properly"
                );
            }

            current = adaptMethod.Invoke(adapter, [current])!;
        }

        if (current is not TResult result)
        {
            throw new InvalidOperationException(
                $"Final result type {current?.GetType().Name} cannot be cast to expected type {typeof(TResult).Name}"
            );
        }

        return result;
    }
}

/// <summary>
/// Implementation of a typed adapter chain with known source and target types
/// </summary>
public sealed class TypedAdapterChain<TSource, TTarget>(IAdapterChain<TTarget> innerChain)
    : ITypedAdapterChain<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    private readonly IAdapterChain<TTarget> _innerChain =
        innerChain ?? throw new ArgumentNullException(nameof(innerChain));

    public TTarget Execute(TSource source)
    {
        return _innerChain.Execute(source);
    }
}
