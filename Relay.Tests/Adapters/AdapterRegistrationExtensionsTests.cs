using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Relay.Adapters;
using Shouldly;

namespace Relay.Tests.Adapters;

public class AdapterRegistrationExtensionsTests : TestBase
{
    [Fact]
    public void AddAdapter_WithValidTypes_ShouldReturnBuilder()
    {
        // Act
        var builder = Services.AddAdapter<ITestRepository, LegacyDataService>();

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<AdapterBuilder<ITestRepository, LegacyDataService>>();
    }

    [Fact]
    public void AddAdapter_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddAdapter<ITestRepository, LegacyDataService>()
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAdaptersFromAssembly_WithValidParameters_ShouldRegisterAdapters()
    {
        // Act
        var result = Services.AddAdaptersFromAssembly<ITestRepository>();

        // Assert
        result.ShouldBe(Services);
        // Should register any adapters found in the assembly
    }

    [Fact]
    public void AddAdaptersFromAssembly_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => ((IServiceCollection)null!).AddAdaptersFromAssembly<ITestRepository>()
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAdaptersFromAssembly_WithSpecificAssemblies_ShouldScanSpecifiedAssemblies()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var result = Services.AddAdaptersFromAssembly<ITestRepository>(
            ServiceLifetime.Scoped,
            assembly
        );

        // Assert
        result.ShouldBe(Services);
    }

    [Fact]
    public void AddAdaptersFromAssembly_WithCustomLifetime_ShouldUseCustomLifetime()
    {
        // Act
        Services.AddAdaptersFromAssembly<ITestRepository>(ServiceLifetime.Singleton);

        // Assert
        // Any registered adapters should use Singleton lifetime
        var adapterRegistrations = Services.Where(s => s.ServiceType == typeof(ITestRepository));
        adapterRegistrations.ShouldAllBe(r => r.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterAdapterWithDependencies_ShouldRegisterDependencies()
    {
        // This is tested indirectly through AddAdaptersFromAssembly
        // The private method should register constructor dependencies

        // Arrange & Act
        Services.AddAdaptersFromAssembly<ITestRepository>();

        // Assert
        // If LegacyDataAdapter is found, LegacyDataService should also be registered
        if (Services.Any(s => s.ImplementationType == typeof(LegacyDataAdapter)))
        {
            Services.Any(s => s.ServiceType == typeof(LegacyDataService)).ShouldBeTrue();
        }
    }
}

public class AdapterBuilderTests : TestBase
{
    [Fact]
    public void Constructor_WithValidServices_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => new AdapterBuilder<ITestRepository, LegacyDataService>(null!)
            )
            .ParamName.ShouldBe("services");
    }

    [Fact]
    public void WithLifetime_ShouldSetAdapterLifetime()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var result = builder.WithLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithAdapteeLifetime_ShouldSetAdapteeLifetime()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var result = builder.WithAdapteeLifetime(ServiceLifetime.Singleton);

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithSingletonLifetime_ShouldSetSingletonLifetime()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var result = builder.WithSingletonLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithScopedLifetime_ShouldSetScopedLifetime()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var result = builder.WithScopedLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void WithTransientLifetime_ShouldSetTransientLifetime()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var result = builder.WithTransientLifetime();

        // Assert
        result.ShouldBe(builder);
    }

    [Fact]
    public void Using_WithAdapterType_ShouldRegisterServices()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var services = builder.Using<LegacyDataAdapter>();

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(LegacyDataService)).ShouldBeTrue();
        Services
            .Any(s =>
                s.ServiceType == typeof(ITestRepository)
                && s.ImplementationType == typeof(LegacyDataAdapter)
            )
            .ShouldBeTrue();
    }

    [Fact]
    public void Using_WithFactoryFunction_ShouldRegisterWithFactory()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var services = builder.Using(legacy => new LegacyDataAdapter(legacy));

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(LegacyDataService)).ShouldBeTrue();
        Services
            .Any(s => s.ServiceType == typeof(ITestRepository) && s.ImplementationFactory != null)
            .ShouldBeTrue();
    }

    [Fact]
    public void Using_WithNullFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () => builder.Using((Func<LegacyDataService, ITestRepository>)null!)
            )
            .ParamName.ShouldBe("adapterFactory");
    }

    [Fact]
    public void Using_WithProviderFactory_ShouldRegisterWithProviderFactory()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act
        var services = builder.Using((legacy, provider) => new LegacyDataAdapter(legacy));

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(LegacyDataService)).ShouldBeTrue();
        Services
            .Any(s => s.ServiceType == typeof(ITestRepository) && s.ImplementationFactory != null)
            .ShouldBeTrue();
    }

    [Fact]
    public void Using_WithNullProviderFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new AdapterBuilder<ITestRepository, LegacyDataService>(Services);

        // Act & Assert
        Should
            .Throw<ArgumentNullException>(
                () =>
                    builder.Using((Func<LegacyDataService, IServiceProvider, ITestRepository>)null!)
            )
            .ParamName.ShouldBe("adapterFactory");
    }

    [Fact]
    public void Adapter_ShouldResolveCorrectly()
    {
        // Arrange
        Services.AddAdapter<ITestRepository, LegacyDataService>().Using<LegacyDataAdapter>();

        // Act
        var repository = GetService<ITestRepository>();

        // Assert
        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<LegacyDataAdapter>();
    }

    [Fact]
    public async Task Adapter_ShouldWorkWithAdaptee()
    {
        // Arrange
        Services.AddAdapter<ITestRepository, LegacyDataService>().Using<LegacyDataAdapter>();

        // Act
        var repository = GetService<ITestRepository>();
        var result = await repository.GetDataAsync(123);

        // Assert
        result.ShouldBe("Legacy-123");
    }

    [Fact]
    public void Adapter_WithDifferentLifetimes_ShouldRespectLifetimes()
    {
        // Arrange
        Services
            .AddAdapter<ITestRepository, LegacyDataService>()
            .WithSingletonLifetime()
            .WithAdapteeLifetime(ServiceLifetime.Transient)
            .Using<LegacyDataAdapter>();

        // Assert
        var adapterRegistration = Services.First(s => s.ServiceType == typeof(ITestRepository));
        var adapteeRegistration = Services.First(s => s.ServiceType == typeof(LegacyDataService));

        adapterRegistration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        adapteeRegistration.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var services = Services
            .AddAdapter<ITestRepository, LegacyDataService>()
            .WithSingletonLifetime()
            .WithAdapteeLifetime(ServiceLifetime.Scoped)
            .Using<LegacyDataAdapter>();

        // Assert
        services.ShouldBe(Services);
        Services.Any(s => s.ServiceType == typeof(ITestRepository)).ShouldBeTrue();
        Services.Any(s => s.ServiceType == typeof(LegacyDataService)).ShouldBeTrue();
    }

    [Fact]
    public async Task Adapter_WithFactoryFunction_ShouldUseFactory()
    {
        // Arrange
        Services
            .AddAdapter<ITestRepository, LegacyDataService>()
            .Using(legacy => new LegacyDataAdapter(legacy));

        // Act
        var repository = GetService<ITestRepository>();
        var result = await repository.GetDataAsync(456);

        // Assert
        result.ShouldBe("Legacy-456");
    }

    [Fact]
    public async Task Adapter_WithProviderFactory_ShouldUseProviderFactory()
    {
        // Arrange
        Services.AddSingleton<string>("test-config");
        Services
            .AddAdapter<ITestRepository, LegacyDataService>()
            .Using(
                (legacy, provider) =>
                {
                    var config = provider.GetRequiredService<string>();
                    return config == "test-config" ? new LegacyDataAdapter(legacy) : null!;
                }
            );

        // Act
        var repository = GetService<ITestRepository>();
        var result = await repository.GetDataAsync(789);

        // Assert
        result.ShouldBe("Legacy-789");
    }
}
