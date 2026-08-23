using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

public sealed class DefaultRelayContext(IServiceProvider serviceProvider) : IRelayContext
{
    /// <summary>
    /// The current environment name. Falls back to the host environment variables that
    /// ASP.NET Core and the generic host use, in the same order the host reads them.
    /// </summary>
    public string Environment { get; set; } = ResolveEnvironment();

    public IServiceProvider ServiceProvider { get; } =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    private static string ResolveEnvironment()
    {
        return FirstNonEmpty(
            System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ) ?? "Development";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
