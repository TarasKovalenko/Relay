using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Shouldly;

namespace Relay.Tests.Core;

public class RelayResolverTests : TestBase
{
    [Fact]
    public void Constructor_WithValidServiceProvider_ShouldInitializeCorrectly()
    {
        // Act
        var resolver = new RelayResolver(ServiceProvider);

        // Assert
        resolver.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new RelayResolver(null!))
            .ParamName.ShouldBe("serviceProvider");
    }

    [Fact]
    public void Resolve_WithRegisteredService_ShouldReturnService()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        var resolver = new RelayResolver(ServiceProvider);

        // Act
        var service = resolver.Resolve<ITestService>();

        // Assert
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void Resolve_WithUnregisteredService_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var resolver = new RelayResolver(ServiceProvider);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => resolver.Resolve<ITestService>());
    }

    [Fact]
    public void Resolve_WithNullContext_ShouldCreateDefaultContext()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        var resolver = new RelayResolver(ServiceProvider);

        // Act
        var service = resolver.Resolve<ITestService>(null);

        // Assert
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void Resolve_WithProvidedContext_ShouldUseContext()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        var resolver = new RelayResolver(ServiceProvider);
        var context = new DefaultRelayContext(ServiceProvider);

        // Act
        var service = resolver.Resolve<ITestService>(context);

        // Assert
        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceA>();
    }
}
