using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;
using Relay.Diagnostics;

namespace Relay.Core.Implementations;

/// <summary>
/// Implementation of an asynchronous adapter chain that executes a sequence of
/// <see cref="IAsyncAdapter{TSource,TTarget}"/> transformations.
/// </summary>
public sealed class AsyncAdapterChain<TResult>(
    IServiceProvider serviceProvider,
    List<AdapterChainStep> steps
) : IAsyncAdapterChain<TResult>
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly List<AdapterChainStep> _steps =
        steps ?? throw new ArgumentNullException(nameof(steps));

    // Cache the resolved AdaptAsync MethodInfo and its Task<T>.Result getter per adapter type.
    private static readonly ConcurrentDictionary<Type, (MethodInfo Method, PropertyInfo Result)> AdaptAsyncCache =
        new();

    public async Task<TResult> ExecuteAsync<TSource>(
        TSource source,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Adapter chain has no steps configured");
        }

        object current = source;
        var sourceType = typeof(TSource);

        var firstStep = _steps[0];
        if (firstStep.SourceType != sourceType)
        {
            throw new InvalidOperationException(
                $"Chain expects source type {firstStep.SourceType.Name} but received {sourceType.Name}"
            );
        }

        using var chainActivity = RelayDiagnostics.ActivitySource.StartActivity("AsyncAdapterChain.Execute");
        chainActivity?.SetTag("relay.chain.result_type", typeof(TResult).Name);
        chainActivity?.SetTag("relay.chain.source_type", sourceType.Name);
        chainActivity?.SetTag("relay.chain.steps", _steps.Count);

        foreach (var step in _steps)
        {
            using var stepActivity = RelayDiagnostics.ActivitySource.StartActivity("AsyncAdapterChain.Step");
            stepActivity?.SetTag("relay.adapter", step.AdapterType.Name);
            stepActivity?.SetTag("relay.adapter.target", step.TargetType.Name);

            var adapter = _serviceProvider.GetRequiredService(step.AdapterType);

            var (method, resultProperty) = AdaptAsyncCache.GetOrAdd(
                step.AdapterType,
                static type =>
                {
                    var m =
                        type.GetInterfaces()
                            .Where(i =>
                                i.IsGenericType
                                && i.GetGenericTypeDefinition() == typeof(IAsyncAdapter<,>)
                            )
                            .SelectMany(i => i.GetMethods())
                            .FirstOrDefault(x => x.Name == "AdaptAsync")
                        ?? throw new InvalidOperationException(
                            $"Adapter {type.Name} does not implement IAsyncAdapter<,> properly"
                        );
                    var resultProp =
                        m.ReturnType.GetProperty("Result")
                        ?? throw new InvalidOperationException(
                            $"Adapter {type.Name}.AdaptAsync must return Task<T>"
                        );
                    return (m, resultProp);
                }
            );

            Task task;
            try
            {
                task = (Task)method.Invoke(adapter, [current, cancellationToken])!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }

            await task.ConfigureAwait(false);
            current = resultProperty.GetValue(task)!;
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
