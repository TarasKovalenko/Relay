namespace Relay.Core.Enums;

/// <summary>
/// The state of a relay's circuit breaker.
/// </summary>
public enum RelayCircuitState
{
    /// <summary>The relay is healthy and receives traffic.</summary>
    Closed,

    /// <summary>The relay failed too often and is skipped until the break duration elapses.</summary>
    Open,

    /// <summary>The break elapsed and a single trial request is allowed through.</summary>
    HalfOpen,
}
