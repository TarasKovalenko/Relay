using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Core.Options;
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

    // ---- Lifetime-ordering fixes ----
    [Fact]
    public void RelayFactory_WithLifetimeAfterRegister_AppliesLifetime()
    {
        var services = new ServiceCollection();
        new RelayFactoryBuilder<ITestService>(services)
            .RegisterRelay<TestServiceA>("a")
            .WithLifetime(ServiceLifetime.Singleton) // called AFTER RegisterRelay
            .Build();

        var descriptor = services.First(s => s.ServiceType == typeof(TestServiceA));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void MultiRelay_WithDefaultLifetimeAfterAddRelay_AppliesLifetime()
    {
        var services = new ServiceCollection();
        new MultiRelayBuilder<ITestService>(services)
            .AddRelay<TestServiceA>()
            .WithDefaultLifetime(ServiceLifetime.Singleton) // called AFTER AddRelay
            .WithStrategy(RelayStrategy.Broadcast)
            .Build();

        var descriptor = services.First(s => s.ServiceType == typeof(TestServiceA));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void MultiRelay_ExplicitRelayLifetime_OverridesDefault()
    {
        var services = new ServiceCollection();
        new MultiRelayBuilder<ITestService>(services)
            .WithDefaultLifetime(ServiceLifetime.Singleton)
            .AddRelay<TestServiceA>(ServiceLifetime.Transient)
            .Build();

        var descriptor = services.First(s => s.ServiceType == typeof(TestServiceA));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    // ---- Resilience / retry ----
    public interface IFlaky
    {
        Task<string> CallAsync();
    }

    public sealed class FlakyRelay(int failUntil, string id) : IFlaky
    {
        public int Calls { get; private set; }

        public Task<string> CallAsync()
        {
            Calls++;
            if (Calls <= failUntil)
            {
                throw new InvalidOperationException("transient");
            }
            return Task.FromResult(id);
        }
    }

    [Fact]
    public async Task Retry_RecoversTransientFailureOnSameRelay()
    {
        var relay = new FlakyRelay(failUntil: 2, id: "A");
        var multi = new MultiRelay<IFlaky>(
            [relay],
            RelayStrategy.FirstSuccessful,
            new RelayResilienceOptions { MaxAttempts = 3 }
        );

        var result = await multi.RelayToAllWithResults(r => r.CallAsync());

        result.Single().ShouldBe("A");
        relay.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task NoRetry_FailsOverToNextRelay()
    {
        var bad = new FlakyRelay(int.MaxValue, "bad");
        var good = new FlakyRelay(0, "good");
        var multi = new MultiRelay<IFlaky>([bad, good], RelayStrategy.Failover);

        var result = await multi.RelayToAllWithResults(r => r.CallAsync());

        result.Single().ShouldBe("good");
    }

    [Fact]
    public async Task Retry_Exhausted_ThenFailsOver()
    {
        var bad = new FlakyRelay(int.MaxValue, "bad");
        var good = new FlakyRelay(0, "good");
        var multi = new MultiRelay<IFlaky>(
            [bad, good],
            RelayStrategy.Failover,
            new RelayResilienceOptions { MaxAttempts = 2 }
        );

        var result = await multi.RelayToAllWithResults(r => r.CallAsync());

        result.Single().ShouldBe("good");
        bad.Calls.ShouldBe(2); // retried twice before failover
    }

    [Fact]
    public void WithRetry_InvalidAttempts_Throws()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new MultiRelayBuilder<IFlaky>(services).WithRetry(0)
        );
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
