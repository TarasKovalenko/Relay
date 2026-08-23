using System.Diagnostics;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Relay.Core.Options;
using Relay.Diagnostics;

namespace Relay.Core.Implementations;

public sealed class MultiRelay<TInterface> : IMultiRelay<TInterface>
    where TInterface : class
{
    private readonly List<TInterface> _relays;
    private readonly RelayStrategy _strategy;
    private readonly RelayResilienceOptions _resilience;
    private readonly RelayCircuitBreakerOptions _circuitBreaker;
    private readonly TimeProvider _timeProvider;
    private readonly RelayCircuitBreaker[] _breakers;
    private int _roundRobinIndex;

    public MultiRelay(IEnumerable<TInterface> relays, RelayStrategy strategy)
        : this(relays, strategy, RelayResilienceOptions.None) { }

    public MultiRelay(
        IEnumerable<TInterface> relays,
        RelayStrategy strategy,
        RelayResilienceOptions resilience
    )
        : this(relays, strategy, resilience, TimeProvider.System) { }

    /// <param name="relays">The relays to route to.</param>
    /// <param name="strategy">How work is distributed across the relays.</param>
    /// <param name="resilience">Per-relay retry settings.</param>
    /// <param name="timeProvider">
    /// Clock used for retry delays. Pass a fake one in tests to make backoff deterministic.
    /// </param>
    public MultiRelay(
        IEnumerable<TInterface> relays,
        RelayStrategy strategy,
        RelayResilienceOptions resilience,
        TimeProvider timeProvider
    )
        : this(relays, strategy, resilience, RelayCircuitBreakerOptions.Disabled, timeProvider) { }

    /// <param name="relays">The relays to route to.</param>
    /// <param name="strategy">How work is distributed across the relays.</param>
    /// <param name="resilience">Per-relay retry settings.</param>
    /// <param name="circuitBreaker">
    /// Per-relay circuit breaker settings. A relay that fails repeatedly is skipped until the
    /// break elapses, instead of being retried on every call.
    /// </param>
    /// <param name="timeProvider">
    /// Clock used for retry delays and circuit breaker timing. Pass a fake one in tests to make
    /// both deterministic.
    /// </param>
    public MultiRelay(
        IEnumerable<TInterface> relays,
        RelayStrategy strategy,
        RelayResilienceOptions resilience,
        RelayCircuitBreakerOptions circuitBreaker,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(relays);
        ArgumentNullException.ThrowIfNull(resilience);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (resilience.MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resilience),
                "MaxAttempts must be at least 1"
            );
        }
        if (circuitBreaker.IsEnabled && circuitBreaker.BreakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(circuitBreaker),
                "BreakDuration must be greater than zero"
            );
        }
        _relays = relays.ToList();
        _strategy = strategy;
        _resilience = resilience;
        _circuitBreaker = circuitBreaker;
        _timeProvider = timeProvider;
        _breakers = [.. _relays.Select(_ => new RelayCircuitBreaker(circuitBreaker, timeProvider))];
    }

    public IEnumerable<TInterface> GetRelays() => _relays;

    /// <summary>
    /// The current circuit breaker state of every relay, in registration order. Useful for
    /// health endpoints and for asserting behaviour in tests.
    /// </summary>
    public IReadOnlyList<RelayCircuitSnapshot> GetCircuitSnapshots() =>
        [
            .. _relays.Select(
                (relay, index) =>
                    new RelayCircuitSnapshot(
                        relay.GetType(),
                        _breakers[index].State,
                        _breakers[index].ConsecutiveFailures
                    )
            ),
        ];

    public async Task<IEnumerable<TResult>> RelayToAllWithResults<TResult>(
        Func<TInterface, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var activity = RelayDiagnostics.ActivitySource.StartActivity(
            "MultiRelay.RelayToAllWithResults"
        );
        activity?.SetTag("relay.strategy", _strategy.ToString());
        activity?.SetTag("relay.count", _relays.Count);

        var startTimestamp = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var results = _strategy switch
            {
                RelayStrategy.Broadcast => await ExecuteBroadcast(operation, cancellationToken),
                RelayStrategy.Failover =>
                [
                    await ExecuteFirstSuccessful(operation, cancellationToken),
                ],
                RelayStrategy.FirstSuccessful =>
                [
                    await ExecuteFirstSuccessful(operation, cancellationToken),
                ],
                RelayStrategy.Parallel => await ExecuteParallel(operation),
                RelayStrategy.RoundRobin => (IEnumerable<TResult>)
                    [await operation(await GetNextRelay())],
                _ => throw new NotSupportedException(
                    $"Strategy {_strategy} not supported for results"
                ),
            };

            succeeded = true;
            return results;
        }
        finally
        {
            RelayMetrics.RecordRelayOperation(
                _strategy.ToString(),
                "RelayToAllWithResults",
                _relays.Count,
                startTimestamp,
                succeeded
            );
        }
    }

    public async Task RelayToAll(
        Func<TInterface, Task> operation,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var activity = RelayDiagnostics.ActivitySource.StartActivity("MultiRelay.RelayToAll");
        activity?.SetTag("relay.strategy", _strategy.ToString());
        activity?.SetTag("relay.count", _relays.Count);

        var startTimestamp = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            await ExecuteStrategy(operation, cancellationToken);
            succeeded = true;
        }
        finally
        {
            RelayMetrics.RecordRelayOperation(
                _strategy.ToString(),
                "RelayToAll",
                _relays.Count,
                startTimestamp,
                succeeded
            );
        }
    }

    private async Task ExecuteStrategy(
        Func<TInterface, Task> operation,
        CancellationToken cancellationToken
    )
    {
        switch (_strategy)
        {
            case RelayStrategy.Broadcast:
                foreach (var relay in _relays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await operation(relay);
                }
                break;
            case RelayStrategy.Failover:
            case RelayStrategy.FirstSuccessful:
                await ExecuteFirstSuccessful(
                    async relay =>
                    {
                        await operation(relay);
                        return true;
                    },
                    cancellationToken
                );
                break;
            case RelayStrategy.Parallel:
                await WhenAllWithAggregate(_relays.Select(operation).ToArray());
                break;
            case RelayStrategy.RoundRobin:
                await operation(await GetNextRelay());
                break;
            default:
                throw new NotSupportedException($"Strategy {_strategy} not supported");
        }
    }

    public Task<TInterface> GetNextRelay()
    {
        if (_relays.Count == 0)
        {
            throw new InvalidOperationException("No relays available");
        }

        // Lock-free round-robin: atomically grab and advance the index.
        var index = Interlocked.Increment(ref _roundRobinIndex) - 1;
        var relay = _relays[(int)((uint)index % (uint)_relays.Count)];
        return Task.FromResult(relay);
    }

    private async Task<IEnumerable<TResult>> ExecuteBroadcast<TResult>(
        Func<TInterface, Task<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        var results = new List<TResult>(_relays.Count);
        foreach (var relay in _relays)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await operation(relay));
        }
        return results;
    }

    private async Task<TResult> ExecuteFirstSuccessful<TResult>(
        Func<TInterface, Task<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        if (_relays.Count == 0)
        {
            throw new InvalidOperationException("No relays available");
        }

        var failures = new List<Exception>(_relays.Count);
        var skipped = 0;

        for (var index = 0; index < _relays.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relay = _relays[index];
            var breaker = _breakers[index];

            // An open circuit means this relay failed repeatedly and recently: skip it outright
            // rather than paying for another timeout.
            if (!breaker.TryEnter(out var entryTransition))
            {
                skipped++;
                RelayMetrics.RecordSkippedRelay(relay.GetType());
                failures.Add(
                    new InvalidOperationException(
                        $"Circuit open for relay {relay.GetType().Name}; skipped"
                    )
                );
                continue;
            }

            ReportTransition(relay, entryTransition);

            try
            {
                var result = await ExecuteWithRetry(relay, operation, cancellationToken);
                ReportTransition(relay, breaker.OnSuccess());
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ReportTransition(relay, breaker.OnFailure());
                failures.Add(ex);
            }
        }

        // Keep every failure: knowing why the last relay failed is rarely enough to debug a failover.
        var attempted = _relays.Count - skipped;
        throw new InvalidOperationException(
            skipped == 0
                ? $"No relay succeeded after trying {_relays.Count} relay(s)"
                : $"No relay succeeded: {attempted} relay(s) tried, {skipped} skipped by an open circuit",
            failures.Count == 1 ? failures[0] : new AggregateException(failures)
        );
    }

    private static void ReportTransition(TInterface relay, RelayCircuitState? transitionedTo)
    {
        if (transitionedTo is { } state)
        {
            RelayMetrics.RecordCircuitTransition(relay.GetType(), state);
        }
    }

    /// <summary>
    /// Invokes <paramref name="operation"/> on a single relay, retrying up to
    /// <c>MaxAttempts</c> times with the configured delay/backoff before giving up.
    /// </summary>
    private async Task<TResult> ExecuteWithRetry<TResult>(
        TInterface relay,
        Func<TInterface, Task<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        var delay = _resilience.Delay;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _resilience.MaxAttempts; attempt++)
        {
            try
            {
                return await operation(relay);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < _resilience.MaxAttempts && delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, _timeProvider, cancellationToken);
                    delay *= _resilience.BackoffFactor;
                }
            }
        }

        throw lastException!;
    }

    private async Task<IEnumerable<TResult>> ExecuteParallel<TResult>(
        Func<TInterface, Task<TResult>> operation
    )
    {
        var tasks = _relays.Select(operation).ToArray();
        await WhenAllWithAggregate(tasks);
        return tasks.Select(t => t.Result);
    }

    /// <summary>
    /// Awaits every task and surfaces all failures. <c>await Task.WhenAll(...)</c> rethrows only
    /// the first exception, which hides the other relays that failed in the same batch.
    /// </summary>
    private static async Task WhenAllWithAggregate(Task[] tasks)
    {
        var whenAll = Task.WhenAll(tasks);
        try
        {
            await whenAll;
        }
        catch (Exception) when (whenAll.Exception is not null)
        {
            throw whenAll.Exception.InnerExceptions.Count == 1
                ? whenAll.Exception.InnerExceptions[0]
                : whenAll.Exception;
        }
    }
}
