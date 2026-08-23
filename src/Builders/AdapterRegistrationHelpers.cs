using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Relay.Builders;

/// <summary>
/// Shared registration helpers used by the adapter chain builders.
/// </summary>
internal static class AdapterRegistrationHelpers
{
    /// <summary>
    /// Registers an adapter type once. Chains frequently reuse the same adapter, and adding it
    /// repeatedly would create a duplicate descriptor for every step that mentions it.
    /// </summary>
    internal static void TryAddAdapter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAdapter
    >(this IServiceCollection services)
        where TAdapter : class
    {
        services.TryAddScoped<TAdapter>();
    }
}
