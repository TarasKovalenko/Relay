using System.Diagnostics;
using System.Reflection;

namespace Relay.Diagnostics;

/// <summary>
/// Central <see cref="System.Diagnostics.ActivitySource"/> for Relay. Subscribe to the
/// "Relay" source with an <see cref="ActivityListener"/> or OpenTelemetry to observe
/// adapter chain steps and multi-relay strategy execution.
/// </summary>
/// <remarks>
/// Activities are only created when a listener is registered, so the overhead is
/// effectively zero when nobody is observing.
/// </remarks>
public static class RelayDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> emitted by Relay.
    /// Use this string to enable tracing (e.g. <c>builder.AddSource(RelayDiagnostics.SourceName)</c>).
    /// </summary>
    public const string SourceName = "Relay";

    private static readonly string Version =
        typeof(RelayDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(RelayDiagnostics).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>
    /// The shared <see cref="System.Diagnostics.ActivitySource"/> instance used by Relay components.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(SourceName, Version);
}
