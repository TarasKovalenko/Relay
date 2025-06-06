using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Relay.Examples.ParallelWithResults;

// This example demonstrates how to use the Relay library to process data in parallel using multiple implementations of an interface.
// Each implementation simulates different processing times and results, showcasing the flexibility of the Relay pattern.
// The example uses the `IMultiRelay` interface to relay a task to all registered processors and collect their results.
// The results include the processor name, the processed result, and the time taken for each processing task.
// The processors are executed in parallel, and the results are printed to the console.

var services = new ServiceCollection();
services
    .AddMultiRelay<IDataProcessor>(config =>
        config
            .AddRelay<FastProcessor>()
            .AddRelay<DetailedProcessor>()
            .AddRelay<AnalyticsProcessor>()
            .WithStrategy(RelayStrategy.Parallel)
    )
    .Build();

var serviceProvider = services.BuildServiceProvider();
var processors = serviceProvider.GetRequiredService<IMultiRelay<IDataProcessor>>();

var results = await processors.RelayToAllWithResults(async processor =>
    await processor.ProcessAsync("Hello World Processing Test")
);

foreach (var result in results)
{
    Console.WriteLine(
        $"{result.ProcessorName}: {result.Result} (took {result.ProcessingTime.TotalMilliseconds}ms)"
    );
}

namespace Relay.Examples.ParallelWithResults
{
    public interface IDataProcessor
    {
        Task<ProcessingResult> ProcessAsync(string data);
    }

    public class ProcessingResult
    {
        public required string ProcessorName { get; set; }
        public required string Result { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class FastProcessor : IDataProcessor
    {
        public async Task<ProcessingResult> ProcessAsync(string data)
        {
            var start = DateTime.UtcNow;
            await Task.Delay(100);
            return new ProcessingResult
            {
                ProcessorName = "Fast",
                Result = $"Fast: {data.ToUpper()}",
                ProcessingTime = DateTime.UtcNow - start,
            };
        }
    }

    public class DetailedProcessor : IDataProcessor
    {
        public async Task<ProcessingResult> ProcessAsync(string data)
        {
            var start = DateTime.UtcNow;
            await Task.Delay(300);
            return new ProcessingResult
            {
                ProcessorName = "Detailed",
                Result = $"Detailed: {data.ToLower().Replace(" ", "_")}",
                ProcessingTime = DateTime.UtcNow - start,
            };
        }
    }

    public class AnalyticsProcessor : IDataProcessor
    {
        public async Task<ProcessingResult> ProcessAsync(string data)
        {
            var start = DateTime.UtcNow;
            await Task.Delay(200);
            return new ProcessingResult
            {
                ProcessorName = "Analytics",
                Result = $"Analytics: {data.Length} chars, {data.Split(' ').Length} words",
                ProcessingTime = DateTime.UtcNow - start,
            };
        }
    }
}
