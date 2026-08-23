using System.Diagnostics;
using System.Diagnostics.Metrics;
using Relay.Core.Enums;

namespace Relay.Diagnostics;

/// <summary>
/// The <see cref="System.Diagnostics.Metrics.Meter"/> Relay publishes its instruments on.
/// </summary>
/// <remarks>
/// Traces tell you what one request did; metrics tell you whether the system is healthy. Hook the
/// meter up with <c>builder.Services.AddOpenTelemetry().WithMetrics(m =&gt;
/// m.AddMeter(RelayDiagnostics.MeterName))</c>. Instruments do no work while nothing is listening.
/// </remarks>
public static class RelayMetrics
{
    internal static readonly Meter Meter = new(RelayDiagnostics.MeterName, RelayDiagnostics.Version);

    private static readonly Counter<long> ChainExecutions = Meter.CreateCounter<long>(
        "relay.adapter_chain.executions",
        unit: "{execution}",
        description: "Number of adapter chain executions."
    );

    private static readonly Histogram<double> ChainDuration = Meter.CreateHistogram<double>(
        "relay.adapter_chain.duration",
        unit: "ms",
        description: "Duration of adapter chain executions."
    );

    private static readonly Counter<long> RelayOperations = Meter.CreateCounter<long>(
        "relay.multi_relay.operations",
        unit: "{operation}",
        description: "Number of multi-relay operations, by strategy and outcome."
    );

    private static readonly Histogram<double> RelayDuration = Meter.CreateHistogram<double>(
        "relay.multi_relay.duration",
        unit: "ms",
        description: "Duration of multi-relay operations."
    );

    private static readonly Counter<long> CircuitTransitions = Meter.CreateCounter<long>(
        "relay.circuit_breaker.transitions",
        unit: "{transition}",
        description: "Circuit breaker state transitions, by relay and target state."
    );

    private static readonly UpDownCounter<long> OpenCircuits = Meter.CreateUpDownCounter<long>(
        "relay.circuit_breaker.open_circuits",
        unit: "{circuit}",
        description: "Number of relay circuits currently open."
    );

    private static readonly Counter<long> SkippedRelays = Meter.CreateCounter<long>(
        "relay.circuit_breaker.skipped",
        unit: "{relay}",
        description: "Relay calls skipped because the circuit was open."
    );

    internal static void RecordChain(
        string chainKind,
        Type resultType,
        int steps,
        long startTimestamp,
        bool succeeded
    )
    {
        if (!ChainExecutions.Enabled && !ChainDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { "relay.chain.kind", chainKind },
            { "relay.chain.result_type", resultType.Name },
            { "relay.chain.steps", steps },
            { "relay.outcome", succeeded ? "success" : "failure" },
        };

        ChainExecutions.Add(1, tags);
        ChainDuration.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            tags
        );
    }

    internal static void RecordRelayOperation(
        string strategy,
        string operation,
        int relayCount,
        long startTimestamp,
        bool succeeded
    )
    {
        if (!RelayOperations.Enabled && !RelayDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { "relay.strategy", strategy },
            { "relay.operation", operation },
            { "relay.count", relayCount },
            { "relay.outcome", succeeded ? "success" : "failure" },
        };

        RelayOperations.Add(1, tags);
        RelayDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, tags);
    }

    internal static void RecordCircuitTransition(Type relayType, RelayCircuitState state)
    {
        if (CircuitTransitions.Enabled)
        {
            CircuitTransitions.Add(
                1,
                new TagList
                {
                    { "relay.type", relayType.Name },
                    { "relay.circuit.state", state.ToString() },
                }
            );
        }

        // Track how many circuits are open right now, so a dashboard can alarm on "> 0".
        var delta = state switch
        {
            RelayCircuitState.Open => 1L,
            RelayCircuitState.Closed => -1L,
            _ => 0L,
        };

        if (delta != 0 && OpenCircuits.Enabled)
        {
            OpenCircuits.Add(delta, new TagList { { "relay.type", relayType.Name } });
        }
    }

    internal static void RecordSkippedRelay(Type relayType)
    {
        if (SkippedRelays.Enabled)
        {
            SkippedRelays.Add(1, new TagList { { "relay.type", relayType.Name } });
        }
    }
}
