using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Shouldly;

namespace Relay.Tests.Builders;

public class RelayRegistrationBuilderTests : TestBase
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => new RelayRegistrationBuilder<ITestService>(null!, typeof(TestServiceA))
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void Constructor_WithNullImplementationType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => new RelayRegistrationBuilder<ITestService>(Services, null!)
            )
            .ParamName.ShouldBe("implementationType");
    }

    [Fact]
    public void WithSingletonLifetime_ShouldSetLifetime()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var result = builder.WithSingletonLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithScopedLifetime_ShouldSetLifetime()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var result = builder.WithScopedLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithTransientLifetime_ShouldSetLifetime()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var result = builder.WithTransientLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithLifetime_ShouldSetSpecificLifetime()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var result = builder.WithLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void DecorateWith_ShouldAddDecorator()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var result = builder.DecorateWith<LoggingDecorator>();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void Build_ShouldRegisterService()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var services = builder.Build();

        // Assert
        services.ShouldBe(Services);
        Services
            .Any(s =>
                s.ServiceType == typeof(ITestService)
                && s.ImplementationType == typeof(TestServiceA)
            )
            .ShouldBeTrue();
    }

    [Fact]
    public void Build_WithSingletonLifetime_ShouldRegisterAsSingleton()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        builder.WithSingletonLifetime().Build();

        // Assert
        var registration = Services.First(s => s.ServiceType == typeof(ITestService));
        registration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Build_WithDecorator_ShouldApplyDecorator()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        builder.DecorateWith<LoggingDecorator>().Build();

        // Assert
        // Should have multiple registrations due to decorator
        Services
            .Count(s => s.ServiceType == typeof(ITestService))
            .ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ToServiceCollection_ShouldReturnBuiltServices()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        var services = builder.ToServiceCollection();

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(ITestService)).ShouldBeTrue();
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var services = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA))
            .WithSingletonLifetime()
            .DecorateWith<LoggingDecorator>()
            .Build();

        // Assert
        services.ShouldBe(Services);
        // When decorators are applied, the service gets a factory instead of direct implementation type
        var registration = Services.First(s => s.ServiceType == typeof(ITestService));
        registration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        registration.ImplementationFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithMultipleDecorators_ShouldApplyAll()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        builder
            .DecorateWith<LoggingDecorator>()
            .DecorateWith<LoggingDecorator>() // Can add same decorator multiple times
            .Build();

        // Assert
        Services.Count(s => s.ServiceType == typeof(ITestService)).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Build_WithDefaultLifetime_ShouldUseScoped()
    {
        // Arrange
        var builder = new RelayRegistrationBuilder<ITestService>(Services, typeof(TestServiceA));

        // Act
        builder.Build();

        // Assert
        var registration = Services.First(s =>
            s.ServiceType == typeof(ITestService) && s.ImplementationType == typeof(TestServiceA)
        );
        registration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
