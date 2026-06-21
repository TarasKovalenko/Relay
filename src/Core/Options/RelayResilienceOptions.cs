namespace Relay.Core.Options;

/// <summary>
/// Per-relay resilience settings applied when a multi-relay uses the
/// <see cref="Enums.RelayStrategy.Failover"/> or <see cref="Enums.RelayStrategy.FirstSuccessful"/>
/// strategy. Each relay is retried up to <see cref="MaxAttempts"/> times before the multi-relay
/// moves on to the next relay.
/// </summary>
public sealed record RelayResilienceOptions
{
    /// <summary>
    /// Maximum number of attempts per relay (including the first). Must be at least 1.
    /// A value of 1 (the default) disables retries.
    /// </summary>
    public int MaxAttempts { get; init; } = 1;

    /// <summary>
    /// Delay between attempts on the same relay. Defaults to no delay.
    /// </summary>
    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Multiplier applied to <see cref="Delay"/> after each failed attempt (exponential backoff).
    /// Defaults to 1.0 (constant delay).
    /// </summary>
    public double BackoffFactor { get; init; } = 1.0;

    /// <summary>The default options: a single attempt, no retry.</summary>
    public static RelayResilienceOptions None { get; } = new();
}
