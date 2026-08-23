using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Core.Options;
using Shouldly;

namespace Relay.Tests.Core;

public class MultiRelayResilienceTests
{
    [Fact]
    public async Task Retry_ShouldUseTheInjectedTimeProviderForBackoff()
    {
        var timeProvider = new FakeTimeProvider();
        var flaky = new FlakyRelay(failuresBeforeSuccess: 2);
        var relay = new MultiRelay<ITestService>(
            [flaky],
            RelayStrategy.Failover,
            new RelayResilienceOptions
            {
                MaxAttempts = 3,
                Delay = TimeSpan.FromSeconds(5),
                BackoffFactor = 2,
            },
            timeProvider
        );

        var execution = relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        // Nothing completes until the fake clock advances past the backoff windows: the relay
        // never touches the wall clock.
        execution.IsCompleted.ShouldBeFalse();

        for (var i = 0; i < 10 && !execution.IsCompleted; i++)
        {
            await Task.Yield();
            timeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        (await execution).ShouldHaveSingleItem().ShouldBe("ServiceA: payload");
        flaky.Attempts.ShouldBe(3);
    }

    [Fact]
    public async Task Retry_ShouldMoveToTheNextRelayAfterExhaustingAttempts()
    {
        var failing = new FlakyRelay(failuresBeforeSuccess: int.MaxValue);
        var relay = new MultiRelay<ITestService>(
            [failing, new TestServiceB()],
            RelayStrategy.Failover,
            new RelayResilienceOptions { MaxAttempts = 2 },
            TimeProvider.System
        );

        var results = await relay.RelayToAllWithResults(s => s.ProcessAsync("payload"));

        results.ShouldHaveSingleItem().ShouldBe("ServiceB: payload");
        failing.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Failover_ShouldReportEveryRelayFailure()
    {
        var relay = new MultiRelay<ITestService>(
            [new FailingRelay("first"), new FailingRelay("second")],
            RelayStrategy.Failover
        );

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            relay.RelayToAllWithResults(s => s.ProcessAsync("payload"))
        );

        var aggregate = exception.InnerException.ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.Count.ShouldBe(2);
        aggregate.InnerExceptions.Select(e => e.Message).ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task Parallel_ShouldSurfaceEveryFailureNotJustTheFirst()
    {
        var relay = new MultiRelay<ITestService>(
            [new FailingRelay("first"), new FailingRelay("second")],
            RelayStrategy.Parallel
        );

        var exception = await Should.ThrowAsync<AggregateException>(() =>
            relay.RelayToAllWithResults(s => s.ProcessAsync("payload"))
        );

        exception.InnerExceptions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Broadcast_ShouldHonourCancellation()
    {
        var relay = new MultiRelay<ITestService>(
            [new TestServiceA(), new TestServiceB()],
            RelayStrategy.Broadcast
        );
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            relay.RelayToAll(s => s.ProcessAsync("payload"), cts.Token)
        );
    }

    [Fact]
    public void Builder_ShouldResolveTheRegisteredTimeProvider()
    {
        var timeProvider = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services
            .AddMultiRelay<ITestService>(builder =>
                builder
                    .AddRelay<TestServiceA>()
                    .WithStrategy(RelayStrategy.Failover)
                    .WithRetry(2, TimeSpan.FromSeconds(1))
            )
            .Build();

        var multiRelay = services
            .BuildServiceProvider()
            .GetRequiredService<IMultiRelay<ITestService>>();

        multiRelay.ShouldNotBeNull();
        multiRelay.GetRelays().ShouldHaveSingleItem();
    }

    private sealed class FlakyRelay(int failuresBeforeSuccess) : ITestService
    {
        public int Attempts { get; private set; }

        public string Process(string input) => throw new NotSupportedException();

        public Task<string> ProcessAsync(string input)
        {
            Attempts++;
            return Attempts <= failuresBeforeSuccess
                ? Task.FromException<string>(new InvalidOperationException("transient"))
                : Task.FromResult($"ServiceA: {input}");
        }
    }

    private sealed class FailingRelay(string message) : ITestService
    {
        public string Process(string input) => throw new InvalidOperationException(message);

        public Task<string> ProcessAsync(string input) =>
            Task.FromException<string>(new InvalidOperationException(message));
    }
}
