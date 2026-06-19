using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Diagnostics;
using Shouldly;

namespace Relay.Tests.Features;

public class NewFeaturesTests
{
    // ---- Async adapter chain test types ----
    public record Src(int N);

    public record Mid(int N);

    public record Dst(string S);

    public class IncrementAdapter : IAsyncAdapter<Src, Mid>
    {
        public async Task<Mid> AdaptAsync(Src source, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return new Mid(source.N + 1);
        }
    }

    public class FormatAdapter : IAsyncAdapter<Mid, Dst>
    {
        public async Task<Dst> AdaptAsync(Mid source, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return new Dst($"v{source.N}");
        }
    }

    [Fact]
    public async Task AsyncAdapterChain_ExecutesAllSteps()
    {
        var services = new ServiceCollection();
        services
            .AddAsyncAdapterChain<Dst>()
            .From<Src>()
            .Then<Mid, IncrementAdapter>()
            .Finally<FormatAdapter>()
            .Build();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IAsyncAdapterChain<Dst>>();

        var result = await chain.ExecuteAsync(new Src(1));

        result.S.ShouldBe("v2");
    }

    [Fact]
    public async Task AsyncAdapterChain_PropagatesAdapterException()
    {
        var services = new ServiceCollection();
        services
            .AddAsyncAdapterChain<Dst>()
            .From<Src>()
            .Then<Mid, ThrowingAsyncAdapter>()
            .Finally<FormatAdapter>()
            .Build();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IAsyncAdapterChain<Dst>>();

        await Should.ThrowAsync<InvalidOperationException>(() => chain.ExecuteAsync(new Src(1)));
    }

    public class ThrowingAsyncAdapter : IAsyncAdapter<Src, Mid>
    {
        public Task<Mid> AdaptAsync(Src source, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    // ---- Context-aware resolution ----
    [Fact]
    public void Resolver_FlowsContext_ToConditionalRelay()
    {
        var services = new ServiceCollection();
        services.AddRelayServices();
        services
            .AddConditionalRelay<ITestService>()
            .When(ctx =>
                ctx.Properties.TryGetValue("variant", out var v) && (string)v == "A"
            )
            .RelayTo<TestServiceA>()
            .Otherwise<TestServiceB>()
            .Build();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IRelayResolver>();

        var ctxA = new DefaultRelayContext(scope.ServiceProvider);
        ctxA.Properties["variant"] = "A";
        resolver.Resolve<ITestService>(ctxA).ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void Resolver_WithoutMatchingContext_UsesOtherwise()
    {
        var services = new ServiceCollection();
        services.AddRelayServices();
        services
            .AddConditionalRelay<ITestService>()
            .When(ctx => ctx.Properties.ContainsKey("variant"))
            .RelayTo<TestServiceA>()
            .Otherwise<TestServiceB>()
            .Build();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IRelayResolver>();

        var ctx = new DefaultRelayContext(scope.ServiceProvider);
        resolver.Resolve<ITestService>(ctx).ShouldBeOfType<TestServiceB>();
    }

    // ---- Native keyed services ----
    [Fact]
    public void AddKeyedRelay_ResolvesViaKeyedService()
    {
        var services = new ServiceCollection();
        services.AddKeyedRelay<ITestService, TestServiceA>("a");
        services.AddKeyedRelay<ITestService, TestServiceB>("b");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<ITestService>("a").ShouldBeOfType<TestServiceA>();
        provider.GetRequiredKeyedService<ITestService>("b").ShouldBeOfType<TestServiceB>();
    }

    [Fact]
    public void RelayFactory_KeyedAndContextSelector_Work()
    {
        var services = new ServiceCollection();
        var builder = services.AddRelayFactory<ITestService>(cfg =>
            cfg.RegisterKeyedRelay<TestServiceA>("a")
                .RegisterKeyedRelay<TestServiceB>("b")
                .SelectKeyByContext(ctx => (string)ctx.Properties["key"])
                .SetDefaultRelay("a")
        );
        builder.Build();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IRelayFactory<ITestService>>();

        factory.CreateRelay("a").ShouldBeOfType<TestServiceA>();
        factory.GetDefaultRelay().ShouldBeOfType<TestServiceA>();

        var ctx = new DefaultRelayContext(scope.ServiceProvider);
        ctx.Properties["key"] = "b";
        factory.CreateRelay(ctx).ShouldBeOfType<TestServiceB>();
    }

    // ---- Diagnostics ----
    [Fact]
    public async Task AdapterChain_EmitsActivity()
    {
        var activities = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == RelayDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStarted = a => activities.Add(a.OperationName),
        };
        ActivitySource.AddActivityListener(listener);

        var services = new ServiceCollection();
        services
            .AddAsyncAdapterChain<Dst>()
            .From<Src>()
            .Then<Mid, IncrementAdapter>()
            .Finally<FormatAdapter>()
            .Build();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IAsyncAdapterChain<Dst>>();
        await chain.ExecuteAsync(new Src(1));

        activities.ShouldContain("AsyncAdapterChain.Execute");
        activities.ShouldContain("AsyncAdapterChain.Step");
    }
}
