namespace Relay.Core.Interfaces;

/// <summary>
/// Context for relay resolution with environment and custom properties
/// </summary>
public interface IRelayContext
{
    /// <summary>
    ///  Gets the environment name for the relay context, such as "Development", "Staging", or "Production".
    /// </summary>
    string Environment { get; }

    /// <summary>
    ///  Gets the service provider for dependency injection within the relay context.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    ///  Gets a collection of custom properties for additional context information.
    /// </summary>
    IDictionary<string, object> Properties { get; }
}
