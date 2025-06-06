using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests;

public class ServiceCollectionExtensionsTests : TestBase
{
    [Fact]
    public void AddRelay_WithConfiguration_ShouldReturnServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddRelay(config => config.FromAssemblyOf<ITestService>());

        // Assert
        result.ShouldBe(services);
        services.Any(s => s.ServiceType == typeof(IRelayContext)).ShouldBeTrue();
        services.Any(s => s.ServiceType == typeof(IRelayResolver)).ShouldBeTrue();
    }

    [Fact]
    public void AddRelay_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddRelay(config => { }))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddRelay_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => services.AddRelay(null!))
            .ParamName.ShouldBe("configureRelay");
    }

    [Fact]
    public void AddRelay_WithImplementation_ShouldReturnBuilder()
    {
        // Act
        var builder = Services.AddRelay<ITestService, TestServiceA>();

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RelayRegistrationBuilder<ITestService>>();
    }

    [Fact]
    public void AddRelay_WithImplementation_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddRelay<ITestService, TestServiceA>()
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddConditionalRelay_ShouldReturnBuilder()
    {
        // Act
        var builder = Services.AddConditionalRelay<ITestService>();

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<ConditionalRelayBuilder<ITestService>>();
    }

    [Fact]
    public void AddConditionalRelay_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddConditionalRelay<ITestService>()
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddMultiRelay_ShouldReturnBuilder()
    {
        // Act
        var builder = Services.AddMultiRelay<ITestService>(config =>
            config.AddRelay<TestServiceA>()
        );

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<MultiRelayBuilder<ITestService>>();
    }

    [Fact]
    public void AddMultiRelay_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddMultiRelay<ITestService>(config => { })
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddMultiRelay_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => Services.AddMultiRelay<ITestService>(null!))
            .ParamName.ShouldBe("configure");
    }

    [Fact]
    public void AddRelayFactory_ShouldReturnBuilder()
    {
        // Act
        var builder = Services.AddRelayFactory<ITestService>(factory =>
            factory.RegisterRelay<TestServiceA>("testA")
        );

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RelayFactoryBuilder<ITestService>>();
    }

    [Fact]
    public void AddRelayFactory_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddRelayFactory<ITestService>(factory => { })
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddRelayFactory_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => Services.AddRelayFactory<ITestService>(null!))
            .ParamName.ShouldBe("configure");
    }

    [Fact]
    public void AddRelayServices_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddRelayServices();

        // Assert
        result.ShouldBe(services);
        services.Any(s => s.ServiceType == typeof(IRelayContext)).ShouldBeTrue();
        services.Any(s => s.ServiceType == typeof(IRelayResolver)).ShouldBeTrue();
    }

    [Fact]
    public void AddRelayServices_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddRelayServices())
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddRelayServices_ShouldRegisterWithCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelayServices();

        // Assert
        var contextRegistration = services.First(s => s.ServiceType == typeof(IRelayContext));
        var resolverRegistration = services.First(s => s.ServiceType == typeof(IRelayResolver));

        contextRegistration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        resolverRegistration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddRelayServices_ShouldNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRelayServices();
        services.AddRelayServices(); // Call twice

        // Assert
        services.Count(s => s.ServiceType == typeof(IRelayContext)).ShouldBe(2); // Will have duplicates, that's expected
        services.Count(s => s.ServiceType == typeof(IRelayResolver)).ShouldBe(2);
    }
}
