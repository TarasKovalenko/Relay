using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Shouldly;

namespace Relay.Tests.Core;

public class RelayFactoryTests : TestBase
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["testA"] = _ => new TestServiceA(),
            ["testB"] = _ => new TestServiceB(),
        };

        // Act
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "testA");

        // Assert
        factory.ShouldNotBeNull();
        factory.GetAvailableKeys().ShouldBe(new[] { "testA", "testB" });
    }

    [Fact]
    public void Constructor_WithNullFactories_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => new RelayFactory<ITestService>(null!, ServiceProvider, "default")
            )
            .ParamName.ShouldBe("factories");
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>();

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => new RelayFactory<ITestService>(factories, null!, "default")
            )
            .ParamName.ShouldBe("serviceProvider");
    }

    [Fact]
    public void CreateRelay_WithValidKey_ShouldReturnCorrectService()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["testA"] = _ => new TestServiceA(),
            ["testB"] = _ => new TestServiceB(),
        };
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "testA");

        // Act
        var serviceA = factory.CreateRelay("testA");
        var serviceB = factory.CreateRelay("testB");

        // Assert
        serviceA.ShouldBeOfType<TestServiceA>();
        serviceB.ShouldBeOfType<TestServiceB>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateRelay_WithEmptyKey_ShouldThrowArgumentException(string key)
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>();
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "default");

        // Act & Assert
        Should.Throw<ArgumentException>(() => factory.CreateRelay(key)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void CreateRelay_WithInvalidKey_ShouldThrowArgumentException()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["testA"] = _ => new TestServiceA(),
        };
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "testA");

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => factory.CreateRelay("invalidKey"));
        exception.ParamName.ShouldBe("key");
        exception.Message.ShouldContain("No relay registered for key 'invalidKey'");
    }

    [Fact]
    public void CreateRelay_WithContext_ShouldReturnDefaultRelay()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["testA"] = _ => new TestServiceA(),
        };
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "testA");
        var context = new DefaultRelayContext(ServiceProvider);

        // Act
        var service = factory.CreateRelay(context);

        // Assert
        service.ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void CreateRelay_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>();
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "default");

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => factory.CreateRelay((Relay.Core.Interfaces.IRelayContext)null!)
            )
            .ParamName.ShouldBe("context");
    }

    [Fact]
    public void GetDefaultRelay_WithValidDefaultKey_ShouldReturnDefaultService()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["testA"] = _ => new TestServiceA(),
            ["testB"] = _ => new TestServiceB(),
        };
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "testB");

        // Act
        var service = factory.GetDefaultRelay();

        // Assert
        service.ShouldBeOfType<TestServiceB>();
    }

    [Fact]
    public void GetDefaultRelay_WithNullDefaultKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>();
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, null);

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() => factory.GetDefaultRelay())
            .Message.ShouldContain("No default relay configured");
    }

    [Fact]
    public void GetDefaultRelay_WithEmptyDefaultKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>();
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, string.Empty);

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() => factory.GetDefaultRelay())
            .Message.ShouldContain("No default relay configured");
    }

    [Fact]
    public void GetAvailableKeys_ShouldReturnAllRegisteredKeys()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["key1"] = _ => new TestServiceA(),
            ["key2"] = _ => new TestServiceB(),
            ["key3"] = _ => new TestServiceC(),
        };
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "key1");

        // Act
        var keys = factory.GetAvailableKeys();

        // Assert
        keys.ShouldBe(new[] { "key1", "key2", "key3" });
    }

    [Fact]
    public void GetAvailableKeys_WithEmptyFactories_ShouldReturnEmptyCollection()
    {
        // Arrange
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>();
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, null);

        // Act
        var keys = factory.GetAvailableKeys();

        // Assert
        keys.ShouldBeEmpty();
    }

    [Fact]
    public void CreateRelay_ShouldUseServiceProvider()
    {
        // Arrange
        Services.AddSingleton<string>("test-dependency");
        var factories = new Dictionary<string, Func<IServiceProvider, ITestService>>
        {
            ["test"] = provider =>
            {
                var dependency = provider.GetRequiredService<string>();
                return dependency == "test-dependency" ? new TestServiceA() : new TestServiceB();
            },
        };
        var factory = new RelayFactory<ITestService>(factories, ServiceProvider, "test");

        // Act
        var service = factory.CreateRelay("test");

        // Assert
        service.ShouldBeOfType<TestServiceA>();
    }
}
