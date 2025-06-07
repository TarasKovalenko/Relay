using Microsoft.Extensions.DependencyInjection;
using Relay.Decorators;
using Shouldly;

namespace Relay.Tests.Decorators;

public class DecoratorExtensionsTests : TestBase
{
    [Fact]
    public void Decorate_WithValidType_ShouldDecorateService()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();

        // Act
        var result = Services.Decorate<ITestService>(typeof(LoggingDecorator));

        // Assert
        result.ShouldBe(Services);
        var registration = Services.Last(s => s.ServiceType == typeof(ITestService));
        registration.ImplementationFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Decorate_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).Decorate<ITestService>(typeof(LoggingDecorator))
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void Decorate_WithNullDecoratorType_ShouldThrowArgumentNullException()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(() => Services.Decorate<ITestService>((Type)null!))
            .ParamName.ShouldBe("decoratorType");
    }

    [Fact]
    public void Decorate_WithUnregisteredService_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        Should
            .Throw<InvalidOperationException>(
                () => Services.Decorate<ITestService>(typeof(LoggingDecorator))
            )
            .Message.ShouldContain("not registered");
    }

    [Fact]
    public void Decorate_WithFactory_ShouldDecorateService()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();

        // Act
        var result = Services.Decorate<ITestService>(
            (service, provider) => new LoggingDecorator(service)
        );

        // Assert
        result.ShouldBe(Services);
        var registration = Services.Last(s => s.ServiceType == typeof(ITestService));
        registration.ImplementationFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Decorate_WithNullFactoryFunction_ShouldThrowArgumentNullException()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () =>
                    Services.Decorate<ITestService>(
                        (Func<ITestService, IServiceProvider, ITestService>)null!
                    )
            )
            .ParamName.ShouldBe("decorator");
    }

    [Fact]
    public void Decorate_ShouldMaintainOriginalLifetime()
    {
        // Arrange
        Services.AddSingleton<ITestService, TestServiceA>();
        var originalLifetime = Services.First(s => s.ServiceType == typeof(ITestService)).Lifetime;

        // Act
        Services.Decorate<ITestService>(typeof(LoggingDecorator));

        // Assert
        var decoratedRegistration = Services.Last(s => s.ServiceType == typeof(ITestService));
        decoratedRegistration.Lifetime.ShouldBe(originalLifetime);
    }

    [Fact]
    public void Decorate_ShouldReplaceOriginalRegistration()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        var originalCount = Services.Count(s => s.ServiceType == typeof(ITestService));

        // Act
        Services.Decorate<ITestService>(typeof(LoggingDecorator));

        // Assert
        var newCount = Services.Count(s => s.ServiceType == typeof(ITestService));
        newCount.ShouldBe(originalCount); // Should replace, not add
    }

    [Fact]
    public void DecoratedService_ShouldResolveCorrectly()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        Services.Decorate<ITestService>(typeof(LoggingDecorator));

        // Act
        var service = GetService<ITestService>();

        // Assert
        service.ShouldBeOfType<LoggingDecorator>();
    }

    [Fact]
    public async Task DecoratedService_ShouldWrapOriginalBehavior()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        Services.Decorate<ITestService>(typeof(LoggingDecorator));

        // Act
        var service = GetService<ITestService>();
        var result = await service.ProcessAsync("test");

        // Assert
        result.ShouldBe("[Logged] ServiceA: test");
    }

    [Fact]
    public void Decorate_WithFactoryFunction_ShouldResolveCorrectly()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        Services.Decorate<ITestService>((service, provider) => new LoggingDecorator(service));

        // Act
        var decoratedService = GetService<ITestService>();

        // Assert
        decoratedService.ShouldBeOfType<LoggingDecorator>();
    }

    [Fact]
    public void Decorate_MultipleDecorators_ShouldNestCorrectly()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        Services.Decorate<ITestService>(typeof(LoggingDecorator));
        Services.Decorate<ITestService>((service, provider) => new LoggingDecorator(service)); // Double wrapping

        // Act
        var service = GetService<ITestService>();

        // Assert
        service.ShouldBeOfType<LoggingDecorator>();
        // The inner service should also be a LoggingDecorator
    }

    [Fact]
    public async Task Decorate_MultipleDecorators_ShouldChainBehavior()
    {
        // Arrange
        Services.AddScoped<ITestService, TestServiceA>();
        Services.Decorate<ITestService>(typeof(LoggingDecorator));
        Services.Decorate<ITestService>((service, provider) => new LoggingDecorator(service));

        // Act
        var service = GetService<ITestService>();
        var result = await service.ProcessAsync("test");

        // Assert
        result.ShouldBe("[Logged] [Logged] ServiceA: test");
    }

    [Fact]
    public void Decorate_WithComplexService_ShouldWork()
    {
        // Arrange
        Services.AddScoped<LegacyDataService>();
        Services.AddScoped<ITestRepository, LegacyDataAdapter>();

        // Act
        Services.Decorate<ITestRepository>(
            (repo, provider) =>
            {
                // Create a decorator that adds caching behavior
                return new CachingRepositoryDecorator(repo);
            }
        );

        // Assert
        var repository = GetService<ITestRepository>();
        repository.ShouldBeOfType<CachingRepositoryDecorator>();
    }

    private class CachingRepositoryDecorator(ITestRepository inner) : ITestRepository
    {
        public async Task<string> GetDataAsync(int id)
        {
            var result = await inner.GetDataAsync(id);
            return $"[Cached] {result}";
        }
    }
}
