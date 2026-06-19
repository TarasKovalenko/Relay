using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Features;

public class AdapterChainFactoryTests
{
    public record Raw(string Value);

    public record Intermediate(string Value);

    public sealed class Settings
    {
        public required string Value { get; init; }
    }

    public class RawToIntermediate : IAdapter<Raw, Intermediate>
    {
        public Intermediate Adapt(Raw source) => new($"{source.Value}->mid");
    }

    public class IntermediateToSettings : IAdapter<Intermediate, Settings>
    {
        public Settings Adapt(Intermediate source) => new() { Value = $"{source.Value}->settings" };
    }

    public class RawToSettings : IAdapter<Raw, Settings>
    {
        public Settings Adapt(Raw source) => new() { Value = $"{source.Value}->direct" };
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Raw("seed"));
        services
            .AddAdapterChainFactory<Settings>()
            .AddChain("full")
            .From<Raw>()
            .Then<Intermediate, RawToIntermediate>()
            .Finally<IntermediateToSettings>()
            .AddChain("direct")
            .From<Raw>()
            .Finally<RawToSettings>()
            .AddChain("mock", _ => new Settings { Value = "mock" })
            .Build();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetAvailableChains_ReturnsAllRegisteredNames()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAdapterChainFactory<Settings>>();

        factory.GetAvailableChains().ShouldBe(["full", "direct", "mock"], ignoreOrder: true);
    }

    [Fact]
    public void CreateFromChain_MultiStep_ExecutesEntireChain()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAdapterChainFactory<Settings>>();

        factory.CreateFromChain("full").Value.ShouldBe("seed->mid->settings");
    }

    [Fact]
    public void CreateFromChain_DirectChain_ExecutesSingleStep()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAdapterChainFactory<Settings>>();

        factory.CreateFromChain("direct").Value.ShouldBe("seed->direct");
    }

    [Fact]
    public void CreateFromChain_ProducerDelegate_Works()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAdapterChainFactory<Settings>>();

        factory.CreateFromChain("mock").Value.ShouldBe("mock");
    }

    [Fact]
    public void CreateFromChain_UnknownName_Throws()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAdapterChainFactory<Settings>>();

        Should.Throw<ArgumentException>(() => factory.CreateFromChain("nope"));
    }

    [Fact]
    public void CreateFromChain_EmptyName_Throws()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAdapterChainFactory<Settings>>();

        Should.Throw<ArgumentException>(() => factory.CreateFromChain(""));
    }
}
