namespace Relay.Core.Interfaces;

/// <summary>
/// Provides ambient access to the <see cref="IRelayContext"/> for the current resolution.
/// Set by <see cref="IRelayResolver"/> when a caller passes an explicit context so that
/// conditional relays and factories can route based on it.
/// </summary>
public interface IRelayContextAccessor
{
    /// <summary>
    /// The context for the current resolution, or <c>null</c> when none has been set.
    /// </summary>
    IRelayContext? Current { get; set; }
}
