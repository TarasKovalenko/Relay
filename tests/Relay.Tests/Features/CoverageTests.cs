using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Core.Options;
using Relay.Decorators;
using Shouldly;

namespace Relay.Tests.Features;

// Targeted tests filling coverage gaps (guard clauses, rarely-hit branches, direct engine calls).
public class CoverageTests
{
    // ---- shared types ----
    public record Src(int N);

    public record Mid(int N);

    public record Mid2(int N);

    public record Dst(string S);

    public sealed class IncAdapter : IAsyncAdapter<Src, Mid>
    {
        public Task<Mid> AdaptAsync(Src source, CancellationToken ct = default) =>
            Task.FromResult(new Mid(source.N + 1));
    }

    public sealed class Inc2Adapter : IAsyncAdapter<Mid, Mid2>
    {
        public Task<Mid2> AdaptAsync(Mid source, CancellationToken ct = default) =>
            Task.FromResult(new Mid2(source.N + 1));
    }

    public sealed class FmtAdapter : IAsyncAdapter<Mid2, Dst>
    {
        public Task<Dst> AdaptAsync(Mid2 source, CancellationToken ct = default) =>
            Task.FromResult(new Dst($"v{source.N}"));
    }

    public sealed class DirectAsync : IAsyncAdapter<Src, Dst>
    {
        public Task<Dst> AdaptAsync(Src source, CancellationToken ct = default) =>
            Task.FromResult(new Dst($"d{source.N}"));
    }

    public record SourceData(string Value);

    public record FinalData(string Value);

    public sealed class SrcToFinal : IAdapter<SourceData, FinalData>
    {
        public FinalData Adapt(SourceData source) => new(source.Value);
    }

    public interface IFlaky
    {
        Task<string> CallAsync();
    }

    public sealed class GoodRelay(string id) : IFlaky
    {
        public int Calls { get; private set; }

        public Task<string> CallAsync()
        {
            Calls++;
            return Task.FromResult(id);
        }
    }

    public sealed class BadRelay : IFlaky
    {
        public Task<string> CallAsync() => throw new InvalidOperationException("always fails");
    }

    public sealed class OnceFlaky(string id) : IFlaky
    {
        private int _calls;

        public Task<string> CallAsync()
        {
            _calls++;
            if (_calls == 1)
            {
                throw new InvalidOperationException("transient");
            }
            return Task.FromResult(id);
        }
    }

    private static ServiceProvider Empty() => new ServiceCollection().BuildServiceProvider();

    // ---- AdapterChain.ExecuteCore wrong source ----
    [Fact]
    public void AdapterChain_ExecuteCore_WrongSource_Throws()
    {
        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(SrcToFinal),
                IsFinalStep = true,
            },
        };
        var chain = new AdapterChain<FinalData>(Empty(), steps);

        Should.Throw<InvalidOperationException>(() => chain.ExecuteCore("not-a-source"));
    }

    // ---- AsyncAdapterChain direct error paths ----
    [Fact]
    public async Task AsyncAdapterChain_EmptySteps_Throws()
    {
        var chain = new AsyncAdapterChain<Dst>(Empty(), []);
        await Should.ThrowAsync<InvalidOperationException>(() => chain.ExecuteAsync(new Src(1)));
    }

    [Fact]
    public async Task AsyncAdapterChain_WrongSource_Throws()
    {
        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(Src),
                TargetType = typeof(Mid),
                AdapterType = typeof(IncAdapter),
                IsFinalStep = true,
            },
        };
        var chain = new AsyncAdapterChain<Dst>(Empty(), steps);
        await Should.ThrowAsync<InvalidOperationException>(() => chain.ExecuteAsync(new Mid(1)));
    }

    [Fact]
    public async Task AsyncAdapterChain_FinalCastMismatch_Throws()
    {
        var services = new ServiceCollection();
        services.AddScoped<IncAdapter>();
        var provider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(Src),
                TargetType = typeof(Mid),
                AdapterType = typeof(IncAdapter),
                IsFinalStep = true,
            },
        };
        var chain = new AsyncAdapterChain<Dst>(provider, steps);
        await Should.ThrowAsync<InvalidOperationException>(() => chain.ExecuteAsync(new Src(1)));
    }

    // ---- Async builder: From().Finally() and multi-intermediate ----
    [Fact]
    public async Task AsyncChain_SingleFinalStep_Works()
    {
        var services = new ServiceCollection();
        services.AddAsyncAdapterChain<Dst>().From<Src>().Finally<DirectAsync>().Build();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IAsyncAdapterChain<Dst>>();
        (await chain.ExecuteAsync(new Src(5))).S.ShouldBe("d5");
    }

    [Fact]
    public async Task AsyncChain_MultiIntermediate_Works()
    {
        var services = new ServiceCollection();
        services
            .AddAsyncAdapterChain<Dst>()
            .From<Src>()
            .Then<Mid, IncAdapter>()
            .Then<Mid2, Inc2Adapter>()
            .Finally<FmtAdapter>()
            .Build();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IAsyncAdapterChain<Dst>>();
        (await chain.ExecuteAsync(new Src(1))).S.ShouldBe("v3");
    }

    // ---- Typed chain proxy (intermediate then final) ----
    public record TSrc(string V);

    public record TInter(string V);

    public record TDst(string V);

    public sealed class TSrcToInter : IAdapter<TSrc, TInter>
    {
        public TInter Adapt(TSrc source) => new($"{source.V}-i");
    }

    public sealed class TInterToDst : IAdapter<TInter, TDst>
    {
        public TDst Adapt(TInter source) => new($"{source.V}-d");
    }

    [Fact]
    public void TypedAdapterChain_WithIntermediate_BuildsAndExecutes()
    {
        var services = new ServiceCollection();
        services
            .AddTypedAdapterChain<TSrc, TDst>()
            .Then<TInter, TSrcToInter>()
            .Then<TInterToDst>()
            .Build();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<ITypedAdapterChain<TSrc, TDst>>();
        chain.Execute(new TSrc("x")).V.ShouldBe("x-i-d");
    }

    // ---- RelayResolver without accessor registered ----
    [Fact]
    public void RelayResolver_NoAccessor_ResolvesDirectly()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITestService, TestServiceA>();
        using var provider = services.BuildServiceProvider();

        var resolver = new RelayResolver(provider);
        resolver.Resolve<ITestService>().ShouldBeOfType<TestServiceA>();
    }

    // ---- MultiRelay ctor + RelayToAll strategies + retry delay ----
    [Fact]
    public void MultiRelay_InvalidResilience_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new MultiRelay<IFlaky>([new GoodRelay("a")], RelayStrategy.Failover, new RelayResilienceOptions { MaxAttempts = 0 })
        );
    }

    [Fact]
    public async Task MultiRelay_RelayToAll_Parallel_CallsAll()
    {
        var a = new GoodRelay("a");
        var b = new GoodRelay("b");
        var multi = new MultiRelay<IFlaky>([a, b], RelayStrategy.Parallel);

        await multi.RelayToAll(r => r.CallAsync());

        a.Calls.ShouldBe(1);
        b.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task MultiRelay_RelayToAll_RoundRobin_CallsOne()
    {
        var a = new GoodRelay("a");
        var b = new GoodRelay("b");
        var multi = new MultiRelay<IFlaky>([a, b], RelayStrategy.RoundRobin);

        await multi.RelayToAll(r => r.CallAsync());

        (a.Calls + b.Calls).ShouldBe(1);
    }

    [Fact]
    public async Task MultiRelay_RelayToAll_Failover_AllFail_Throws()
    {
        var multi = new MultiRelay<IFlaky>([new BadRelay(), new BadRelay()], RelayStrategy.Failover);

        await Should.ThrowAsync<InvalidOperationException>(() => multi.RelayToAll(r => r.CallAsync()));
    }

    [Fact]
    public async Task MultiRelay_RelayToAll_Failover_Succeeds()
    {
        var good = new GoodRelay("good");
        var multi = new MultiRelay<IFlaky>([new BadRelay(), good], RelayStrategy.Failover);

        await multi.RelayToAll(r => r.CallAsync());

        good.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task MultiRelay_Retry_WithDelay_RecoversTransient()
    {
        var relay = new OnceFlaky("ok");
        var multi = new MultiRelay<IFlaky>(
            [relay],
            RelayStrategy.FirstSuccessful,
            new RelayResilienceOptions
            {
                MaxAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(1),
                BackoffFactor = 2.0,
            }
        );

        (await multi.RelayToAllWithResults(r => r.CallAsync())).Single().ShouldBe("ok");
    }

    // ---- MultiRelayBuilder.WithRetry success path ----
    [Fact]
    public async Task MultiRelayBuilder_WithRetry_Configures()
    {
        var services = new ServiceCollection();
        new MultiRelayBuilder<IFlaky>(services)
            .AddRelay<OnceFlaky>()
            .WithStrategy(RelayStrategy.FirstSuccessful)
            .WithRetry(2, TimeSpan.FromMilliseconds(1), 1.5)
            .Build();
        services.AddSingleton(new OnceFlaky("done"));

        // Re-register OnceFlaky as the concrete relay resolves it; ensure build succeeded.
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiRelay<IFlaky>>().ShouldNotBeNull();
    }

    // ---- RelayFactoryBuilder delegate form + guards ----
    [Fact]
    public void RelayFactory_DelegateRegistration_Works()
    {
        var services = new ServiceCollection();
        new RelayFactoryBuilder<ITestService>(services)
            .RegisterRelay("k", _ => new TestServiceA())
            .WithLifetime(ServiceLifetime.Singleton)
            .SetDefaultRelay("k")
            .Build();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IRelayFactory<ITestService>>();
        factory.CreateRelay("k").ShouldBeOfType<TestServiceA>();
        factory.GetDefaultRelay().ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void RelayFactoryBuilder_EmptyKeyGuards_Throw()
    {
        var b = new RelayFactoryBuilder<ITestService>(new ServiceCollection());
        Should.Throw<ArgumentException>(() => b.RegisterRelay("", _ => new TestServiceA()));
        Should.Throw<ArgumentNullException>(() => b.RegisterRelay("k", null!));
        Should.Throw<ArgumentException>(() => b.RegisterRelay<TestServiceA>(""));
        Should.Throw<ArgumentException>(() => b.RegisterKeyedRelay<TestServiceA>(""));
        Should.Throw<ArgumentException>(() => b.SetDefaultRelay(""));
    }

    // ---- AdapterChainFactoryBuilder guards + WithLifetime ----
    [Fact]
    public void AdapterChainFactoryBuilder_EmptyNameGuards_Throw()
    {
        var b = new AdapterChainFactoryBuilder<FinalData>(new ServiceCollection());
        Should.Throw<ArgumentException>(() => b.AddChain("", _ => new FinalData("x")));
        Should.Throw<ArgumentException>(() => b.AddChain(""));
    }

    [Fact]
    public void AdapterChainFactoryBuilder_WithLifetime_Applies()
    {
        var services = new ServiceCollection();
        new AdapterChainFactoryBuilder<FinalData>(services)
            .WithLifetime(ServiceLifetime.Singleton)
            .AddChain("a", _ => new FinalData("a"))
            .Build();

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IAdapterChainFactory<FinalData>)
        );
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    // ---- Decorator second overload: not-registered + instance-based ----
    [Fact]
    public void Decorate_Func_ServiceNotRegistered_Throws()
    {
        var services = new ServiceCollection();
        Should.Throw<InvalidOperationException>(() =>
            services.Decorate<ITestService>((inner, _) => new LoggingDecorator(inner))
        );
    }

    [Fact]
    public void Decorate_Func_InstanceRegistration_Wraps()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService>(new TestServiceA());
        services.Decorate<ITestService>((inner, _) => new LoggingDecorator(inner));

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ITestService>();
        service.Process("x").ShouldBe("[Logged] ServiceA: x");
    }

    // ---- Reachable contract throws ----
    public sealed class NotAnAdapter; // intentionally does not implement IAdapter<,>

    public record WrongData(string Value);

    public sealed class SrcToWrong : IAdapter<SourceData, WrongData>
    {
        public WrongData Adapt(SourceData source) => new(source.Value);
    }

    [Fact]
    public void AdapterChain_AdapterMissingInterface_Throws()
    {
        var services = new ServiceCollection();
        services.AddScoped<NotAnAdapter>();
        var provider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(NotAnAdapter),
                IsFinalStep = true,
            },
        };
        var chain = new AdapterChain<FinalData>(provider, steps);

        Should.Throw<InvalidOperationException>(() => chain.ExecuteCore(new SourceData("x")));
    }

    [Fact]
    public void AdapterChain_FinalCastMismatch_Throws()
    {
        var services = new ServiceCollection();
        services.AddScoped<SrcToWrong>();
        var provider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(WrongData),
                AdapterType = typeof(SrcToWrong),
                IsFinalStep = true,
            },
        };
        var chain = new AdapterChain<FinalData>(provider, steps);

        Should.Throw<InvalidOperationException>(() => chain.ExecuteCore(new SourceData("x")));
    }

    // ---- Public constructor / builder null guards ----
    [Fact]
    public void NullGuards_Throw()
    {
        var provider = Empty();
        var steps = new List<AdapterChainStep>();
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => new AdapterChain<FinalData>(null!, steps));
        Should.Throw<ArgumentNullException>(() => new AdapterChain<FinalData>(provider, null!));
        Should.Throw<ArgumentNullException>(() => new AsyncAdapterChain<Dst>(null!, steps));
        Should.Throw<ArgumentNullException>(() => new AsyncAdapterChain<Dst>(provider, null!));
        Should.Throw<ArgumentNullException>(() => new MultiRelay<IFlaky>(null!, RelayStrategy.Broadcast));
        Should.Throw<ArgumentNullException>(() => new TypedAdapterChain<TSrc, TDst>(null!));
        Should.Throw<ArgumentNullException>(() => new DefaultRelayContext(null!));
        Should.Throw<ArgumentNullException>(() => new RelayResolver(null!));
        Should.Throw<ArgumentNullException>(() =>
            new RelayFactory<ITestService>(null!, provider, null)
        );
        Should.Throw<ArgumentNullException>(() =>
            new RelayFactory<ITestService>(
                new Dictionary<string, Func<IServiceProvider, ITestService>>(),
                null!,
                null
            )
        );
        Should.Throw<ArgumentNullException>(() => new MultiRelayBuilder<IFlaky>(null!));
        Should.Throw<ArgumentNullException>(() => new RelayFactoryBuilder<ITestService>(null!));
        Should.Throw<ArgumentNullException>(() => new AdapterChainFactoryBuilder<FinalData>(null!));
        Should.Throw<ArgumentNullException>(() => new ConditionalRelayBuilder<ITestService>(null!));
        Should.Throw<ArgumentNullException>(() => services.AddRelay(null!));
    }

    // ---- Conditional relay TypeSelector path ----
    [Fact]
    public void ConditionalRelay_TypeSelector_Resolves()
    {
        var services = new ServiceCollection();
        services.AddRelayServices();
        services
            .AddConditionalRelay<ITestService>()
            .When(_ => true)
            .RelayTo(_ => typeof(TestServiceA))
            .Build();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITestService>().ShouldBeOfType<TestServiceA>();
    }

    [Fact]
    public void ConditionalRelay_NoMatch_Throws()
    {
        var services = new ServiceCollection();
        services.AddRelayServices();
        services.AddConditionalRelay<ITestService>().When(_ => false).RelayTo<TestServiceA>().Build();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Should.Throw<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ITestService>()
        );
    }

    // ---- Decorator first (Type) overload across registration sources ----
    [Fact]
    public void Decorate_Type_FactoryRegistration_Wraps()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITestService>(_ => new TestServiceA());
        services.Decorate<ITestService>(typeof(LoggingDecorator));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ITestService>().Process("x").ShouldBe("[Logged] ServiceA: x");
    }

    [Fact]
    public void Decorate_Type_InstanceRegistration_Wraps()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService>(new TestServiceA());
        services.Decorate<ITestService>(typeof(LoggingDecorator));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ITestService>().Process("x").ShouldBe("[Logged] ServiceA: x");
    }
}
