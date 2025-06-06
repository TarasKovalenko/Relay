namespace Relay.Core.Enums;

/// <summary>
/// Strategy for multi-relay execution
/// </summary>
public enum RelayStrategy
{
    /// <summary>Execute on all relays</summary>
    Broadcast,

    /// <summary>Try until one succeeds</summary>
    Failover,

    /// <summary>Return the first successful result</summary>
    FirstSuccessful,

    /// <summary>Distribute a load across relays</summary>
    RoundRobin,

    /// <summary>Execute all in parallel</summary>
    Parallel,
}
