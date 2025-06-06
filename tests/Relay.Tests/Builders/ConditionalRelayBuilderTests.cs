using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Builders;

public class ConditionalRelayBuilderTests : TestBase
{
    [Fact]
    public void Constructor_WithValidServiceCollection_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new ConditionalRelayBuilder<ITestService>(null!))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void WithSingletonLifetime_ShouldSetLifetime()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithSingletonLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithScopedLifetime_ShouldSetLifetime()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithScopedLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithTransientLifetime_ShouldSetLifetime()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithTransientLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithLifetime_ShouldSetSpecificLifetime()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.WithLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void When_WithValidCondition_ShouldReturnStep()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        var step = builder.When(ctx => ctx.Environment == "Development");

        // Assert
        step.ShouldNotBeNull();
        step.ShouldBeOfType<ConditionalRelayStep<ITestService>>();
    }

    [Fact]
    public void When_WithNullCondition_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => builder.When(null!))
            .ParamName.ShouldBe("condition");
    }

    [Fact]
    public void Otherwise_ShouldRegisterDefaultCondition()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        var result = builder.Otherwise<TestServiceA>();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void Build_WithValidConditions_ShouldRegisterService()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);

        // Act
        builder
            .When(ctx => ctx.Environment == "Development")
            .RelayTo<TestServiceA>()
            .When(ctx => ctx.Environment == "Production")
            .RelayTo<TestServiceB>()
            .Build();

        // Assert
        Services.Any(s => s.ServiceType == typeof(ITestService)).ShouldBeTrue();
    }

    [Fact]
    public void Build_ShouldCreateFactoryRegistration()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder.When(ctx => true).RelayTo<TestServiceA>();

        // Act
        builder.Build();

        // Assert
        var registration = Services.First(s => s.ServiceType == typeof(ITestService));
        registration.ImplementationFactory.ShouldNotBeNull();
    }

    [Fact]
    public void ConditionalRelay_ShouldResolveCorrectImplementation()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder
            .When(ctx => ctx.Environment == "Development")
            .RelayTo<TestServiceA>()
            .When(ctx => ctx.Environment == "Production")
            .RelayTo<TestServiceB>()
            .Build();

        // Set up context for Development
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            // Act
            var service = GetService<ITestService>();

            // Assert
            service.ShouldBeOfType<TestServiceA>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void ConditionalRelay_WithProductionEnvironment_ShouldResolveProductionService()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder
            .When(ctx => ctx.Environment == "Development")
            .RelayTo<TestServiceA>()
            .When(ctx => ctx.Environment == "Production")
            .RelayTo<TestServiceB>()
            .Build();

        // Set up context for Production
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        try
        {
            // Act
            var service = GetService<ITestService>();

            // Assert
            service.ShouldBeOfType<TestServiceB>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void ConditionalRelay_WithNoMatchingCondition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder.When(ctx => ctx.Environment == "NonExistentEnvironment").RelayTo<TestServiceA>();
        builder.Build();

        // Act & Assert
        Should
            .Throw<InvalidOperationException>(() => GetService<ITestService>())
            .Message.ShouldContain("No suitable relay found");
    }

    [Fact]
    public void ConditionalRelay_WithTypeSelector_ShouldWork()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder.When(ctx => ctx.Environment == "Development").RelayTo(ctx => typeof(TestServiceA));
        builder.Build();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            // Act
            var service = GetService<ITestService>();

            // Assert
            service.ShouldBeOfType<TestServiceA>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void ConditionalRelay_WithCustomProperties_ShouldWork()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder
            .When(ctx =>
                ctx.Properties.ContainsKey("UseServiceA") && (bool)ctx.Properties["UseServiceA"]
            )
            .RelayTo<TestServiceA>();
        builder
            .When(ctx =>
                !ctx.Properties.ContainsKey("UseServiceA") || !(bool)ctx.Properties["UseServiceA"]
            )
            .RelayTo<TestServiceB>();
        builder.Build();

        // Register custom context
        Services.AddScoped<IRelayContext>(provider =>
        {
            var context = new DefaultRelayContext(provider);
            context.Properties["UseServiceA"] = true;
            return context;
        });

        // Act
        var service = GetService<ITestService>();

        // Assert
        service.ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var services = new ConditionalRelayBuilder<ITestService>(Services)
            .WithScopedLifetime()
            .When(ctx => ctx.Environment == "Development")
            .RelayTo<TestServiceA>()
            .When(ctx => ctx.Environment == "Production")
            .RelayTo<TestServiceB>()
            .Otherwise<TestServiceC>()
            .Build();

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(ITestService)).ShouldBeTrue();
    }

    [Fact]
    public void ConditionalRelayStep_RelayTo_WithNullTypeSelector_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        var step = builder.When(ctx => true);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => step.RelayTo((Func<IRelayContext, Type>)null!))
            .ParamName.ShouldBe("typeSelector");
    }

    [Fact]
    public void ConditionalRelay_WithSingletonLifetime_ShouldUseSingletonLifetime()
    {
        // Arrange
        var builder = new ConditionalRelayBuilder<ITestService>(Services);
        builder.WithSingletonLifetime().When(ctx => true).RelayTo<TestServiceA>().Build();

        // Act
        var registration = Services.First(s => s.ServiceType == typeof(ITestService));

        // Assert
        registration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
