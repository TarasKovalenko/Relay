using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Relay.Builders;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Core.Options;
using Shouldly;

namespace Relay.Tests.Core;

public class CircuitBreakerTests
{
    private static readonly RelayCircuitBreakerOptions TwoStrikes = new()
    {
        FailureThreshold = 2,
        BreakDuration = TimeSpan.FromSeconds(30),
    };

    private static MultiRelay<ITestService> Build(
        FakeTimeProvider timeProvider,
        RelayCircuitBreakerOptions breaker,
        params ITestService[] relays
    ) => new(relays, RelayStrategy.Failover, RelayResilienceOptions.None, breaker, timeProvider);

    private static async Task<string> Call(MultiRelay<ITestService> relay)
    {
        var results = await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));
        return results.Single();
    }

    [Fact]
    public async Task Circuit_ShouldOpenAfterConsecutiveFailuresAndSkipTheRelay()
    {
        var timeProvider = new FakeTimeProvider();
        var failing = new CountingRelay(alwaysFails: true);
        var healthy = new CountingRelay(alwaysFails: false);
        var relay = Build(timeProvider, TwoStrikes, failing, healthy);

        await Call(relay);
        await Call(relay);
        failing.Calls.ShouldBe(2);

        // Third call must not touch the failing relay at all.
        (await Call(relay)).ShouldBe("ok");
        failing.Calls.ShouldBe(2);
        relay.GetCircuitSnapshots()[0].State.ShouldBe(RelayCircuitState.Open);
    }

    [Fact]
    public async Task Circuit_ShouldStayClosedWhenFailuresAreNotConsecutive()
    {
        var timeProvider = new FakeTimeProvider();
        var flaky = new CountingRelay(alwaysFails: true);
        var relay = Build(timeProvider, new RelayCircuitBreakerOptions { FailureThreshold = 3 }, flaky);

        await Should.ThrowAsync<InvalidOperationException>(() => Call(relay));
        flaky.AlwaysFails = false;
        await Call(relay);
        flaky.AlwaysFails = true;
        await Should.ThrowAsync<InvalidOperationException>(() => Call(relay));

        var snapshot = relay.GetCircuitSnapshots()[0];
        snapshot.State.ShouldBe(RelayCircuitState.Closed);
        snapshot.ConsecutiveFailures.ShouldBe(1, "the success in between reset the counter");
    }

    [Fact]
    public async Task Circuit_ShouldAllowOneTrialAfterTheBreakElapses()
    {
        var timeProvider = new FakeTimeProvider();
        var failing = new CountingRelay(alwaysFails: true);
        var healthy = new CountingRelay(alwaysFails: false);
        var relay = Build(timeProvider, TwoStrikes, failing, healthy);

        await Call(relay);
        await Call(relay);
        await Call(relay);
        failing.Calls.ShouldBe(2);

        timeProvider.Advance(TimeSpan.FromSeconds(31));

        await Call(relay);
        failing.Calls.ShouldBe(3, "the break elapsed, so one trial call is allowed through");
        relay.GetCircuitSnapshots()[0].State.ShouldBe(RelayCircuitState.Open, "the trial failed");
    }

    [Fact]
    public async Task Circuit_ShouldCloseWhenTheTrialSucceeds()
    {
        var timeProvider = new FakeTimeProvider();
        var flaky = new CountingRelay(alwaysFails: true);
        var healthy = new CountingRelay(alwaysFails: false);
        var relay = Build(timeProvider, TwoStrikes, flaky, healthy);

        await Call(relay);
        await Call(relay);
        relay.GetCircuitSnapshots()[0].State.ShouldBe(RelayCircuitState.Open);

        flaky.AlwaysFails = false;
        timeProvider.Advance(TimeSpan.FromSeconds(31));
        await Call(relay);

        var snapshot = relay.GetCircuitSnapshots()[0];
        snapshot.State.ShouldBe(RelayCircuitState.Closed);
        snapshot.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public async Task Circuit_ShouldFailWithASkipMessageWhenEveryRelayIsOpen()
    {
        var timeProvider = new FakeTimeProvider();
        var relay = Build(
            timeProvider,
            TwoStrikes,
            new CountingRelay(alwaysFails: true),
            new CountingRelay(alwaysFails: true)
        );

        await Should.ThrowAsync<InvalidOperationException>(() => Call(relay));
        await Should.ThrowAsync<InvalidOperationException>(() => Call(relay));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Call(relay));
        exception.Message.ShouldContain("skipped by an open circuit");
        relay.GetCircuitSnapshots().ShouldAllBe(s => s.State == RelayCircuitState.Open);
    }

    [Fact]
    public async Task Circuit_ShouldBeDisabledByDefault()
    {
        var failing = new CountingRelay(alwaysFails: true);
        var healthy = new CountingRelay(alwaysFails: false);
        var relay = new MultiRelay<ITestService>(
            [failing, healthy],
            RelayStrategy.Failover
        );

        await Call(relay);
        await Call(relay);
        await Call(relay);

        failing.Calls.ShouldBe(3, "without a breaker every call still tries the failing relay");
        relay.GetCircuitSnapshots().ShouldAllBe(s => s.State == RelayCircuitState.Closed);
    }

    [Fact]
    public async Task Circuit_ShouldOnlyCountARelayAsFailedAfterItsRetriesAreExhausted()
    {
        var timeProvider = new FakeTimeProvider();
        var flaky = new CountingRelay(alwaysFails: true) { FailuresBeforeSuccess = 1 };
        var relay = new MultiRelay<ITestService>(
            [flaky],
            RelayStrategy.Failover,
            new RelayResilienceOptions { MaxAttempts = 2 },
            TwoStrikes,
            timeProvider
        );

        (await Call(relay)).ShouldBe("ok");

        flaky.Calls.ShouldBe(2, "the first attempt failed and the retry succeeded");
        relay.GetCircuitSnapshots()[0].State.ShouldBe(RelayCircuitState.Closed);
    }

    [Fact]
    public void Builder_ShouldRejectInvalidCircuitBreakerSettings()
    {
        var services = new ServiceCollection();
        var builder = new MultiRelayBuilder<ITestService>(services);

        Should.Throw<ArgumentOutOfRangeException>(() => builder.WithCircuitBreaker(0));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            builder.WithCircuitBreaker(3, TimeSpan.Zero)
        );
    }

    [Fact]
    public async Task Builder_ShouldConfigureTheCircuitBreaker()
    {
        var timeProvider = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services
            .AddMultiRelay<ITestService>(config =>
                config
                    .AddRelay<AlwaysFailingRelay>()
                    .AddRelay<TestServiceB>()
                    .WithStrategy(RelayStrategy.Failover)
                    .WithCircuitBreaker(failureThreshold: 1, breakDuration: TimeSpan.FromMinutes(1))
            )
            .Build();

        var provider = services.BuildServiceProvider();
        var multiRelay = (MultiRelay<ITestService>)
            provider.GetRequiredService<IMultiRelay<ITestService>>();

        await multiRelay.RelayToAllWithResults(s => s.ProcessAsync("payload"));
        await multiRelay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        multiRelay.GetCircuitSnapshots()[0].State.ShouldBe(RelayCircuitState.Open);
        AlwaysFailingRelay.Calls.ShouldBe(1);
    }

    private sealed class CountingRelay(bool alwaysFails) : ITestService
    {
        public bool AlwaysFails { get; set; } = alwaysFails;
        public int FailuresBeforeSuccess { get; init; } = int.MaxValue;
        public int Calls { get; private set; }

        public string Process(string input) => throw new NotSupportedException();

        public Task<string> ProcessAsync(string input)
        {
            Calls++;
            return AlwaysFails && Calls <= FailuresBeforeSuccess
                ? Task.FromException<string>(new InvalidOperationException("relay is down"))
                : Task.FromResult("ok");
        }
    }

    private sealed class AlwaysFailingRelay : ITestService
    {
        internal static int Calls;

        public string Process(string input) => throw new NotSupportedException();

        public Task<string> ProcessAsync(string input)
        {
            Calls++;
            return Task.FromException<string>(new InvalidOperationException("relay is down"));
        }
    }
}
