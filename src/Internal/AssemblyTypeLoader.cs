using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Relay.Internal;

/// <summary>
/// Assembly scanning helpers.
/// </summary>
internal static class AssemblyTypeLoader
{
    /// <summary>
    /// Returns the types that could be loaded from <paramref name="assembly"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Assembly.GetTypes"/> throws <see cref="ReflectionTypeLoadException"/> as soon as
    /// a single type references a missing dependency, which takes down startup for a plugin or an
    /// optional package that is not installed. Scanning should degrade to the types that did load.
    /// </remarks>
    [RequiresUnreferencedCode("Assembly scanning is not compatible with trimming.")]
    internal static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
