using Microsoft.Extensions.DependencyInjection;
using Relay.Extensions;
using Shouldly;

namespace Relay.Tests.Extensions;

public class LazyResolutionTests
{
    [Fact]
    public void AddLazyResolution_ShouldResolveAnyRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddLazyResolution();
        services.AddSingleton<ExpensiveService>();

        var provider = services.BuildServiceProvider();
        var lazy = provider.GetRequiredService<Lazy<ExpensiveService>>();

        lazy.ShouldNotBeNull();
        lazy.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddLazyResolution_ShouldNotCreateServiceUntilValueIsRead()
    {
        ExpensiveService.Created = 0;

        var services = new ServiceCollection();
        services.AddLazyResolution();
        services.AddTransient<ExpensiveService>();

        var provider = services.BuildServiceProvider();
        var lazy = provider.GetRequiredService<Lazy<ExpensiveService>>();

        ExpensiveService.Created.ShouldBe(0);

        _ = lazy.Value;

        ExpensiveService.Created.ShouldBe(1);
    }

    [Fact]
    public void AddLazyResolution_ShouldCacheTheResolvedInstance()
    {
        var services = new ServiceCollection();
        services.AddLazyResolution();
        services.AddTransient<ExpensiveService>();

        var provider = services.BuildServiceProvider();
        var lazy = provider.GetRequiredService<Lazy<ExpensiveService>>();

        lazy.Value.ShouldBeSameAs(lazy.Value);
    }

    [Fact]
    public void AddLazyResolution_ShouldBreakConstructorCycles()
    {
        var services = new ServiceCollection();
        services.AddLazyResolution();
        services.AddSingleton<CycleA>();
        services.AddSingleton<CycleB>();

        var provider = services.BuildServiceProvider();

        var a = provider.GetRequiredService<CycleA>();
        a.B.Value.A.Value.ShouldBeSameAs(a);
    }

    [Fact]
    public void AddLazyResolutionOfT_ShouldRegisterOnlyTheClosedType()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExpensiveService>();
        services.AddLazyResolution<ExpensiveService>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<Lazy<ExpensiveService>>().Value.ShouldNotBeNull();
        provider.GetService<Lazy<CycleA>>().ShouldBeNull();
    }

    [Fact]
    public void AddLazyResolution_ShouldThrowOnNullServices()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddLazyResolution());
    }

    private sealed class ExpensiveService
    {
        internal static int Created;

        public ExpensiveService() => Created++;
    }

    private sealed class CycleA(Lazy<CycleB> b)
    {
        public Lazy<CycleB> B { get; } = b;
    }

    private sealed class CycleB(Lazy<CycleA> a)
    {
        public Lazy<CycleA> A { get; } = a;
    }
}
