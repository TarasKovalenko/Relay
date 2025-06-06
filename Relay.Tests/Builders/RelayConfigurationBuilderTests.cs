using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Shouldly;

namespace Relay.Tests.Builders;

public class RelayConfigurationBuilderTests : TestBase
{
    [Fact]
    public void Constructor_WithValidServiceCollection_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new RelayConfigurationBuilder(Services);

        // Assert
        builder.ShouldNotBeNull();
        // Should have added relay services
        Services
            .Any(s => s.ServiceType == typeof(Relay.Core.Interfaces.IRelayContext))
            .ShouldBeTrue();
        Services
            .Any(s => s.ServiceType == typeof(Relay.Core.Interfaces.IRelayResolver))
            .ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullServiceCollection_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new RelayConfigurationBuilder(null!))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void FromAssemblyOf_ShouldAddAssembly()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);

        // Act
        var result = builder.FromAssemblyOf<ITestService>();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void FromAssemblies_WithValidAssemblies_ShouldAddAssemblies()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var result = builder.FromAssemblies(assembly);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void FromAssemblies_WithNullAssemblies_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => builder.FromAssemblies(null!))
            .ParamName.ShouldBe("assemblies");
    }

    [Fact]
    public void WithDefaultLifetime_ShouldSetDefaultLifetime()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);

        // Act
        var result = builder.WithDefaultLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void AddRelays_WithValidConfiguration_ShouldReturnSelector()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);

        // Act
        var selector = builder.AddRelays(s => s.ForInterface<ITestService>());

        // Assert
        selector.ShouldNotBeNull();
        selector.ShouldBeOfType<RelayTypeSelector>();
    }

    [Fact]
    public void AddRelays_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => builder.AddRelays(null!))
            .ParamName.ShouldBe("configure");
    }

    [Fact]
    public void RegisterRelays_ShouldRegisterRelaysFromAssemblies()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);
        builder.FromAssemblyOf<ITestService>();

        // Act
        var result = builder.RegisterRelays();

        // Assert
        result.ShouldBe(builder);
        // Should have registered some services (test classes ending with TestService)
        Services.Count(s => s.ServiceType == typeof(ITestService)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void RegisterRelays_WithMultipleAssemblies_ShouldRegisterFromAll()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);
        builder.FromAssemblies(Assembly.GetExecutingAssembly(), typeof(string).Assembly);

        // Act
        var result = builder.RegisterRelays();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void RegisterRelays_ShouldSkipFrameworkInterfaces()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);
        builder.FromAssemblyOf<ITestService>();

        // Act
        builder.RegisterRelays();

        // Assert
        // Should not register framework interfaces like IDisposable
        Services.Any(s => s.ServiceType == typeof(IDisposable)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterRelays_ShouldRegisterOnlyConcreteClasses()
    {
        // Arrange
        var builder = new RelayConfigurationBuilder(Services);
        builder.FromAssemblyOf<ITestService>();

        // Act
        builder.RegisterRelays();

        // Assert
        // All registered implementation types should be concrete classes
        var implementationTypes = Services
            .Where(s => s.ImplementationType != null)
            .Select(s => s.ImplementationType!)
            .ToList();

        implementationTypes.ShouldAllBe(t => t.IsClass && !t.IsAbstract);
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var result = new RelayConfigurationBuilder(Services)
            .FromAssemblyOf<ITestService>()
            .WithDefaultLifetime(ServiceLifetime.Singleton)
            .RegisterRelays();

        // Assert
        result.ShouldBeOfType<RelayConfigurationBuilder>();
    }
}
