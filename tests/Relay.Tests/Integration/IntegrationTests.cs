using Microsoft.Extensions.DependencyInjection;
using Relay.Adapters;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Integration;

public class IntegrationTests : TestBase
{
    [Fact]
    public async Task CompleteRelayWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange - Configure all relay types
        Services.AddRelay<ITestService, TestServiceA>().WithScopedLifetime().Build();

        Services
            .AddConditionalRelay<ITestRepository>()
            .When(ctx => ctx.Environment == "Development")
            .RelayTo<TestRepositoryImplementation>()
            .When(ctx => ctx.Environment == "Production")
            .RelayTo<TestRepositoryImplementation>()
            .Build();

        Services
            .AddMultiRelay<ITestNotification>(config =>
                config
                    .AddRelay<EmailNotification>()
                    .AddRelay<SmsNotification>()
                    .AddRelay<PushNotification>()
                    .WithStrategy(RelayStrategy.Broadcast)
            )
            .Build();

        Services
            .AddRelayFactory<ITestService>(factory =>
                factory
                    .RegisterRelay<TestServiceA>("serviceA")
                    .RegisterRelay<TestServiceB>("serviceB")
                    .RegisterRelay<TestServiceC>("serviceC")
                    .SetDefaultRelay("serviceA")
            )
            .Build();

        Services.AddAdapter<ITestRepository, LegacyDataService>().Using<LegacyDataAdapter>();

        // Act - Test each relay type
        var basicService = GetService<ITestService>();
        var repository = GetService<ITestRepository>();
        var multiNotifications = GetService<IMultiRelay<ITestNotification>>();
        var factory = GetService<IRelayFactory<ITestService>>();

        var basicResult = await basicService.ProcessAsync("test");
        var repoResult = await repository.GetDataAsync(123);
        var notifications = multiNotifications.GetRelays();
        var factoryService = factory.CreateRelay("serviceB");
        var factoryResult = await factoryService.ProcessAsync("factory");

        // Assert
        basicResult.ShouldBe("ServiceA: test");
        repoResult.ShouldNotBeNull();
        notifications.Count().ShouldBe(3);
        factoryResult.ShouldBe("ServiceB: factory");
    }

    [Fact]
    public async Task MultiRelayBroadcast_ShouldExecuteAllServices()
    {
        // Arrange
        Services
            .AddMultiRelay<ITestNotification>(config =>
                config
                    .AddRelay<EmailNotification>()
                    .AddRelay<SmsNotification>()
                    .WithStrategy(RelayStrategy.Broadcast)
            )
            .Build();

        var multiRelay = GetService<IMultiRelay<ITestNotification>>();
        var executionCount = 0;

        // Act
        await multiRelay.RelayToAll(async notification =>
        {
            await notification.SendAsync("test message");
            Interlocked.Increment(ref executionCount);
        });

        // Assert
        executionCount.ShouldBe(2); // Should execute on both services
    }

    [Fact]
    public async Task MultiRelayParallel_ShouldExecuteInParallel()
    {
        // Arrange
        Services
            .AddMultiRelay<ITestNotification>(config =>
                config
                    .AddRelay<EmailNotification>()
                    .AddRelay<SmsNotification>()
                    .AddRelay<PushNotification>()
                    .WithStrategy(RelayStrategy.Parallel)
            )
            .Build();

        var multiRelay = GetService<IMultiRelay<ITestNotification>>();
        var startTime = DateTime.UtcNow;

        // Act
        var results = await multiRelay.RelayToAllWithResults(async notification =>
        {
            await notification.SendAsync("parallel test");
            return notification.GetType().Name;
        });

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        results.Count().ShouldBe(3);
        results.ShouldContain("EmailNotification");
        results.ShouldContain("SmsNotification");
        results.ShouldContain("PushNotification");
        // Should complete faster than sequential execution
        duration.ShouldBeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task ConditionalRelay_WithEnvironmentRouting_ShouldRouteCorrectly()
    {
        // Arrange
        Services
            .AddConditionalRelay<ITestService>()
            .When(ctx => ctx.Environment == "Development")
            .RelayTo<TestServiceA>()
            .When(ctx => ctx.Environment == "Staging")
            .RelayTo<TestServiceB>()
            .When(ctx => ctx.Environment == "Production")
            .RelayTo<TestServiceC>()
            .Build();

        // Test Development
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        try
        {
            using var devProvider = Services.BuildServiceProvider();
            var devService = devProvider.GetRequiredService<ITestService>();
            var devResult = await devService.ProcessAsync("dev-test");
            devResult.ShouldBe("ServiceA: dev-test");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }

        // Test Staging
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Staging");
        try
        {
            using var stagingProvider = Services.BuildServiceProvider();
            var stagingService = stagingProvider.GetRequiredService<ITestService>();
            var stagingResult = await stagingService.ProcessAsync("staging-test");
            stagingResult.ShouldBe("ServiceB: staging-test");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void RelayFactory_WithMultipleRegistrations_ShouldCreateCorrectServices()
    {
        // Arrange
        Services
            .AddRelayFactory<ITestService>(factory =>
                factory
                    .RegisterRelay<TestServiceA>("alpha")
                    .RegisterRelay<TestServiceB>("beta")
                    .RegisterRelay<TestServiceC>("gamma")
                    .SetDefaultRelay("beta")
            )
            .Build();

        var factory = GetService<IRelayFactory<ITestService>>();

        // Act
        var serviceAlpha = factory.CreateRelay("alpha");
        var serviceBeta = factory.CreateRelay("beta");
        var serviceGamma = factory.CreateRelay("gamma");
        var defaultService = factory.GetDefaultRelay();
        var availableKeys = factory.GetAvailableKeys();

        // Assert
        serviceAlpha.ShouldBeOfType<TestServiceA>();
        serviceBeta.ShouldBeOfType<TestServiceB>();
        serviceGamma.ShouldBeOfType<TestServiceC>();
        defaultService.ShouldBeOfType<TestServiceB>();
        availableKeys.ShouldBe(new[] { "alpha", "beta", "gamma" });
    }

    [Fact]
    public async Task AdapterPattern_WithLegacyIntegration_ShouldWorkCorrectly()
    {
        // Arrange
        Services
            .AddAdapter<ITestRepository, LegacyDataService>()
            .WithSingletonLifetime()
            .WithAdapteeLifetime(ServiceLifetime.Scoped)
            .Using<LegacyDataAdapter>();

        // Act
        var repository = GetService<ITestRepository>();
        var result = await repository.GetDataAsync(999);

        // Assert
        repository.ShouldBeOfType<LegacyDataAdapter>();
        result.ShouldBe("Legacy-999");
    }

    [Fact]
    public async Task ComplexScenario_WithAllPatterns_ShouldIntegrateSeamlessly()
    {
        // Arrange - Complex business scenario
        // 1. Basic service relay
        Services.AddRelay<ITestService, TestServiceA>().Build();

        // 2. Conditional data access based on environment
        Services
            .AddConditionalRelay<ITestRepository>()
            .When(ctx =>
                ctx.Properties.ContainsKey("UseLegacy") && (bool)ctx.Properties["UseLegacy"]
            )
            .RelayTo<TestRepositoryImplementation>()
            .Otherwise<TestRepositoryImplementation>()
            .Build();

        // 3. Multi-channel notifications
        Services
            .AddMultiRelay<ITestNotification>(config =>
                config
                    .AddRelay<EmailNotification>()
                    .AddRelay<SmsNotification>()
                    .WithStrategy(RelayStrategy.Broadcast)
            )
            .Build();

        // 4. Legacy system adapter
        Services
            .AddAdapter<ITestRepository, LegacyDataService>()
            .Using(legacy => new LegacyDataAdapter(legacy));

        // 5. Service factory for different processors
        Services
            .AddRelayFactory<ITestService>(factory =>
                factory
                    .RegisterRelay<TestServiceA>("fast")
                    .RegisterRelay<TestServiceB>("thorough")
                    .SetDefaultRelay("fast")
            )
            .Build();

        // 6. Custom context with business rules
        Services.AddScoped<IRelayContext>(provider =>
        {
            var context = new Relay.Core.Implementations.DefaultRelayContext(provider);
            context.Properties["UseLegacy"] = false;
            context.Properties["ProcessingMode"] = "fast";
            return context;
        });

        // Act - Simulate business workflow
        var processor = GetService<ITestService>();
        var repository = GetService<ITestRepository>();
        var notifications = GetService<IMultiRelay<ITestNotification>>();
        var factory = GetService<IRelayFactory<ITestService>>();

        var processResult = await processor.ProcessAsync("business-data");
        var dataResult = await repository.GetDataAsync(12345);
        var notificationCount = notifications.GetRelays().Count();
        var fastProcessor = factory.CreateRelay("fast");
        var thoroughProcessor = factory.CreateRelay("thorough");

        var fastResult = await fastProcessor.ProcessAsync("quick");
        var thoroughResult = await thoroughProcessor.ProcessAsync("detailed");

        // Assert - Verify all components work together
        processResult.ShouldBe("ServiceA: business-data");
        dataResult.ShouldNotBeNull();
        notificationCount.ShouldBe(2);
        fastResult.ShouldBe("ServiceA: quick");
        thoroughResult.ShouldBe("ServiceB: detailed");
    }

    [Fact]
    public void ServiceLifetimes_ShouldBeRespected()
    {
        // Arrange
        Services.AddRelay<ITestService, TestServiceA>().WithSingletonLifetime().Build();

        Services
            .AddMultiRelay<ITestNotification>(config =>
                config
                    .WithScopedLifetime()
                    .AddRelay<EmailNotification>(ServiceLifetime.Transient)
                    .AddRelay<SmsNotification>(ServiceLifetime.Singleton)
            )
            .Build();

        // Act
        var provider1 = ServiceProvider;
        var provider2 = Services.BuildServiceProvider();

        var service1a = provider1.GetRequiredService<ITestService>();
        var service1b = provider1.GetRequiredService<ITestService>();
        var service2 = provider2.GetRequiredService<ITestService>();

        // Assert
        // Singleton should return same instance within and across providers
        service1a.ShouldBeSameAs(service1b);
        service1a.ShouldNotBeSameAs(service2); // Different providers
    }

    [Fact]
    public async Task ErrorHandling_ShouldPropagateCorrectly()
    {
        // Arrange
        Services
            .AddMultiRelay<ITestService>(config =>
                config
                    .AddRelay<FailingTestService>()
                    .AddRelay<FailingTestService>() // Add only failing services
                    .WithStrategy(RelayStrategy.Failover)
            )
            .Build();

        var multiRelay = GetService<IMultiRelay<ITestService>>();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () =>
                await multiRelay.RelayToAllWithResults(async service =>
                    await service.ProcessAsync("test")
                )
        );
    }

    private class FailingTestService : ITestService
    {
        public string Process(string input) =>
            throw new InvalidOperationException("Service failed");

        public Task<string> ProcessAsync(string input) =>
            throw new InvalidOperationException("Service failed");
    }
}
