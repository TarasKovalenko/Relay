using Microsoft.Extensions.DependencyInjection;

namespace Relay.Tests;

public abstract class TestBase
{
    protected IServiceCollection Services { get; }
    protected IServiceProvider ServiceProvider => Services.BuildServiceProvider();

    protected TestBase()
    {
        Services = new ServiceCollection();
        Services.AddRelayServices();
    }

    protected T GetService<T>()
        where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    protected T? GetOptionalService<T>()
        where T : class
    {
        return ServiceProvider.GetService<T>();
    }
}

// Test interfaces and implementations
public interface ITestService
{
    string Process(string input);
    Task<string> ProcessAsync(string input);
}

public interface ITestRepository
{
    Task<string> GetDataAsync(int id);
}

public interface ITestNotification
{
    Task SendAsync(string message);
}

public class TestServiceA : ITestService
{
    public string Process(string input) => $"ServiceA: {input}";

    public async Task<string> ProcessAsync(string input)
    {
        await Task.Delay(1);
        return $"ServiceA: {input}";
    }
}

public class TestServiceB : ITestService
{
    public string Process(string input) => $"ServiceB: {input}";

    public async Task<string> ProcessAsync(string input)
    {
        await Task.Delay(1);
        return $"ServiceB: {input}";
    }
}

public class TestServiceC : ITestService
{
    public string Process(string input) => $"ServiceC: {input}";

    public async Task<string> ProcessAsync(string input)
    {
        await Task.Delay(1);
        return $"ServiceC: {input}";
    }
}

public class TestRepositoryImplementation : ITestRepository
{
    public async Task<string> GetDataAsync(int id)
    {
        await Task.Delay(1);
        return $"Data-{id}";
    }
}

public class EmailNotification : ITestNotification
{
    public async Task SendAsync(string message)
    {
        await Task.Delay(1);
        // Email sending logic
    }
}

public class SmsNotification : ITestNotification
{
    public async Task SendAsync(string message)
    {
        await Task.Delay(1);
        // SMS sending logic
    }
}

public class PushNotification : ITestNotification
{
    public async Task SendAsync(string message)
    {
        await Task.Delay(1);
        // Push notification logic
    }
}

// Legacy system for adapter tests
public class LegacyDataService
{
    public string GetLegacyData(int id) => $"Legacy-{id}";
}

// Adapter for legacy system
public class LegacyDataAdapter : ITestRepository
{
    private readonly LegacyDataService _legacyService;

    public LegacyDataAdapter(LegacyDataService legacyService)
    {
        _legacyService = legacyService;
    }

    public async Task<string> GetDataAsync(int id)
    {
        await Task.Delay(1);
        return _legacyService.GetLegacyData(id);
    }
}

// Decorator for testing
public class LoggingDecorator : ITestService
{
    private readonly ITestService _inner;

    public LoggingDecorator(ITestService inner)
    {
        _inner = inner;
    }

    public string Process(string input)
    {
        var result = _inner.Process(input);
        return $"[Logged] {result}";
    }

    public async Task<string> ProcessAsync(string input)
    {
        var result = await _inner.ProcessAsync(input);
        return $"[Logged] {result}";
    }
}
