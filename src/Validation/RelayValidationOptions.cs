namespace Relay.Validation;

/// <summary>
/// Controls which checks <see cref="ServiceCollectionValidationExtensions.ValidateRelayRegistrations"/> runs.
/// </summary>
public sealed record RelayValidationOptions
{
    /// <summary>Report singletons that depend on scoped services. Enabled by default.</summary>
    public bool CheckCaptiveDependencies { get; init; } = true;

    /// <summary>Report constructor parameters that have no registration. Enabled by default.</summary>
    public bool CheckMissingRegistrations { get; init; } = true;

    /// <summary>Report singletons that depend on transient <see cref="IDisposable"/> services. Enabled by default.</summary>
    public bool CheckDisposableTransients { get; init; } = true;

    /// <summary>Report the same service/implementation pair registered more than once. Disabled by default.</summary>
    public bool CheckDuplicateRegistrations { get; init; }

    /// <summary>
    /// Skip "missing registration" reports for <c>System.*</c> and <c>Microsoft.*</c> types.
    /// Enabled by default, because those are usually contributed by a host builder that is not
    /// part of the collection being validated.
    /// </summary>
    public bool IgnoreMissingFrameworkServices { get; init; } = true;

    /// <summary>Service types to exclude from every check.</summary>
    public IReadOnlyCollection<Type> IgnoredServiceTypes { get; init; } = [];

    /// <summary>Extra predicate to exclude service or dependency types from every check.</summary>
    public Func<Type, bool>? Ignore { get; init; }

    /// <summary>The default options.</summary>
    public static RelayValidationOptions Default { get; } = new();
}
