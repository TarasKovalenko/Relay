using Microsoft.Extensions.DependencyInjection;

namespace Relay.Validation;

/// <summary>
/// The kind of problem found while validating a <see cref="IServiceCollection"/>.
/// </summary>
public enum RelayValidationIssueKind
{
    /// <summary>
    /// A longer-lived service depends on a shorter-lived one (for example a singleton that
    /// takes a scoped dependency). The dependency is "captured" and effectively promoted to
    /// the outer lifetime, which is almost always a bug.
    /// </summary>
    CaptiveDependency,

    /// <summary>
    /// A constructor parameter has no matching registration, so resolution throws at runtime.
    /// </summary>
    MissingRegistration,

    /// <summary>
    /// A singleton depends on a transient <see cref="IDisposable"/>, so every resolution is
    /// kept alive by the container until the application shuts down.
    /// </summary>
    DisposableTransientCapture,

    /// <summary>
    /// The same service/implementation pair is registered more than once.
    /// </summary>
    DuplicateRegistration,
}

/// <summary>
/// How serious a <see cref="RelayValidationIssue"/> is.
/// </summary>
public enum RelayValidationSeverity
{
    /// <summary>Worth reviewing, but resolution still works.</summary>
    Warning,

    /// <summary>Broken registration: resolution fails or behaves incorrectly.</summary>
    Error,
}

/// <summary>
/// A single problem found by <see cref="ServiceCollectionValidationExtensions.ValidateRelayRegistrations"/>.
/// </summary>
public sealed record RelayValidationIssue
{
    /// <summary>The kind of problem.</summary>
    public required RelayValidationIssueKind Kind { get; init; }

    /// <summary>How serious the problem is.</summary>
    public required RelayValidationSeverity Severity { get; init; }

    /// <summary>The service type whose registration is at fault.</summary>
    public required Type ServiceType { get; init; }

    /// <summary>The implementation type being inspected, when known.</summary>
    public Type? ImplementationType { get; init; }

    /// <summary>The dependency the problem concerns, when the problem is about a constructor parameter.</summary>
    public Type? DependencyType { get; init; }

    /// <summary>Lifetime of the service being inspected.</summary>
    public ServiceLifetime? ServiceLifetime { get; init; }

    /// <summary>Lifetime of <see cref="DependencyType"/>, when it is registered.</summary>
    public ServiceLifetime? DependencyLifetime { get; init; }

    /// <summary>Human readable description of the problem.</summary>
    public required string Message { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"[{Severity}] {Kind}: {Message}";
}
