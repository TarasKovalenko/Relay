namespace Relay.Core.Enums;

/// <summary>
/// Strategy for handling chain failures
/// </summary>
public enum ChainFailureStrategy
{
    /// <summary>Stop on first failure</summary>
    StopOnFirstFailure,
    /// <summary>Continue despite failures</summary>
    ContinueOnFailure,
    /// <summary>Retry on failure</summary>
    RetryOnFailure
}
