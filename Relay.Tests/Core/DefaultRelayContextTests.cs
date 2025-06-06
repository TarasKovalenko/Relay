using Relay.Core.Implementations;
using Shouldly;

namespace Relay.Tests.Core;

public class DefaultRelayContextTests : TestBase
{
    [Fact]
    public void Constructor_WithValidServiceProvider_ShouldInitializeCorrectly()
    {
        // Act
        var context = new DefaultRelayContext(ServiceProvider);

        // Assert
        context.ServiceProvider.ShouldBeEquivalentTo(ServiceProvider);
        context.Environment.ShouldNotBeNull();
        context.Properties.ShouldNotBeNull();
        context.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new DefaultRelayContext(null!))
            .ParamName.ShouldBe("serviceProvider");
    }

    [Fact]
    public void Environment_ShouldDefaultToDevelopment_WhenEnvironmentVariableNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

        // Act
        var context = new DefaultRelayContext(ServiceProvider);

        // Assert
        context.Environment.ShouldBe("Development");
    }

    [Fact]
    public void Environment_ShouldUseEnvironmentVariable_WhenSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        try
        {
            // Act
            var context = new DefaultRelayContext(ServiceProvider);

            // Assert
            context.Environment.ShouldBe("Production");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void Properties_ShouldBeModifiable()
    {
        // Arrange
        var context = new DefaultRelayContext(ServiceProvider);

        // Act
        context.Properties["TestKey"] = "TestValue";

        // Assert
        context.Properties["TestKey"].ShouldBe("TestValue");
    }

    [Fact]
    public void Environment_ShouldBeModifiable()
    {
        // Arrange
        var context = new DefaultRelayContext(ServiceProvider);

        // Act
        context.Environment = "Testing";

        // Assert
        context.Environment.ShouldBe("Testing");
    }
}
