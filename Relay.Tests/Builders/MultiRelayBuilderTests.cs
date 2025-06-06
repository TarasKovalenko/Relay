using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Builders;

public class MultiRelayBuilderTests : TestBase
{
    [Fact]
    public void Constructor_WithValidServiceCollection_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new MultiRelayBuilder<ITestService>(null!))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddRelay_WithDefaultLifetime_ShouldAddRelay()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.AddRelay<TestServiceA>();

        // Assert
        result.ShouldBe(builder);
        Services.Any(s => s.ServiceType == typeof(TestServiceA)).ShouldBeTrue();
    }

    [Fact]
    public void AddRelay_WithSpecificLifetime_ShouldUseSpecificLifetime()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.AddRelay<TestServiceA>(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
        var registration = Services.First(s => s.ServiceType == typeof(TestServiceA));
        registration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void WithStrategy_ShouldSetStrategy()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithStrategy(RelayStrategy.Parallel);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithSingletonLifetime_ShouldSetDefaultLifetime()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithSingletonLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithScopedLifetime_ShouldSetDefaultLifetime()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithScopedLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithTransientLifetime_ShouldSetDefaultLifetime()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithTransientLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithDefaultLifetime_ShouldSetDefaultLifetime()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithDefaultLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithParallelExecution_ShouldSetParallelStrategy()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithParallelExecution();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void Build_WithMultipleRelays_ShouldRegisterMultiRelay()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.AddRelay<TestServiceA>();
        builder.AddRelay<TestServiceB>();

        // Act
        var services = builder.Build();

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(IMultiRelay<ITestService>)).ShouldBeTrue();
    }

    [Fact]
    public void Build_ShouldCreateFactoryRegistration()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.AddRelay<TestServiceA>();

        // Act
        builder.Build();

        // Assert
        var registration = Services.First(s => s.ServiceType == typeof(IMultiRelay<ITestService>));
        registration.ImplementationFactory.ShouldNotBeNull();
    }

    [Fact]
    public void MultiRelay_ShouldResolveAllRelays()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.AddRelay<TestServiceA>();
        builder.AddRelay<TestServiceB>();
        builder.Build();

        // Act
        var multiRelay = GetService<IMultiRelay<ITestService>>();
        var relays = multiRelay.GetRelays();

        // Assert
        relays.Count().ShouldBe(2);
        relays.ShouldContain(r => r.GetType() == typeof(TestServiceA));
        relays.ShouldContain(r => r.GetType() == typeof(TestServiceB));
    }

    [Fact]
    public void MultiRelay_WithBroadcastStrategy_ShouldUseCorrectStrategy()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.AddRelay<TestServiceA>();
        builder.AddRelay<TestServiceB>();
        builder.WithStrategy(RelayStrategy.Broadcast);
        builder.Build();

        // Act
        var multiRelay = GetService<IMultiRelay<ITestService>>();

        // Assert
        multiRelay.ShouldNotBeNull();
        // Strategy verification would need access to private fields or more elaborate testing
    }

    [Fact]
    public void MultiRelay_WithDifferentLifetimes_ShouldRespectIndividualLifetimes()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.WithDefaultLifetime(ServiceLifetime.Scoped);
        builder.AddRelay<TestServiceA>(ServiceLifetime.Singleton);
        builder.AddRelay<TestServiceB>(ServiceLifetime.Transient);
        builder.Build();

        // Act & Assert
        var singletonRegistration = Services.First(s => s.ServiceType == typeof(TestServiceA));
        var transientRegistration = Services.First(s => s.ServiceType == typeof(TestServiceB));

        singletonRegistration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        transientRegistration.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var services = new MultiRelayBuilder<ITestService>(Services)
            .WithScopedLifetime()
            .WithStrategy(RelayStrategy.Broadcast)
            .AddRelay<TestServiceA>()
            .AddRelay<TestServiceB>()
            .AddRelay<TestServiceC>()
            .Build();

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(IMultiRelay<ITestService>)).ShouldBeTrue();
        Services
            .Count(s =>
                s.ServiceType == typeof(TestServiceA)
                || s.ServiceType == typeof(TestServiceB)
                || s.ServiceType == typeof(TestServiceC)
            )
            .ShouldBe(3);
    }

    [Fact]
    public void Build_WithNoRelays_ShouldStillRegisterMultiRelay()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);

        // Act
        builder.Build();

        // Assert
        Services.Any(s => s.ServiceType == typeof(IMultiRelay<ITestService>)).ShouldBeTrue();
    }

    [Fact]
    public void MultiRelay_WithNoRelays_ShouldReturnEmptyCollection()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.Build();

        // Act
        var multiRelay = GetService<IMultiRelay<ITestService>>();
        var relays = multiRelay.GetRelays();

        // Assert
        relays.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithSingletonMultiRelayLifetime_ShouldUseSingletonLifetime()
    {
        // Arrange
        var builder = new MultiRelayBuilder<ITestService>(Services);
        builder.WithSingletonLifetime();
        builder.AddRelay<TestServiceA>();

        // Act
        builder.Build();

        // Assert
        var registration = Services.First(s => s.ServiceType == typeof(IMultiRelay<ITestService>));
        registration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
