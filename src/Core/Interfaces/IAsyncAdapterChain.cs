namespace Relay.Core.Interfaces;

/// <summary>
/// Represents an adapter that asynchronously transforms from one type to another.
/// Use for transformation steps that perform I/O (HTTP, database, file access).
/// </summary>
public interface IAsyncAdapter<in TSource, TTarget>
{
    /// <summary>
    /// Asynchronously adapts/transforms the source object to the target type.
    /// </summary>
    Task<TTarget> AdaptAsync(TSource source, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an asynchronous adapter chain that executes a sequence of transformations.
/// </summary>
public interface IAsyncAdapterChain<TResult>
{
    /// <summary>
    /// Asynchronously executes the adapter chain starting from the source data.
    /// </summary>
    Task<TResult> ExecuteAsync<TSource>(
        TSource source,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Builder for configuring asynchronous adapter chains.
/// </summary>
public interface IAsyncAdapterChainBuilder<TResult>
{
    /// <summary>
    /// Specifies the source type for the adapter chain.
    /// </summary>
    IAsyncAdapterChainFromBuilder<TSource, TResult> From<TSource>()
        where TSource : class;
}

/// <summary>
/// Builder for configuring asynchronous adapter chains after specifying the source.
/// </summary>
public interface IAsyncAdapterChainFromBuilder<TSource, TResult>
    where TSource : class
{
    /// <summary>
    /// Adds an intermediate asynchronous transformation step to the chain.
    /// </summary>
    IAsyncAdapterChainThenBuilder<TTarget, TResult> Then<TTarget, TAdapter>()
        where TTarget : class
        where TAdapter : class, IAsyncAdapter<TSource, TTarget>;

    /// <summary>
    /// Adds the final asynchronous transformation step to the chain.
    /// </summary>
    IAsyncAdapterChainFinalBuilder<TResult> Finally<TAdapter>()
        where TAdapter : class, IAsyncAdapter<TSource, TResult>;
}

/// <summary>
/// Builder for configuring subsequent steps in the asynchronous adapter chain.
/// </summary>
public interface IAsyncAdapterChainThenBuilder<TSource, TResult>
    where TSource : class
{
    /// <summary>
    /// Adds another intermediate asynchronous transformation step to the chain.
    /// </summary>
    IAsyncAdapterChainThenBuilder<TTarget, TResult> Then<TTarget, TAdapter>()
        where TTarget : class
        where TAdapter : class, IAsyncAdapter<TSource, TTarget>;

    /// <summary>
    /// Adds the final asynchronous transformation step to the chain.
    /// </summary>
    IAsyncAdapterChainFinalBuilder<TResult> Finally<TAdapter>()
        where TAdapter : class, IAsyncAdapter<TSource, TResult>;
}

/// <summary>
/// Final builder for completing the asynchronous adapter chain configuration.
/// </summary>
public interface IAsyncAdapterChainFinalBuilder<TResult>
{
    /// <summary>
    /// Builds the asynchronous adapter chain and registers it with the service collection.
    /// </summary>
    void Build();
}
