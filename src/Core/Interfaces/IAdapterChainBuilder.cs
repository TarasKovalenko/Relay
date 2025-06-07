namespace Relay.Core.Interfaces;

/// <summary>
/// Builder for configuring adapter chains
/// </summary>
public interface IAdapterChainBuilder<TResult>
{
    /// <summary>
    /// Specifies the source type for the adapter chain
    /// </summary>
    IAdapterChainFromBuilder<TSource, TResult> From<TSource>() where TSource : class;
}

/// <summary>
/// Builder for configuring adapter chains after specifying the source
/// </summary>
public interface IAdapterChainFromBuilder<TSource, TResult>
    where TSource : class
{
    /// <summary>
    /// Adds an intermediate transformation step to the chain
    /// </summary>
    IAdapterChainThenBuilder<TTarget, TResult> Then<TTarget, TAdapter>() 
        where TTarget : class
        where TAdapter : class, IAdapter<TSource, TTarget>;

    /// <summary>
    /// Adds the final transformation step to the chain
    /// </summary>
    IAdapterChainFinalBuilder<TResult> Finally<TAdapter>() 
        where TAdapter : class, IAdapter<TSource, TResult>;
}

/// <summary>
/// Builder for configuring subsequent steps in the adapter chain
/// </summary>
public interface IAdapterChainThenBuilder<TSource, TResult>
    where TSource : class
{
    /// <summary>
    /// Adds another intermediate transformation step to the chain
    /// </summary>
    IAdapterChainThenBuilder<TTarget, TResult> Then<TTarget, TAdapter>() 
        where TTarget : class
        where TAdapter : class, IAdapter<TSource, TTarget>;

    /// <summary>
    /// Adds the final transformation step to the chain
    /// </summary>
    IAdapterChainFinalBuilder<TResult> Finally<TAdapter>() 
        where TAdapter : class, IAdapter<TSource, TResult>;
}

/// <summary>
/// Final builder for completing the adapter chain configuration
/// </summary>
public interface IAdapterChainFinalBuilder<TResult>
{
    /// <summary>
    /// Builds the adapter chain and registers it with the service collection
    /// </summary>
    void Build();
}

/// <summary>
/// Builder for typed adapter chains with known source and target types
/// </summary>
public interface ITypedAdapterChainBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    /// <summary>
    /// Adds an intermediate transformation step to the chain
    /// </summary>
    ITypedAdapterChainThenBuilder<TIntermediate, TTarget> Then<TIntermediate, TAdapter>() 
        where TIntermediate : class
        where TAdapter : class, IAdapter<TSource, TIntermediate>;

    /// <summary>
    /// Adds the final transformation step to the chain (TSource -> TTarget)
    /// </summary>
    ITypedAdapterChainFinalBuilder<TSource, TTarget> Then<TAdapter>() 
        where TAdapter : class, IAdapter<TSource, TTarget>;
}

/// <summary>
/// Builder for subsequent steps in typed adapter chains
/// </summary>
public interface ITypedAdapterChainThenBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    /// <summary>
    /// Adds another intermediate transformation step to the chain
    /// </summary>
    ITypedAdapterChainThenBuilder<TIntermediate, TTarget> Then<TIntermediate, TAdapter>() 
        where TIntermediate : class
        where TAdapter : class, IAdapter<TSource, TIntermediate>;

    /// <summary>
    /// Adds the final transformation step to the chain
    /// </summary>
    ITypedAdapterChainFinalBuilder<TSource, TTarget> Then<TAdapter>() 
        where TAdapter : class, IAdapter<TSource, TTarget>;
}

/// <summary>
/// Final builder for completing the typed adapter chain configuration
/// </summary>
public interface ITypedAdapterChainFinalBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    /// <summary>
    /// Builds the typed adapter chain and registers it with the service collection
    /// </summary>
    void Build();
}
