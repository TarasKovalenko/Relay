namespace Relay.Core.Interfaces;

/// <summary>
/// Represents an adapter that can transform from one type to another
/// </summary>
public interface IAdapter<in TSource, out TTarget>
{
    /// <summary>
    /// Adapts/transforms the source object to the target type
    /// </summary>
    TTarget Adapt(TSource source);
}

/// <summary>
/// Represents an adapter chain that can execute a sequence of transformations
/// </summary>
public interface IAdapterChain<out TResult>
{
    /// <summary>
    /// Executes the adapter chain starting from the source data
    /// </summary>
    TResult Execute<TSource>(TSource source);
}

/// <summary>
/// Represents a typed adapter chain with known source and target types
/// </summary>
public interface ITypedAdapterChain<in TSource, out TTarget>
{
    /// <summary>
    /// Executes the adapter chain with the provided source
    /// </summary>
    TTarget Execute(TSource source);
}
