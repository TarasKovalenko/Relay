using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Builders;

public class AdapterChainBuilderTests
{
    // Test data types
    public record SourceData(string Value);

    public record IntermediateData(string ProcessedValue);

    public record FinalData(string FinalValue);

    // Test adapters
    public class SourceToIntermediateAdapter : IAdapter<SourceData, IntermediateData>
    {
        public IntermediateData Adapt(SourceData source)
        {
            return new IntermediateData($"Processed: {source.Value}");
        }
    }

    public class IntermediateToFinalAdapter : IAdapter<IntermediateData, FinalData>
    {
        public FinalData Adapt(IntermediateData source)
        {
            return new FinalData($"Final: {source.ProcessedValue}");
        }
    }

    public class DirectAdapter : IAdapter<SourceData, FinalData>
    {
        public FinalData Adapt(SourceData source)
        {
            return new FinalData($"Direct: {source.Value}");
        }
    }

    [Fact]
    public void AdapterChainBuilder_Constructor_ShouldThrowWhenServicesIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AdapterChainBuilder<FinalData>(null!));
    }

    [Fact]
    public void AdapterChainBuilder_From_ShouldReturnFromBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new AdapterChainBuilder<FinalData>(services);

        // Act
        var fromBuilder = builder.From<SourceData>();

        // Assert
        fromBuilder.ShouldNotBeNull();
        fromBuilder.ShouldBeOfType<AdapterChainFromBuilder<SourceData, FinalData>>();
    }

    [Fact]
    public void AdapterChainFromBuilder_Then_ShouldRegisterAdapterAndReturnThenBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new AdapterChainBuilder<FinalData>(services);
        var fromBuilder = builder.From<SourceData>();

        // Act
        var thenBuilder = fromBuilder.Then<IntermediateData, SourceToIntermediateAdapter>();

        // Assert
        thenBuilder.ShouldNotBeNull();
        thenBuilder.ShouldBeOfType<AdapterChainThenBuilder<IntermediateData, FinalData>>();

        // Verify adapter is registered
        var serviceProvider = services.BuildServiceProvider();
        var adapter = serviceProvider.GetService<SourceToIntermediateAdapter>();
        adapter.ShouldNotBeNull();
    }

    [Fact]
    public void AdapterChainFromBuilder_Finally_ShouldRegisterAdapterAndReturnFinalBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new AdapterChainBuilder<FinalData>(services);
        var fromBuilder = builder.From<SourceData>();

        // Act
        var finalBuilder = fromBuilder.Finally<DirectAdapter>();

        // Assert
        finalBuilder.ShouldNotBeNull();
        finalBuilder.ShouldBeOfType<AdapterChainFinalBuilder<FinalData>>();

        // Verify adapter is registered
        var serviceProvider = services.BuildServiceProvider();
        var adapter = serviceProvider.GetService<DirectAdapter>();
        adapter.ShouldNotBeNull();
    }

    [Fact]
    public void AdapterChainThenBuilder_Then_ShouldRegisterAdapterAndReturnThenBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new AdapterChainBuilder<FinalData>(services);
        var thenBuilder = builder
            .From<SourceData>()
            .Then<IntermediateData, SourceToIntermediateAdapter>();

        // Act
        var nextThenBuilder = thenBuilder.Then<FinalData, IntermediateToFinalAdapter>();

        // Assert
        nextThenBuilder.ShouldNotBeNull();
        nextThenBuilder.ShouldBeOfType<AdapterChainThenBuilder<FinalData, FinalData>>();

        // Verify both adapters are registered
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetService<SourceToIntermediateAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IntermediateToFinalAdapter>().ShouldNotBeNull();
    }

    [Fact]
    public void AdapterChainThenBuilder_Finally_ShouldRegisterAdapterAndReturnFinalBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new AdapterChainBuilder<FinalData>(services);
        var thenBuilder = builder
            .From<SourceData>()
            .Then<IntermediateData, SourceToIntermediateAdapter>();

        // Act
        var finalBuilder = thenBuilder.Finally<IntermediateToFinalAdapter>();

        // Assert
        finalBuilder.ShouldNotBeNull();
        finalBuilder.ShouldBeOfType<AdapterChainFinalBuilder<FinalData>>();

        // Verify both adapters are registered
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetService<SourceToIntermediateAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IntermediateToFinalAdapter>().ShouldNotBeNull();
    }

    [Fact]
    public void AdapterChainFinalBuilder_Build_ShouldRegisterAdapterChain()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        // Act
        services.AddAdapterChain<FinalData>().From<SourceData>().Finally<DirectAdapter>().Build();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var chain = serviceProvider.GetService<IAdapterChain<FinalData>>();
        chain.ShouldNotBeNull();
    }

    [Fact]
    public void AdapterChain_Integration_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        services.AddAdapterChain<FinalData>().From<SourceData>().Finally<DirectAdapter>().Build();

        var serviceProvider = services.BuildServiceProvider();
        var chain = serviceProvider.GetRequiredService<IAdapterChain<FinalData>>();
        var source = new SourceData("test");

        // Act
        var result = chain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Direct: test");
    }

    [Fact]
    public void AdapterChain_Integration_MultiStep_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        services
            .AddAdapterChain<FinalData>()
            .From<SourceData>()
            .Then<IntermediateData, SourceToIntermediateAdapter>()
            .Finally<IntermediateToFinalAdapter>()
            .Build();

        var serviceProvider = services.BuildServiceProvider();
        var chain = serviceProvider.GetRequiredService<IAdapterChain<FinalData>>();
        var source = new SourceData("test");

        // Act
        var result = chain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Final: Processed: test");
    }

    [Fact]
    public void AdapterChain_Integration_ComplexChain_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        // Build a more complex chain: SourceData -> IntermediateData -> IntermediateData -> FinalData
        services
            .AddAdapterChain<FinalData>()
            .From<SourceData>()
            .Then<IntermediateData, SourceToIntermediateAdapter>()
            .Then<IntermediateData, IdentityAdapter>() // Identity transformation
            .Finally<IntermediateToFinalAdapter>()
            .Build();

        var serviceProvider = services.BuildServiceProvider();
        var chain = serviceProvider.GetRequiredService<IAdapterChain<FinalData>>();
        var source = new SourceData("test");

        // Act
        var result = chain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Final: Identity: Processed: test");
    }

    [Fact]
    public void AdapterChain_MultipleChains_ShouldBeIndependent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        // Register two different chains
        services.AddAdapterChain<FinalData>().From<SourceData>().Finally<DirectAdapter>().Build();

        services
            .AddAdapterChain<IntermediateData>()
            .From<SourceData>()
            .Finally<SourceToIntermediateAdapter>()
            .Build();

        var serviceProvider = services.BuildServiceProvider();
        var finalChain = serviceProvider.GetRequiredService<IAdapterChain<FinalData>>();
        var intermediateChain = serviceProvider.GetRequiredService<
            IAdapterChain<IntermediateData>
        >();
        var source = new SourceData("test");

        // Act
        var finalResult = finalChain.Execute(source);
        var intermediateResult = intermediateChain.Execute(source);

        // Assert
        finalResult.ShouldNotBeNull();
        finalResult.FinalValue.ShouldBe("Direct: test");

        intermediateResult.ShouldNotBeNull();
        intermediateResult.ProcessedValue.ShouldBe("Processed: test");
    }

    // Helper adapter for complex chain testing
    public class IdentityAdapter : IAdapter<IntermediateData, IntermediateData>
    {
        public IntermediateData Adapt(IntermediateData source)
        {
            return new IntermediateData($"Identity: {source.ProcessedValue}");
        }
    }
}
