using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

/// <summary>
/// Scoped, mutable holder for the ambient <see cref="IRelayContext"/>.
/// </summary>
public sealed class RelayContextAccessor : IRelayContextAccessor
{
    /// <inheritdoc />
    public IRelayContext? Current { get; set; }
}
