using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Relay.Validation;

/// <summary>
/// Static analysis for a <see cref="IServiceCollection"/>: finds captive dependencies, missing
/// registrations, captured disposable transients and duplicate registrations before the
/// application ever builds a provider.
/// </summary>
/// <remarks>
/// This complements <c>ServiceProviderOptions.ValidateOnBuild</c>/<c>ValidateScopes</c>: those
/// only fail once something is resolved (and <c>ValidateScopes</c> only in development), while
/// this runs over the descriptors themselves and reports every problem at once.
/// </remarks>
public static class ServiceCollectionValidationExtensions
{
    // Provided by the container itself, so they never need an explicit registration.
    private static readonly HashSet<Type> AlwaysAvailable =
    [
        typeof(IServiceProvider),
        typeof(IServiceScopeFactory),
        typeof(IServiceProviderIsService),
        typeof(IServiceProviderIsKeyedService),
    ];

    /// <summary>
    /// Inspects every registration and reports the problems found.
    /// </summary>
    public static RelayValidationResult ValidateRelayRegistrations(
        this IServiceCollection services,
        RelayValidationOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= RelayValidationOptions.Default;

        var registry = ServiceRegistry.Build(services);
        var issues = new List<RelayValidationIssue>();

        foreach (var descriptor in services)
        {
            var implementationType = GetImplementationType(descriptor);
            if (implementationType is null || implementationType.ContainsGenericParameters)
            {
                // Factory/instance registrations and open generics carry no inspectable constructor.
                continue;
            }

            if (IsIgnored(descriptor.ServiceType, options))
            {
                continue;
            }

            InspectConstructor(descriptor, implementationType, registry, options, issues);
        }

        if (options.CheckDuplicateRegistrations)
        {
            AddDuplicateIssues(services, options, issues);
        }

        return new RelayValidationResult(issues);
    }

    /// <summary>
    /// Runs <see cref="ValidateRelayRegistrations"/> and throws a
    /// <see cref="RelayValidationException"/> when an error-severity issue is found.
    /// </summary>
    public static IServiceCollection ValidateRelayRegistrationsOrThrow(
        this IServiceCollection services,
        RelayValidationOptions? options = null
    )
    {
        services.ValidateRelayRegistrations(options).ThrowIfInvalid();
        return services;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "Validation is a development-time diagnostic and is not expected to run "
            + "in a trimmed application."
    )]
    private static void InspectConstructor(
        ServiceDescriptor descriptor,
        Type implementationType,
        ServiceRegistry registry,
        RelayValidationOptions options,
        List<RelayValidationIssue> issues
    )
    {
        var constructor = SelectConstructor(implementationType, registry, options);
        if (constructor is null)
        {
            return;
        }

        foreach (var parameter in constructor.GetParameters())
        {
            var key = parameter.GetCustomAttribute<FromKeyedServicesAttribute>()?.Key;
            var dependencyType = Unwrap(parameter.ParameterType, out var isCollection);

            if (
                IsIgnored(dependencyType, options)
                || AlwaysAvailable.Contains(dependencyType)
                || dependencyType == typeof(IServiceProvider)
            )
            {
                continue;
            }

            var lifetimes = registry.LifetimesOf(dependencyType, key);

            if (lifetimes.Count == 0)
            {
                if (
                    options.CheckMissingRegistrations
                    && !isCollection
                    && !parameter.HasDefaultValue
                    && !(options.IgnoreMissingFrameworkServices && IsFrameworkType(dependencyType))
                )
                {
                    issues.Add(
                        new RelayValidationIssue
                        {
                            Kind = RelayValidationIssueKind.MissingRegistration,
                            Severity = RelayValidationSeverity.Error,
                            ServiceType = descriptor.ServiceType,
                            ImplementationType = implementationType,
                            DependencyType = dependencyType,
                            ServiceLifetime = descriptor.Lifetime,
                            Message =
                                $"{implementationType.Name} (registered as {descriptor.ServiceType.Name}) "
                                + $"requires {Describe(dependencyType, key)}, which is not registered.",
                        }
                    );
                }

                continue;
            }

            if (
                options.CheckCaptiveDependencies
                && descriptor.Lifetime == ServiceLifetime.Singleton
                && lifetimes.Contains(ServiceLifetime.Scoped)
            )
            {
                issues.Add(
                    new RelayValidationIssue
                    {
                        Kind = RelayValidationIssueKind.CaptiveDependency,
                        Severity = RelayValidationSeverity.Error,
                        ServiceType = descriptor.ServiceType,
                        ImplementationType = implementationType,
                        DependencyType = dependencyType,
                        ServiceLifetime = ServiceLifetime.Singleton,
                        DependencyLifetime = ServiceLifetime.Scoped,
                        Message =
                            $"Singleton {implementationType.Name} (registered as {descriptor.ServiceType.Name}) "
                            + $"captures scoped {Describe(dependencyType, key)}. The scoped instance would live "
                            + "for the lifetime of the application. Inject IRelayScopeRunner (or IServiceScopeFactory) "
                            + "and create a scope per unit of work instead.",
                    }
                );
            }

            if (
                options.CheckDisposableTransients
                && descriptor.Lifetime == ServiceLifetime.Singleton
                && lifetimes.Contains(ServiceLifetime.Transient)
                && registry.HasDisposableImplementation(dependencyType, key)
            )
            {
                issues.Add(
                    new RelayValidationIssue
                    {
                        Kind = RelayValidationIssueKind.DisposableTransientCapture,
                        Severity = RelayValidationSeverity.Warning,
                        ServiceType = descriptor.ServiceType,
                        ImplementationType = implementationType,
                        DependencyType = dependencyType,
                        ServiceLifetime = ServiceLifetime.Singleton,
                        DependencyLifetime = ServiceLifetime.Transient,
                        Message =
                            $"Singleton {implementationType.Name} depends on transient disposable "
                            + $"{Describe(dependencyType, key)}. The container keeps every instance alive "
                            + "until the root provider is disposed.",
                    }
                );
            }
        }
    }

    private static void AddDuplicateIssues(
        IServiceCollection services,
        RelayValidationOptions options,
        List<RelayValidationIssue> issues
    )
    {
        var duplicates = services
            .Where(d => !d.IsKeyedService && d.ImplementationType is not null)
            .Where(d => !IsIgnored(d.ServiceType, options))
            .GroupBy(d => (d.ServiceType, d.ImplementationType))
            .Where(g => g.Count() > 1);

        foreach (var group in duplicates)
        {
            issues.Add(
                new RelayValidationIssue
                {
                    Kind = RelayValidationIssueKind.DuplicateRegistration,
                    Severity = RelayValidationSeverity.Warning,
                    ServiceType = group.Key.ServiceType,
                    ImplementationType = group.Key.ImplementationType,
                    Message =
                        $"{group.Key.ServiceType.Name} -> {group.Key.ImplementationType!.Name} is registered "
                        + $"{group.Count()} times. Only the last registration wins for a single resolution, "
                        + "but every one of them is returned by IEnumerable<T>.",
                }
            );
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "Validation is a development-time diagnostic and is not expected to run "
            + "in a trimmed application."
    )]
    private static ConstructorInfo? SelectConstructor(
        Type implementationType,
        ServiceRegistry registry,
        RelayValidationOptions options
    )
    {
        var constructors = implementationType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        if (constructors.Count == 0)
        {
            return null;
        }

        // Mirror the container: the greediest constructor whose parameters can all be satisfied.
        // If none can, fall back to the greediest one so the missing dependency gets reported.
        return constructors.FirstOrDefault(c =>
                c.GetParameters().All(p => CanSatisfy(p, registry, options))
            ) ?? constructors[0];
    }

    private static bool CanSatisfy(
        ParameterInfo parameter,
        ServiceRegistry registry,
        RelayValidationOptions options
    )
    {
        if (parameter.HasDefaultValue)
        {
            return true;
        }

        var dependencyType = Unwrap(parameter.ParameterType, out var isCollection);
        if (isCollection || AlwaysAvailable.Contains(dependencyType))
        {
            return true;
        }

        if (IsIgnored(dependencyType, options))
        {
            return true;
        }

        var key = parameter.GetCustomAttribute<FromKeyedServicesAttribute>()?.Key;
        return registry.LifetimesOf(dependencyType, key).Count > 0;
    }

    /// <summary>
    /// Reduces <c>IEnumerable&lt;T&gt;</c>, <c>Lazy&lt;T&gt;</c> and <c>Func&lt;T&gt;</c>
    /// parameters to the service actually being asked for.
    /// </summary>
    private static Type Unwrap(Type parameterType, out bool isCollection)
    {
        isCollection = false;
        var current = parameterType;

        while (current.IsGenericType)
        {
            var definition = current.GetGenericTypeDefinition();

            if (
                definition == typeof(IEnumerable<>)
                || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(IReadOnlyList<>)
            )
            {
                isCollection = true;
                current = current.GetGenericArguments()[0];
                continue;
            }

            if (definition == typeof(Lazy<>) || definition == typeof(Func<>))
            {
                current = current.GetGenericArguments()[0];
                continue;
            }

            break;
        }

        if (current.IsArray && current.GetElementType() is { } elementType)
        {
            isCollection = true;
            current = elementType;
        }

        return current;
    }

    private static bool IsIgnored(Type type, RelayValidationOptions options) =>
        options.IgnoredServiceTypes.Contains(type) || options.Ignore?.Invoke(type) == true;

    private static bool IsFrameworkType(Type type) =>
        type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true
        || type.Namespace?.StartsWith("Microsoft", StringComparison.Ordinal) == true;

    private static string Describe(Type type, object? key) =>
        key is null ? type.Name : $"{type.Name} (key '{key}')";

    private static Type? GetImplementationType(ServiceDescriptor descriptor) =>
        descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;

    /// <summary>
    /// Lifetime lookup over the descriptors, keyed registrations and open generics included.
    /// </summary>
    private sealed class ServiceRegistry
    {
        private readonly Dictionary<(Type Type, object? Key), List<ServiceDescriptor>> _byService =
            new();

        internal static ServiceRegistry Build(IServiceCollection services)
        {
            var registry = new ServiceRegistry();
            foreach (var descriptor in services)
            {
                var key = descriptor.IsKeyedService ? descriptor.ServiceKey : null;
                var lookupKey = (descriptor.ServiceType, key);
                if (!registry._byService.TryGetValue(lookupKey, out var list))
                {
                    list = [];
                    registry._byService[lookupKey] = list;
                }

                list.Add(descriptor);
            }

            return registry;
        }

        internal IReadOnlyCollection<ServiceLifetime> LifetimesOf(Type serviceType, object? key)
        {
            var descriptors = Descriptors(serviceType, key);
            return descriptors.Count == 0
                ? []
                : descriptors.Select(d => d.Lifetime).Distinct().ToList();
        }

        internal bool HasDisposableImplementation(Type serviceType, object? key) =>
            Descriptors(serviceType, key)
                .Select(d => d.IsKeyedService ? d.KeyedImplementationType : d.ImplementationType)
                .Any(t =>
                    t is not null
                    && (
                        typeof(IDisposable).IsAssignableFrom(t)
                        || typeof(IAsyncDisposable).IsAssignableFrom(t)
                    )
                );

        private List<ServiceDescriptor> Descriptors(Type serviceType, object? key)
        {
            if (_byService.TryGetValue((serviceType, key), out var exact))
            {
                return exact;
            }

            // A keyed dependency also resolves through KeyedService.AnyKey registrations.
            if (
                key is not null
                && _byService.TryGetValue((serviceType, KeyedService.AnyKey), out var anyKey)
            )
            {
                return anyKey;
            }

            // Closed generics are satisfied by an open generic registration.
            if (
                serviceType.IsGenericType
                && _byService.TryGetValue(
                    (serviceType.GetGenericTypeDefinition(), key),
                    out var openGeneric
                )
            )
            {
                return openGeneric;
            }

            return [];
        }
    }
}
