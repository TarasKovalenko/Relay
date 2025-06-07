using Microsoft.Extensions.DependencyInjection;
using Relay.Builders;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Builders;

public class TypedAdapterChainBuilderTests
{
    // Test data types
    public record SourceData(string Value);

    public record IntermediateData(string ProcessedValue);

    public record FinalData(string FinalValue);

    public record AlternativeData(string AltValue);

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

    public class IntermediateToAlternativeAdapter : IAdapter<IntermediateData, AlternativeData>
    {
        public AlternativeData Adapt(IntermediateData source)
        {
            return new AlternativeData($"Alternative: {source.ProcessedValue}");
        }
    }

    public class AlternativeToFinalAdapter : IAdapter<AlternativeData, FinalData>
    {
        public FinalData Adapt(AlternativeData source)
        {
            return new FinalData($"FromAlt: {source.AltValue}");
        }
    }

    [Fact]
    public void TypedAdapterChainBuilder_Constructor_ShouldThrowWhenServicesIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => new TypedAdapterChainBuilder<SourceData, FinalData>(null!)
        );
    }

    [Fact]
    public void TypedAdapterChainBuilder_Then_WithIntermediate_ShouldReturnThenBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new TypedAdapterChainBuilder<SourceData, FinalData>(services);

        // Act
        var thenBuilder = builder.Then<IntermediateData, SourceToIntermediateAdapter>();

        // Assert
        thenBuilder.ShouldNotBeNull();
        thenBuilder.ShouldBeOfType<TypedAdapterChainThenBuilder<IntermediateData, FinalData>>();

        // Verify adapter is registered
        var serviceProvider = services.BuildServiceProvider();
        var adapter = serviceProvider.GetService<SourceToIntermediateAdapter>();
        adapter.ShouldNotBeNull();
    }

    [Fact]
    public void TypedAdapterChainBuilder_Then_Direct_ShouldReturnFinalBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new TypedAdapterChainBuilder<SourceData, FinalData>(services);

        // Act
        var finalBuilder = builder.Then<DirectAdapter>();

        // Assert
        finalBuilder.ShouldNotBeNull();
        finalBuilder.ShouldBeOfType<TypedAdapterChainFinalBuilder<SourceData, FinalData>>();

        // Verify adapter is registered
        var serviceProvider = services.BuildServiceProvider();
        var adapter = serviceProvider.GetService<DirectAdapter>();
        adapter.ShouldNotBeNull();
    }

    [Fact]
    public void TypedAdapterChainThenBuilder_Then_WithIntermediate_ShouldReturnThenBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new TypedAdapterChainBuilder<SourceData, FinalData>(services);
        var thenBuilder = builder.Then<IntermediateData, SourceToIntermediateAdapter>();

        // Act
        var nextThenBuilder = thenBuilder.Then<AlternativeData, IntermediateToAlternativeAdapter>();

        // Assert
        nextThenBuilder.ShouldNotBeNull();
        nextThenBuilder.ShouldBeOfType<TypedAdapterChainThenBuilder<AlternativeData, FinalData>>();

        // Verify both adapters are registered
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetService<SourceToIntermediateAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IntermediateToAlternativeAdapter>().ShouldNotBeNull();
    }

    [Fact]
    public void TypedAdapterChainThenBuilder_Then_Final_ShouldReturnFinalBuilderProxy()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new TypedAdapterChainBuilder<SourceData, FinalData>(services);
        var thenBuilder = builder.Then<IntermediateData, SourceToIntermediateAdapter>();

        // Act
        var finalBuilder = thenBuilder.Then<IntermediateToFinalAdapter>();

        // Assert
        finalBuilder.ShouldNotBeNull();
        finalBuilder.ShouldBeOfType<
            TypedAdapterChainFinalBuilderProxy<IntermediateData, FinalData>
        >();

        // Verify both adapters are registered
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetService<SourceToIntermediateAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IntermediateToFinalAdapter>().ShouldNotBeNull();
    }

    [Fact]
    public void TypedAdapterChain_Integration_DirectAdapter_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRelayServices();

        services.AddTypedAdapterChain<SourceData, FinalData>().Then<DirectAdapter>().Build();

        var serviceProvider = services.BuildServiceProvider();
        var typedChain = serviceProvider.GetRequiredService<
            ITypedAdapterChain<SourceData, FinalData>
        >();
        var source = new SourceData("test");

        // Act
        var result = typedChain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Direct: test");
    }
}
