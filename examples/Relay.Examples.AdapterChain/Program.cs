using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Relay;
using Relay.Core.Interfaces;
using Relay.Examples.AdapterChain;

namespace Relay.Examples.AdapterChain
{
    // Example data types for the transformation pipeline
    public record RawData(string Input, DateTime Timestamp);

    public record ValidatedData(string CleanInput, DateTime Timestamp, bool IsValid);

    public record EnrichedData(
        string CleanInput,
        DateTime Timestamp,
        bool IsValid,
        string Category,
        int Score
    );

    public record ProcessedResult(
        string FinalOutput,
        DateTime ProcessedAt,
        string Status,
        Dictionary<string, object> Metadata
    );

    // Example data types for the typed chain
    public record XmlData(string XmlContent);

    public record JsonData(string JsonContent);

    public record DataDto(string Id, string Name, DateTime CreatedAt);

    public record DomainModel(string Id, string Name, DateTime CreatedAt, bool IsActive);

    // Adapter implementations
    public class ValidationAdapter(ILogger<ValidationAdapter> logger)
        : IAdapter<RawData, ValidatedData>
    {
        public ValidatedData Adapt(RawData source)
        {
            logger.LogInformation("Validating raw data: {Input}", source.Input);

            var cleanInput = source.Input?.Trim() ?? "";
            var isValid = !string.IsNullOrWhiteSpace(cleanInput);

            return new ValidatedData(cleanInput, source.Timestamp, isValid);
        }
    }

    public class EnrichmentAdapter(ILogger<EnrichmentAdapter> logger)
        : IAdapter<ValidatedData, EnrichedData>
    {
        public EnrichedData Adapt(ValidatedData source)
        {
            logger.LogInformation("Enriching validated data: {Input}", source.CleanInput);

            var category = source.CleanInput.Length switch
            {
                < 5 => "Short",
                < 20 => "Medium",
                _ => "Long",
            };

            var score = source.IsValid ? source.CleanInput.Length * 10 : 0;

            return new EnrichedData(
                source.CleanInput,
                source.Timestamp,
                source.IsValid,
                category,
                score
            );
        }
    }

    public class ProcessedResultAdapter(ILogger<ProcessedResultAdapter> logger)
        : IAdapter<EnrichedData, ProcessedResult>
    {
        public ProcessedResult Adapt(EnrichedData source)
        {
            logger.LogInformation("Creating final result for: {Input}", source.CleanInput);

            var status = source.IsValid ? "Success" : "Failed";
            var finalOutput =
                $"Processed: {source.CleanInput} (Category: {source.Category}, Score: {source.Score})";

            var metadata = new Dictionary<string, object>
            {
                ["originalTimestamp"] = source.Timestamp,
                ["category"] = source.Category,
                ["score"] = source.Score,
                ["isValid"] = source.IsValid,
            };

            return new ProcessedResult(finalOutput, DateTime.UtcNow, status, metadata);
        }
    }

    // Typed chain adapters
    public class XmlToJsonAdapter(ILogger<XmlToJsonAdapter> logger) : IAdapter<XmlData, JsonData>
    {
        public JsonData Adapt(XmlData source)
        {
            logger.LogInformation("Converting XML to JSON");

            // Simplified XML to JSON conversion
            var jsonContent =
                $"{{\"xmlData\": \"{source.XmlContent.Replace("\"", "\\\"")}\", \"convertedAt\": \"{DateTime.UtcNow:O}\"}}";

            logger.LogInformation("Converted XML to JSON: {JsonContent}", jsonContent);

            return new JsonData(jsonContent);
        }
    }

    public class JsonToDtoAdapter(ILogger<JsonToDtoAdapter> logger) : IAdapter<JsonData, DataDto>
    {
        public DataDto Adapt(JsonData source)
        {
            logger.LogInformation("Converting JSON to DTO");

            // Simplified JSON to DTO conversion
            var id = Guid.NewGuid().ToString();
            var name = "Converted Data";
            var createdAt = DateTime.UtcNow;

            logger.LogInformation(
                "Converted JSON to DTO: Id={Id}, Name={Name}, CreatedAt={CreatedAt}",
                id,
                name,
                createdAt
            );

            return new DataDto(id, name, createdAt);
        }
    }

    public class DtoToDomainAdapter(ILogger<DtoToDomainAdapter> logger)
        : IAdapter<DataDto, DomainModel>
    {
        public DomainModel Adapt(DataDto source)
        {
            logger.LogInformation("Converting DTO to Domain Model: {Id}", source.Id);

            var domainModel = new DomainModel(source.Id, source.Name, source.CreatedAt, true);

            logger.LogInformation(
                "Converted DTO to Domain Model: Id={Id}, Name={Name}, CreatedAt={CreatedAt}, IsActive={IsActive}",
                domainModel.Id,
                domainModel.Name,
                domainModel.CreatedAt,
                domainModel.IsActive
            );

            return domainModel;
        }
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        // Add relay services
        services.AddRelayServices();
        services.AddLogging(configure => configure.AddConsole());

        // Complex transformation pipeline (A → B → C → X)
        services
            .AddAdapterChain<ProcessedResult>()
            .From<RawData>() // A (source)
            .Then<ValidatedData, ValidationAdapter>() // A → B
            .Then<EnrichedData, EnrichmentAdapter>() // B → C
            .Finally<ProcessedResultAdapter>() // C → X (final)
            .Build();

        // Strongly-typed chain (XmlData → JsonData → DataDto → DomainModel)
        services
            .AddTypedAdapterChain<XmlData, DomainModel>()
            .Then<JsonData, XmlToJsonAdapter>() // XmlData → JsonData
            .Then<DataDto, JsonToDtoAdapter>() // JsonData → DataDto
            .Then<DtoToDomainAdapter>() // DataDto → DomainModel
            .Build();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("=== Relay Adapter Chain Examples ===");

            // Example 1: Complex transformation pipeline
            await RunComplexTransformationExample(serviceProvider, logger);

            // Example 2: Strongly-typed chain
            await RunTypedChainExample(serviceProvider, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during execution");
        }
    }

    private static async Task RunComplexTransformationExample(
        IServiceProvider serviceProvider,
        ILogger logger
    )
    {
        logger.LogInformation("\n--- Complex Transformation Pipeline Example ---");

        var adapterChain = serviceProvider.GetRequiredService<IAdapterChain<ProcessedResult>>();

        var testData = new[]
        {
            new RawData("  Hello World  ", DateTime.UtcNow.AddMinutes(-5)),
            new RawData("", DateTime.UtcNow.AddMinutes(-3)),
            new RawData(
                "This is a much longer piece of text that should be categorized as long",
                DateTime.UtcNow.AddMinutes(-1)
            ),
            new RawData("Short", DateTime.UtcNow),
        };

        foreach (var rawData in testData)
        {
            logger.LogInformation(
                "\nProcessing: '{Input}' from {Timestamp}",
                rawData.Input,
                rawData.Timestamp
            );

            try
            {
                var result = adapterChain.Execute(rawData);

                logger.LogInformation("Result: {FinalOutput}", result.FinalOutput);
                logger.LogInformation(
                    "Status: {Status}, Processed At: {ProcessedAt}",
                    result.Status,
                    result.ProcessedAt
                );
                logger.LogInformation(
                    "Metadata: {Metadata}",
                    string.Join(", ", result.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process data: {Input}", rawData.Input);
            }
        }

        await Task.CompletedTask;
    }

    private static async Task RunTypedChainExample(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("\n--- Strongly-Typed Chain Example ---");

        var typedChain = serviceProvider.GetRequiredService<
            ITypedAdapterChain<XmlData, DomainModel>
        >();

        var xmlTestData = new[]
        {
            new XmlData("<user><n>John Doe</n><email>john@example.com</email></user>"),
            new XmlData("<product><title>Sample Product</title><price>29.99</price></product>"),
            new XmlData("<order><id>12345</id><status>pending</status></order>"),
        };

        foreach (var xmlData in xmlTestData)
        {
            logger.LogInformation("\nProcessing XML: {XmlContent}", xmlData.XmlContent);

            try
            {
                var domainModel = typedChain.Execute(xmlData);

                logger.LogInformation("Converted to Domain Model:");
                logger.LogInformation("  ID: {Id}", domainModel.Id);
                logger.LogInformation("  Name: {Name}", domainModel.Name);
                logger.LogInformation("  Created At: {CreatedAt}", domainModel.CreatedAt);
                logger.LogInformation("  Is Active: {IsActive}", domainModel.IsActive);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process XML: {XmlContent}", xmlData.XmlContent);
            }
        }

        await Task.CompletedTask;
    }
}
