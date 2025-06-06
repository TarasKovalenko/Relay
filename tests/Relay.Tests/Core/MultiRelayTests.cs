using Relay.Core.Enums;
using Relay.Core.Implementations;
using Shouldly;

namespace Relay.Tests.Core;

public class MultiRelayTests
{
    [Fact]
    public void Constructor_WithValidRelaysAndStrategy_ShouldInitializeCorrectly()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };

        // Act
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Broadcast);

        // Assert
        multiRelay.GetRelays().ShouldBe(relays);
    }

    [Fact]
    public void Constructor_WithNullRelays_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => new MultiRelay<ITestService>(null!, RelayStrategy.Broadcast)
            )
            .ParamName.ShouldBe("relays");
    }

    [Fact]
    public void GetRelays_ShouldReturnAllRelays()
    {
        // Arrange
        var relays = new List<ITestService>
        {
            new TestServiceA(),
            new TestServiceB(),
            new TestServiceC(),
        };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Broadcast);

        // Act
        var result = multiRelay.GetRelays();

        // Assert
        result.ShouldBe(relays);
        result.Count().ShouldBe(3);
    }

    [Fact]
    public async Task RelayToAllWithResults_BroadcastStrategy_ShouldExecuteOnAllRelays()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Broadcast);

        // Act
        var results = await multiRelay.RelayToAllWithResults(async service =>
            await service.ProcessAsync("test")
        );

        // Assert
        results.ShouldNotBeNull();
        results.Count().ShouldBe(2);
        results.ShouldContain("ServiceA: test");
        results.ShouldContain("ServiceB: test");
    }

    [Fact]
    public async Task RelayToAllWithResults_ParallelStrategy_ShouldExecuteInParallel()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Parallel);

        // Act
        var results = await multiRelay.RelayToAllWithResults(async service =>
            await service.ProcessAsync("test")
        );

        // Assert
        results.ShouldNotBeNull();
        results.Count().ShouldBe(2);
        results.ShouldContain("ServiceA: test");
        results.ShouldContain("ServiceB: test");
    }

    [Fact]
    public async Task RelayToAllWithResults_FailoverStrategy_ShouldReturnFirstSuccessful()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Failover);

        // Act
        var results = await multiRelay.RelayToAllWithResults(async service =>
            await service.ProcessAsync("test")
        );

        // Assert
        results.ShouldNotBeNull();
        results.Count().ShouldBe(1);
        results.First().ShouldBe("ServiceA: test");
    }

    [Fact]
    public async Task RelayToAllWithResults_FirstSuccessfulStrategy_ShouldReturnFirstSuccessful()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.FirstSuccessful);

        // Act
        var results = await multiRelay.RelayToAllWithResults(async service =>
            await service.ProcessAsync("test")
        );

        // Assert
        results.ShouldNotBeNull();
        results.Count().ShouldBe(1);
        results.First().ShouldBe("ServiceA: test");
    }

    [Fact]
    public async Task RelayToAllWithResults_RoundRobinStrategy_ShouldUseNextRelay()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.RoundRobin);

        // Act
        var results = await multiRelay.RelayToAllWithResults(async service =>
            await service.ProcessAsync("test")
        );

        // Assert
        results.ShouldNotBeNull();
        results.Count().ShouldBe(1);
        results.First().ShouldBe("ServiceA: test");
    }

    [Fact]
    public async Task RelayToAllWithResults_WithNullOperation_ShouldThrowArgumentNullException()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Broadcast);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await multiRelay.RelayToAllWithResults<string>(null!)
        );
    }

    [Fact]
    public async Task RelayToAll_BroadcastStrategy_ShouldExecuteOnAllRelays()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Broadcast);
        var executedServices = new List<string>();

        // Act
        await multiRelay.RelayToAll(async service =>
        {
            var result = await service.ProcessAsync("test");
            executedServices.Add(result);
        });

        // Assert
        executedServices.Count.ShouldBe(2);
        executedServices.ShouldContain("ServiceA: test");
        executedServices.ShouldContain("ServiceB: test");
    }

    [Fact]
    public async Task RelayToAll_WithNullOperation_ShouldThrowArgumentNullException()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Broadcast);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await multiRelay.RelayToAll(null!)
        );
    }

    [Fact]
    public async Task GetNextRelay_WithAvailableRelays_ShouldReturnNext()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA(), new TestServiceB() };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.RoundRobin);

        // Act
        var first = await multiRelay.GetNextRelay();
        var second = await multiRelay.GetNextRelay();
        var third = await multiRelay.GetNextRelay(); // Should wrap around

        // Assert
        first.ShouldBeOfType<TestServiceA>();
        second.ShouldBeOfType<TestServiceB>();
        third.ShouldBeOfType<TestServiceA>(); // Round robin wraps around
    }

    [Fact]
    public async Task GetNextRelay_WithNoRelays_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var relays = new List<ITestService>();
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.RoundRobin);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await multiRelay.GetNextRelay()
        );
    }

    [Fact]
    public async Task RelayToAllWithResults_UnsupportedStrategy_ShouldThrowNotSupportedException()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA() };
        var multiRelay = new MultiRelay<ITestService>(relays, (RelayStrategy)999);

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(
            async () =>
                await multiRelay.RelayToAllWithResults(async service =>
                    await service.ProcessAsync("test")
                )
        );
    }

    [Fact]
    public async Task RelayToAll_UnsupportedStrategy_ShouldThrowNotSupportedException()
    {
        // Arrange
        var relays = new List<ITestService> { new TestServiceA() };
        var multiRelay = new MultiRelay<ITestService>(relays, (RelayStrategy)999);

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(
            async () =>
                await multiRelay.RelayToAll(async service => await service.ProcessAsync("test"))
        );
    }

    [Fact]
    public async Task RelayToAllWithResults_FailoverWithFailures_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var failingService = new FailingTestService();
        var relays = new List<ITestService> { failingService };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Failover);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () =>
                await multiRelay.RelayToAllWithResults(async service =>
                    await service.ProcessAsync("test")
                )
        );
    }

    [Fact]
    public async Task RelayToAll_FailoverWithFailures_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var failingService = new FailingTestService();
        var relays = new List<ITestService> { failingService };
        var multiRelay = new MultiRelay<ITestService>(relays, RelayStrategy.Failover);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () =>
                await multiRelay.RelayToAll(async service => await service.ProcessAsync("test"))
        );
    }

    private class FailingTestService : ITestService
    {
        public string Process(string input) =>
            throw new InvalidOperationException("Service failed");

        public Task<string> ProcessAsync(string input) =>
            throw new InvalidOperationException("Service failed");
    }
}
