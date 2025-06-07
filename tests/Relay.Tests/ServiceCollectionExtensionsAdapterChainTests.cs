using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests;

public class ServiceCollectionExtensionsAdapterChainTests
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
    public void AddAdapterChain_ShouldThrowWhenServicesIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => ServiceCollectionExtensions.AddAdapterChain<FinalData>(null!)
        );
    }

    [Fact]
    public void AddAdapterChain_ShouldReturnAdapterChainBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddAdapterChain<FinalData>();

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<Relay.Builders.AdapterChainBuilder<FinalData>>();
    }

    [Fact]
    public void AddAdapterChain_Integration_ShouldRegisterAndResolveSuccessfully()
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

        // Verify it works
        var result = chain.Execute(new SourceData("test"));
        result.FinalValue.ShouldBe("Direct: test");
    }

    [Fact]
    public void AdapterChain_ComplexIntegration_ShouldWorkEndToEnd()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        // Act - Create a complex chain
        services
            .AddAdapterChain<FinalData>()
            .From<SourceData>()
            .Then<IntermediateData, SourceToIntermediateAdapter>()
            .Finally<IntermediateToFinalAdapter>()
            .Build();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var chain = serviceProvider.GetRequiredService<IAdapterChain<FinalData>>();

        var result = chain.Execute(new SourceData("integration-test"));
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Final: Processed: integration-test");
    }
}
