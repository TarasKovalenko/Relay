namespace Relay.Core.Options;

/// <summary>
/// Per-relay circuit breaker settings for a multi-relay using the
/// <see cref="Enums.RelayStrategy.Failover"/> or <see cref="Enums.RelayStrategy.FirstSuccessful"/>
/// strategy.
/// </summary>
/// <remarks>
/// Retry and failover alone keep paying the full cost of a relay that is down: every request
/// still waits for it to time out before moving on. A breaker remembers that outcome, so a relay
/// that failed <see cref="FailureThreshold"/> times in a row is skipped outright until
/// <see cref="BreakDuration"/> has passed, and is then given a single trial request before it is
/// trusted again.
/// </remarks>
public sealed record RelayCircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures that opens the circuit. Must be at least 1.
    /// </summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>
    /// How long the circuit stays open before a single trial request is allowed through.
    /// Must be greater than zero.
    /// </summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>No circuit breaking: every relay is tried on every call.</summary>
    public static RelayCircuitBreakerOptions Disabled { get; } = new() { FailureThreshold = 0 };

    /// <summary><c>true</c> when the breaker is configured to trip.</summary>
    public bool IsEnabled => FailureThreshold > 0;
}
