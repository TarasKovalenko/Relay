using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

/// <summary>
/// Creates <see cref="AdapterChainStep"/> instances that carry a strongly-typed invoker.
/// The builders know the source/target/adapter types statically, so the delegate can be
/// closed over here and the chain never has to fall back to reflection at execution time.
/// </summary>
internal static class AdapterChainStepFactory
{
    internal static AdapterChainStep Create<TSource, TTarget, TAdapter>(bool isFinalStep)
        where TAdapter : class, IAdapter<TSource, TTarget>
    {
        return new AdapterChainStep
        {
            SourceType = typeof(TSource),
            TargetType = typeof(TTarget),
            AdapterType = typeof(TAdapter),
            IsFinalStep = isFinalStep,
            Invoker = static (provider, source) =>
            {
                object? result = provider
                    .GetRequiredService<TAdapter>()
                    .Adapt((TSource)source);

                return result
                    ?? throw new InvalidOperationException(
                        $"Adapter {typeof(TAdapter).Name} returned null for {typeof(TTarget).Name}"
                    );
            },
        };
    }

    internal static AdapterChainStep CreateAsync<TSource, TTarget, TAdapter>(bool isFinalStep)
        where TAdapter : class, IAsyncAdapter<TSource, TTarget>
    {
        return new AdapterChainStep
        {
            SourceType = typeof(TSource),
            TargetType = typeof(TTarget),
            AdapterType = typeof(TAdapter),
            IsFinalStep = isFinalStep,
            AsyncInvoker = static async (provider, source, cancellationToken) =>
            {
                object? result = await provider
                    .GetRequiredService<TAdapter>()
                    .AdaptAsync((TSource)source, cancellationToken)
                    .ConfigureAwait(false);

                return result
                    ?? throw new InvalidOperationException(
                        $"Adapter {typeof(TAdapter).Name} returned null for {typeof(TTarget).Name}"
                    );
            },
        };
    }
}
