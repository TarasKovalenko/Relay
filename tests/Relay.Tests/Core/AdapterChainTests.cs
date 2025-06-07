using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Shouldly;

namespace Relay.Tests.Core;

public class AdapterChainTests
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

    public class ThrowingAdapter : IAdapter<SourceData, IntermediateData>
    {
        public IntermediateData Adapt(SourceData source)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    public class InvalidAdapter
    {
        // Does not implement IAdapter<,>
        public IntermediateData Adapt(SourceData source)
        {
            return new IntermediateData("Invalid");
        }
    }

    [Fact]
    public void AdapterChainStep_ShouldInitializeProperties()
    {
        // Arrange & Act
        var step = new AdapterChainStep
        {
            SourceType = typeof(SourceData),
            TargetType = typeof(IntermediateData),
            AdapterType = typeof(SourceToIntermediateAdapter),
            IsFinalStep = false,
        };

        // Assert
        step.SourceType.ShouldBe(typeof(SourceData));
        step.TargetType.ShouldBe(typeof(IntermediateData));
        step.AdapterType.ShouldBe(typeof(SourceToIntermediateAdapter));
        step.IsFinalStep.ShouldBeFalse();
    }

    [Fact]
    public void AdapterChain_Constructor_ShouldThrowWhenServiceProviderIsNull()
    {
        // Arrange
        var steps = new List<AdapterChainStep>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AdapterChain<FinalData>(null!, steps));
    }

    [Fact]
    public void AdapterChain_Constructor_ShouldThrowWhenStepsIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => new AdapterChain<FinalData>(serviceProvider, null!)
        );
    }

    [Fact]
    public void AdapterChain_Execute_ShouldThrowWhenSourceIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var steps = new List<AdapterChainStep>();
        var chain = new AdapterChain<FinalData>(serviceProvider, steps);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => chain.Execute<SourceData>(null!));
    }

    [Fact]
    public void AdapterChain_Execute_ShouldThrowWhenNoStepsConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var steps = new List<AdapterChainStep>();
        var chain = new AdapterChain<FinalData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => chain.Execute(source));
        exception.Message.ShouldBe("Adapter chain has no steps configured");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldThrowWhenFirstStepSourceTypeMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SourceToIntermediateAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(IntermediateData), // Wrong type
                TargetType = typeof(FinalData),
                AdapterType = typeof(SourceToIntermediateAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => chain.Execute(source));
        exception.Message.ShouldBe(
            "Chain expects source type IntermediateData but received SourceData"
        );
    }

    [Fact]
    public void AdapterChain_Execute_ShouldThrowWhenAdapterNotRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        // Don't register the adapter
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(DirectAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => chain.Execute(source));
    }

    [Fact]
    public void AdapterChain_Execute_ShouldThrowWhenAdapterDoesNotImplementInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<InvalidAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(IntermediateData),
                AdapterType = typeof(InvalidAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<IntermediateData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => chain.Execute(source));
        exception.Message.ShouldBe(
            "Adapter InvalidAdapter does not implement IAdapter<,> properly"
        );
    }

    [Fact]
    public void AdapterChain_Execute_ShouldExecuteSingleStep()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DirectAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(DirectAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act
        var result = chain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Direct: test");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldExecuteMultipleSteps()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SourceToIntermediateAdapter>();
        services.AddScoped<IntermediateToFinalAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(IntermediateData),
                AdapterType = typeof(SourceToIntermediateAdapter),
                IsFinalStep = false,
            },
            new()
            {
                SourceType = typeof(IntermediateData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(IntermediateToFinalAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act
        var result = chain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Final: Processed: test");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldThrowWhenFinalResultCannotBeCast()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SourceToIntermediateAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(IntermediateData),
                AdapterType = typeof(SourceToIntermediateAdapter),
                IsFinalStep = true,
            },
        };

        // Chain expects FinalData but step produces IntermediateData
        var chain = new AdapterChain<FinalData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => chain.Execute(source));
        exception.Message.ShouldBe(
            "Final result type IntermediateData cannot be cast to expected type FinalData"
        );
    }

    [Fact]
    public void AdapterChain_Execute_ShouldPropagateAdapterExceptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ThrowingAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(IntermediateData),
                AdapterType = typeof(ThrowingAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<IntermediateData>(serviceProvider, steps);
        var source = new SourceData("test");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => chain.Execute(source));
        exception.Message.ShouldBe("Test exception");
    }

    [Fact]
    public void TypedAdapterChain_Constructor_ShouldThrowWhenInnerChainIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => new TypedAdapterChain<SourceData, FinalData>(null!)
        );
    }

    [Fact]
    public void TypedAdapterChain_Execute_ShouldDelegateToInnerChain()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DirectAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(DirectAdapter),
                IsFinalStep = true,
            },
        };

        var innerChain = new AdapterChain<FinalData>(serviceProvider, steps);
        var typedChain = new TypedAdapterChain<SourceData, FinalData>(innerChain);
        var source = new SourceData("test");

        // Act
        var result = typedChain.Execute(source);

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Direct: test");
    }

    [Fact]
    public void TypedAdapterChain_Execute_ShouldPropagateInnerChainExceptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var steps = new List<AdapterChainStep>();

        var innerChain = new AdapterChain<FinalData>(serviceProvider, steps);
        var typedChain = new TypedAdapterChain<SourceData, FinalData>(innerChain);
        var source = new SourceData("test");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => typedChain.Execute(source));
        exception.Message.ShouldBe("Adapter chain has no steps configured");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldHandleComplexChain()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SourceToIntermediateAdapter>();
        services.AddScoped<IntermediateToFinalAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(IntermediateData),
                AdapterType = typeof(SourceToIntermediateAdapter),
                IsFinalStep = false,
            },
            new()
            {
                SourceType = typeof(IntermediateData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(IntermediateToFinalAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);

        // Act
        var result = chain.Execute(new SourceData("input"));

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Final: Processed: input");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldHandleEmptyInput()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DirectAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(DirectAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);

        // Act
        var result = chain.Execute(new SourceData(""));

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Direct: ");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DirectAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>
        {
            new()
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(DirectAdapter),
                IsFinalStep = true,
            },
        };

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);

        // Act
        var result = chain.Execute(new SourceData("!@#$%^&*()"));

        // Assert
        result.ShouldNotBeNull();
        result.FinalValue.ShouldBe("Direct: !@#$%^&*()");
    }

    [Fact]
    public void AdapterChain_Execute_ShouldHandleLongChain()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SourceToIntermediateAdapter>();
        services.AddScoped<IntermediateToFinalAdapter>();
        services.AddScoped<IdentityIntermediateAdapter>();
        var serviceProvider = services.BuildServiceProvider();

        var steps = new List<AdapterChainStep>();

        // Create a realistic long chain: SourceData -> IntermediateData (repeated) -> FinalData
        steps.Add(
            new AdapterChainStep
            {
                SourceType = typeof(SourceData),
                TargetType = typeof(IntermediateData),
                AdapterType = typeof(SourceToIntermediateAdapter),
                IsFinalStep = false,
            }
        );

        // Add multiple identity transformations
        for (int i = 0; i < 10; i++)
        {
            steps.Add(
                new AdapterChainStep
                {
                    SourceType = typeof(IntermediateData),
                    TargetType = typeof(IntermediateData),
                    AdapterType = typeof(IdentityIntermediateAdapter),
                    IsFinalStep = false,
                }
            );
        }

        // Final transformation
        steps.Add(
            new AdapterChainStep
            {
                SourceType = typeof(IntermediateData),
                TargetType = typeof(FinalData),
                AdapterType = typeof(IntermediateToFinalAdapter),
                IsFinalStep = true,
            }
        );

        var chain = new AdapterChain<FinalData>(serviceProvider, steps);

        // Act & Assert - This should work without timeout or stack overflow
        var result = chain.Execute(new SourceData("test"));
        result.ShouldNotBeNull();
        result.FinalValue.ShouldContain("Final:");
    }

    // Helper adapter for identity transformation
    public class IdentityIntermediateAdapter : IAdapter<IntermediateData, IntermediateData>
    {
        public IntermediateData Adapt(IntermediateData source)
        {
            return new IntermediateData($"Id:{source.ProcessedValue}");
        }
    }
}
