using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;
using Relay.Diagnostics;

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

    /// <summary>
    /// Strongly-typed invoker supplied by the builders. When present the chain runs without
    /// reflection (and stays trim/AOT friendly).
    /// </summary>
    internal Func<IServiceProvider, object, object>? Invoker { get; init; }

    /// <summary>
    /// Strongly-typed asynchronous invoker supplied by the builders.
    /// </summary>
    internal Func<IServiceProvider, object, CancellationToken, Task<object>>? AsyncInvoker
    {
        get;
        init;
    }
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

    // Cache the resolved Adapt MethodInfo per adapter type. Only used by the reflection
    // fallback for steps that were constructed without a strongly-typed invoker.
    private static readonly ConcurrentDictionary<Type, MethodInfo> AdaptMethodCache = new();

    public TResult Execute<TSource>(TSource? source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Verify the first step matches the statically known source type.
        var firstStep = _steps.Count > 0 ? _steps[0] : null;
        if (firstStep is not null && firstStep.SourceType != typeof(TSource))
        {
            throw new InvalidOperationException(
                $"Chain expects source type {firstStep.SourceType.Name} but received {typeof(TSource).Name}"
            );
        }

        return ExecuteCore(source);
    }

    /// <summary>
    /// Executes the chain against a runtime-typed source. Used when the source type is only
    /// known at runtime (e.g. resolved from the container by a named chain factory).
    /// </summary>
    internal TResult ExecuteCore(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Adapter chain has no steps configured");
        }

        object current = source;

        // Verify the first step accepts the provided source instance.
        var firstStep = _steps[0];
        if (!firstStep.SourceType.IsInstanceOfType(source))
        {
            throw new InvalidOperationException(
                $"Chain expects source type {firstStep.SourceType.Name} but received {source.GetType().Name}"
            );
        }

        using var chainActivity = RelayDiagnostics.ActivitySource.StartActivity(
            "AdapterChain.Execute"
        );
        chainActivity?.SetTag("relay.chain.result_type", typeof(TResult).Name);
        chainActivity?.SetTag("relay.chain.source_type", firstStep.SourceType.Name);
        chainActivity?.SetTag("relay.chain.steps", _steps.Count);

        var startTimestamp = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            // Execute each step in the chain
            foreach (var step in _steps)
            {
                using var stepActivity = RelayDiagnostics.ActivitySource.StartActivity(
                    "AdapterChain.Step"
                );
                stepActivity?.SetTag("relay.adapter", step.AdapterType.Name);
                stepActivity?.SetTag("relay.adapter.target", step.TargetType.Name);

                current =
                    step.Invoker is not null
                        ? step.Invoker(_serviceProvider, current)
                        : InvokeByReflection(step, current);
            }

            if (current is not TResult result)
            {
                throw new InvalidOperationException(
                    $"Final result type {current?.GetType().Name} cannot be cast to expected type {typeof(TResult).Name}"
                );
            }

            succeeded = true;
            return result;
        }
        finally
        {
            RelayMetrics.RecordChain(
                "sync",
                typeof(TResult),
                _steps.Count,
                startTimestamp,
                succeeded
            );
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "Fallback only runs for chain steps built outside the Relay builders; "
            + "builder-produced steps carry a strongly-typed invoker and never reach this path."
    )]
    private object InvokeByReflection(AdapterChainStep step, object current)
    {
        var adapter = _serviceProvider.GetRequiredService(step.AdapterType);

        // Resolve the Adapt method once per adapter type, then reuse the cached MethodInfo.
        var adaptMethod = AdaptMethodCache.GetOrAdd(
            step.AdapterType,
            static type =>
                type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAdapter<,>))
                    .SelectMany(i => i.GetMethods())
                    .FirstOrDefault(m => m.Name == "Adapt")
                ?? throw new InvalidOperationException(
                    $"Adapter {type.Name} does not implement IAdapter<,> properly"
                )
        );

        try
        {
            return adaptMethod.Invoke(adapter, [current])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap and re-throw the inner exception to preserve original stack trace
            throw ex.InnerException;
        }
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
