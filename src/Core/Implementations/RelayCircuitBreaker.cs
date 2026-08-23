using Relay.Core.Enums;
using Relay.Core.Options;

namespace Relay.Core.Implementations;

/// <summary>
/// A snapshot of one relay's circuit breaker, for diagnostics and tests.
/// </summary>
/// <param name="RelayType">The relay implementation the circuit guards.</param>
/// <param name="State">The state at the moment the snapshot was taken.</param>
/// <param name="ConsecutiveFailures">Failures observed since the last success.</param>
public sealed record RelayCircuitSnapshot(
    Type RelayType,
    RelayCircuitState State,
    int ConsecutiveFailures
);

/// <summary>
/// Tracks consecutive failures for a single relay and decides whether it should be tried.
/// </summary>
/// <remarks>
/// Deliberately small: no background timers and no thread of its own. State only moves when a
/// call asks to enter or reports its outcome, and every transition is derived from
/// <see cref="TimeProvider"/> so tests can drive it without waiting.
/// </remarks>
internal sealed class RelayCircuitBreaker(
    RelayCircuitBreakerOptions options,
    TimeProvider timeProvider
)
{
    private readonly Lock _gate = new();
    private RelayCircuitState _state = RelayCircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private bool _trialInFlight;

    internal RelayCircuitState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    internal int ConsecutiveFailures
    {
        get
        {
            lock (_gate)
            {
                return _consecutiveFailures;
            }
        }
    }

    /// <summary>
    /// Asks permission to call the relay. Returns <c>false</c> while the circuit is open, or when
    /// another caller already holds the single half-open trial.
    /// </summary>
    /// <param name="transitionedTo">
    /// The state the circuit moved to as a result of this call, or <c>null</c> when it did not move.
    /// </param>
    internal bool TryEnter(out RelayCircuitState? transitionedTo)
    {
        transitionedTo = null;

        if (!options.IsEnabled)
        {
            return true;
        }

        lock (_gate)
        {
            switch (_state)
            {
                case RelayCircuitState.Closed:
                    return true;

                case RelayCircuitState.Open:
                    if (timeProvider.GetUtcNow() - _openedAt < options.BreakDuration)
                    {
                        return false;
                    }

                    // The break elapsed: let exactly one request through to probe the relay.
                    _state = RelayCircuitState.HalfOpen;
                    _trialInFlight = true;
                    transitionedTo = RelayCircuitState.HalfOpen;
                    return true;

                case RelayCircuitState.HalfOpen:
                    if (_trialInFlight)
                    {
                        return false;
                    }

                    _trialInFlight = true;
                    return true;

                default:
                    return true;
            }
        }
    }

    /// <summary>Records a successful call, closing the circuit if it was not already closed.</summary>
    internal RelayCircuitState? OnSuccess()
    {
        if (!options.IsEnabled)
        {
            return null;
        }

        lock (_gate)
        {
            _trialInFlight = false;
            _consecutiveFailures = 0;

            if (_state == RelayCircuitState.Closed)
            {
                return null;
            }

            _state = RelayCircuitState.Closed;
            return RelayCircuitState.Closed;
        }
    }

    /// <summary>
    /// Records a failed call. Opens the circuit once the threshold is reached, and re-opens it
    /// immediately when a half-open trial fails.
    /// </summary>
    internal RelayCircuitState? OnFailure()
    {
        if (!options.IsEnabled)
        {
            return null;
        }

        lock (_gate)
        {
            var wasHalfOpen = _state == RelayCircuitState.HalfOpen;
            _trialInFlight = false;
            _consecutiveFailures++;

            if (!wasHalfOpen && _consecutiveFailures < options.FailureThreshold)
            {
                return null;
            }

            _openedAt = timeProvider.GetUtcNow();
            if (_state == RelayCircuitState.Open)
            {
                // Already open (a concurrent caller tripped it): extend the break, no transition.
                return null;
            }

            _state = RelayCircuitState.Open;
            return RelayCircuitState.Open;
        }
    }
}
