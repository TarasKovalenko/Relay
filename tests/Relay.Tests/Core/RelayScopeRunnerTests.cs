using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Core;

public class RelayScopeRunnerTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddRelayServices();
        services.AddScoped<ScopedCounter>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddRelayServices_ShouldRegisterScopeRunner()
    {
        using var provider = BuildProvider();

        provider.GetService<IRelayScopeRunner>().ShouldNotBeNull();
    }

    [Fact]
    public void Run_ShouldResolveScopedServiceOutsideOfAScope()
    {
        using var provider = BuildProvider();
        var runner = provider.GetRequiredService<IRelayScopeRunner>();

        ScopedCounter? captured = null;
        runner.Run<ScopedCounter>(counter => captured = counter);

        captured.ShouldNotBeNull();
        captured.Disposed.ShouldBeTrue("the scope, and therefore the service, is disposed");
    }

    [Fact]
    public void Run_ShouldCreateANewScopePerCall()
    {
        using var provider = BuildProvider();
        var runner = provider.GetRequiredService<IRelayScopeRunner>();

        var first = runner.Run<ScopedCounter, Guid>(c => c.Id);
        var second = runner.Run<ScopedCounter, Guid>(c => c.Id);

        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task RunAsync_ShouldResolveScopedServiceAndReturnResult()
    {
        using var provider = BuildProvider();
        var runner = provider.GetRequiredService<IRelayScopeRunner>();

        var result = await runner.RunAsync<ScopedCounter, string>(
            (counter, _) => counter.WorkAsync()
        );

        result.ShouldBe("done");
    }

    [Fact]
    public async Task RunAsync_WithProvider_ShouldExposeTheScopedProvider()
    {
        using var provider = BuildProvider();
        var runner = provider.GetRequiredService<IRelayScopeRunner>();

        ScopedCounter? captured = null;
        await runner.RunAsync(
            (scopedProvider, token) =>
            {
                token.ShouldBe(CancellationToken.None);
                captured = scopedProvider.GetRequiredService<ScopedCounter>();
                return Task.CompletedTask;
            }
        );

        captured.ShouldNotBeNull();
        captured.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_ShouldFlowCancellationToken()
    {
        using var provider = BuildProvider();
        var runner = provider.GetRequiredService<IRelayScopeRunner>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        CancellationToken observed = default;
        await runner.RunAsync<ScopedCounter>(
            (_, token) =>
            {
                observed = token;
                return Task.CompletedTask;
            },
            cts.Token
        );

        observed.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Run_ShouldThrowOnNullDelegate()
    {
        using var provider = BuildProvider();
        var runner = provider.GetRequiredService<IRelayScopeRunner>();

        Should.Throw<ArgumentNullException>(() => runner.Run<ScopedCounter>(null!));
    }

    private sealed class ScopedCounter : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Disposed { get; private set; }

        public Task<string> WorkAsync() => Task.FromResult("done");

        public void Dispose() => Disposed = true;
    }
}
