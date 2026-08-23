using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Core.Options;
using Relay.Diagnostics;
using Shouldly;

namespace Relay.Tests.Diagnostics;

public class RelayMetricsTests
{
    [Fact]
    public void MeterName_ShouldMatchTheActivitySourceName()
    {
        RelayDiagnostics.MeterName.ShouldBe("Relay");
        RelayMetrics.Meter.Name.ShouldBe(RelayDiagnostics.MeterName);
    }

    [Fact]
    public void SyncChain_ShouldRecordDurationAndOutcome()
    {
        using var collector = new MetricCollector<double>(
            RelayMetrics.Meter,
            "relay.adapter_chain.duration"
        );

        var services = new ServiceCollection();
        services.AddRelayServices();
        services
            .AddAdapterChain<FinalData>()
            .From<SourceData>()
            .Then<IntermediateData, SourceToIntermediateAdapter>()
            .Finally<IntermediateToFinalAdapter>()
            .Build();

        using var provider = services.BuildServiceProvider();
        provider
            .GetRequiredService<IAdapterChain<FinalData>>()
            .Execute(new SourceData("payload"));

        var measurement = collector.LastMeasurement.ShouldNotBeNull();
        measurement.Value.ShouldBeGreaterThanOrEqualTo(0);
        measurement.Tags["relay.chain.kind"].ShouldBe("sync");
        measurement.Tags["relay.chain.result_type"].ShouldBe(nameof(FinalData));
        measurement.Tags["relay.chain.steps"].ShouldBe(2);
        measurement.Tags["relay.outcome"].ShouldBe("success");
    }

    [Fact]
    public void FailingChain_ShouldRecordAFailureOutcome()
    {
        using var collector = new MetricCollector<long>(
            RelayMetrics.Meter,
            "relay.adapter_chain.executions"
        );

        var chain = new AdapterChain<FinalData>(
            new ServiceCollection().BuildServiceProvider(),
            [
                new AdapterChainStep
                {
                    SourceType = typeof(SourceData),
                    TargetType = typeof(FinalData),
                    AdapterType = typeof(SourceToIntermediateAdapter),
                    IsFinalStep = true,
                },
            ]
        );

        Should.Throw<Exception>(() => chain.Execute(new SourceData("payload")));

        collector.LastMeasurement.ShouldNotBeNull().Tags["relay.outcome"].ShouldBe("failure");
    }

    [Fact]
    public async Task AsyncChain_ShouldRecordTheAsyncKind()
    {
        using var collector = new MetricCollector<double>(
            RelayMetrics.Meter,
            "relay.adapter_chain.duration"
        );

        var services = new ServiceCollection();
        services.AddRelayServices();
        services
            .AddAsyncAdapterChain<FinalData>()
            .From<SourceData>()
            .Finally<AsyncSourceToFinalAdapter>()
            .Build();

        using var provider = services.BuildServiceProvider();
        await provider
            .GetRequiredService<IAsyncAdapterChain<FinalData>>()
            .ExecuteAsync(new SourceData("payload"));

        collector.LastMeasurement.ShouldNotBeNull().Tags["relay.chain.kind"].ShouldBe("async");
    }

    [Fact]
    public async Task MultiRelay_ShouldRecordStrategyAndOutcome()
    {
        using var collector = new MetricCollector<long>(
            RelayMetrics.Meter,
            "relay.multi_relay.operations"
        );

        var relay = new MultiRelay<ITestService>(
            [new TestServiceA(), new TestServiceB()],
            RelayStrategy.Broadcast
        );

        await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        var measurement = collector.LastMeasurement.ShouldNotBeNull();
        measurement.Value.ShouldBe(1);
        measurement.Tags["relay.strategy"].ShouldBe(nameof(RelayStrategy.Broadcast));
        measurement.Tags["relay.operation"].ShouldBe("RelayToAllWithResults");
        measurement.Tags["relay.count"].ShouldBe(2);
        measurement.Tags["relay.outcome"].ShouldBe("success");
    }

    [Fact]
    public async Task MultiRelay_ShouldRecordAFailedOperation()
    {
        using var collector = new MetricCollector<long>(
            RelayMetrics.Meter,
            "relay.multi_relay.operations"
        );

        var relay = new MultiRelay<ITestService>([new BrokenRelay()], RelayStrategy.Failover);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            relay.RelayToAll(s => s.ProcessAsync("payload"))
        );

        var measurement = collector.LastMeasurement.ShouldNotBeNull();
        measurement.Tags["relay.operation"].ShouldBe("RelayToAll");
        measurement.Tags["relay.outcome"].ShouldBe("failure");
    }

    [Fact]
    public async Task CircuitBreaker_ShouldRecordTransitionsAndOpenCircuitCount()
    {
        using var transitions = new MetricCollector<long>(
            RelayMetrics.Meter,
            "relay.circuit_breaker.transitions"
        );
        using var openCircuits = new MetricCollector<long>(
            RelayMetrics.Meter,
            "relay.circuit_breaker.open_circuits"
        );

        var timeProvider = new FakeTimeProvider();
        var broken = new BrokenRelay();
        var relay = new MultiRelay<ITestService>(
            [broken, new TestServiceB()],
            RelayStrategy.Failover,
            RelayResilienceOptions.None,
            new RelayCircuitBreakerOptions
            {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromSeconds(10),
            },
            timeProvider
        );

        await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        transitions
            .LastMeasurement.ShouldNotBeNull()
            .Tags["relay.circuit.state"]
            .ShouldBe(nameof(RelayCircuitState.Open));
        openCircuits.LastMeasurement.ShouldNotBeNull().Value.ShouldBe(1);

        // Recover: the trial after the break succeeds, so the circuit closes again.
        broken.Fail = false;
        timeProvider.Advance(TimeSpan.FromSeconds(11));
        await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        transitions
            .LastMeasurement.ShouldNotBeNull()
            .Tags["relay.circuit.state"]
            .ShouldBe(nameof(RelayCircuitState.Closed));
        openCircuits.LastMeasurement.ShouldNotBeNull().Value.ShouldBe(-1);
        openCircuits.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(0);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldCountSkippedRelays()
    {
        using var skipped = new MetricCollector<long>(
            RelayMetrics.Meter,
            "relay.circuit_breaker.skipped"
        );

        var relay = new MultiRelay<ITestService>(
            [new BrokenRelay(), new TestServiceB()],
            RelayStrategy.Failover,
            RelayResilienceOptions.None,
            new RelayCircuitBreakerOptions
            {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMinutes(5),
            },
            new FakeTimeProvider()
        );

        await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));
        skipped.LastMeasurement.ShouldBeNull("nothing is skipped until the circuit opens");

        await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        var measurement = skipped.LastMeasurement.ShouldNotBeNull();
        measurement.Value.ShouldBe(1);
        measurement.Tags["relay.type"].ShouldBe(nameof(BrokenRelay));
    }

    private sealed record SourceData(string Value);

    private sealed record IntermediateData(string Value);

    private sealed record FinalData(string Value);

    private sealed class SourceToIntermediateAdapter : IAdapter<SourceData, IntermediateData>
    {
        public IntermediateData Adapt(SourceData source) => new(source.Value);
    }

    private sealed class IntermediateToFinalAdapter : IAdapter<IntermediateData, FinalData>
    {
        public FinalData Adapt(IntermediateData source) => new(source.Value);
    }

    private sealed class AsyncSourceToFinalAdapter : IAsyncAdapter<SourceData, FinalData>
    {
        public Task<FinalData> AdaptAsync(
            SourceData source,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new FinalData(source.Value));
    }

    private sealed class BrokenRelay : ITestService
    {
        public bool Fail { get; set; } = true;

        public string Process(string input) => throw new NotSupportedException();

        public Task<string> ProcessAsync(string input) =>
            Fail
                ? Task.FromException<string>(new InvalidOperationException("relay is down"))
                : Task.FromResult("ok");
    }
}
