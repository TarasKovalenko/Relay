using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

public sealed class DefaultRelayContext(IServiceProvider serviceProvider) : IRelayContext
{
    public string Environment { get; set; } =
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

    public IServiceProvider ServiceProvider { get; } =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
