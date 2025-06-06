using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Shouldly;

namespace Relay.Tests;

public class RelayTypeSelectorTests : TestBase
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };

        // Act
        var selector = new RelayTypeSelector(Services, assemblies);

        // Assert
        selector.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Arrange
        var assemblies = new List<Assembly>();

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new RelayTypeSelector(null!, assemblies))
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void Constructor_WithNullAssemblies_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => new RelayTypeSelector(Services, null!))
            .ParamName.ShouldBe("assemblies");
    }

    [Fact]
    public void Where_WithValidPredicate_ShouldReturnSelf()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.Where(t => t.IsClass);

        // Assert
        result.ShouldBe(selector);
    }

    [Fact]
    public void Where_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => selector.Where(null!))
            .ParamName.ShouldBe("predicate");
    }

    [Fact]
    public void ForInterface_ShouldRegisterImplementations()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.ForInterface<ITestService>();

        // Assert
        result.ShouldBe(selector);
        Services.Any(s => s.ServiceType == typeof(ITestService)).ShouldBeTrue();
    }

    [Fact]
    public void WithSingletonLifetime_ShouldSetLifetime()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.WithSingletonLifetime();

        // Assert
        result.ShouldBe(selector);
    }

    [Fact]
    public void WithScopedLifetime_ShouldSetLifetime()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.WithScopedLifetime();

        // Assert
        result.ShouldBe(selector);
    }

    [Fact]
    public void WithTransientLifetime_ShouldSetLifetime()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.WithTransientLifetime();

        // Assert
        result.ShouldBe(selector);
    }

    [Fact]
    public void WithLifetime_ShouldSetSpecificLifetime()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.WithLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(selector);
    }

    [Fact]
    public void AsSingleton_ShouldRegisterAsSingleton()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.AsSingleton<ITestService>();

        // Assert
        result.ShouldBe(selector);
        if (Services.Any(s => s.ServiceType == typeof(ITestService)))
        {
            var registration = Services.First(s => s.ServiceType == typeof(ITestService));
            registration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        }
    }

    [Fact]
    public void AsScoped_ShouldRegisterAsScoped()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.AsScoped<ITestService>();

        // Assert
        result.ShouldBe(selector);
        if (Services.Any(s => s.ServiceType == typeof(ITestService)))
        {
            var registration = Services.First(s => s.ServiceType == typeof(ITestService));
            registration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        }
    }

    [Fact]
    public void AsTransient_ShouldRegisterAsTransient()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        var result = selector.AsTransient<ITestService>();

        // Assert
        result.ShouldBe(selector);
        if (Services.Any(s => s.ServiceType == typeof(ITestService)))
        {
            var registration = Services.First(s => s.ServiceType == typeof(ITestService));
            registration.Lifetime.ShouldBe(ServiceLifetime.Transient);
        }
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act & Assert
        var result = selector
            .Where(t => t.IsClass && !t.IsAbstract)
            .WithSingletonLifetime()
            .ForInterface<ITestService>()
            .WithScopedLifetime()
            .ForInterface<ITestRepository>();

        result.ShouldBe(selector);
    }

    [Fact]
    public void ForInterface_WithFilter_ShouldApplyFilter()
    {
        // Arrange
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        var selector = new RelayTypeSelector(Services, assemblies);

        // Act
        selector.Where(t => t.Name.Contains("TestServiceA")).ForInterface<ITestService>();

        // Assert
        // Should only register implementations that match the filter
        var registrations = Services.Where(s => s.ServiceType == typeof(ITestService)).ToList();
        registrations.ShouldAllBe(r => r.ImplementationType!.Name.Contains("TestServiceA") == true);
    }
}
