using System.Text;

namespace Relay.Validation;

/// <summary>
/// The outcome of validating a service collection.
/// </summary>
public sealed class RelayValidationResult(IReadOnlyList<RelayValidationIssue> issues)
{
    /// <summary>Every issue that was found, warnings included.</summary>
    public IReadOnlyList<RelayValidationIssue> Issues { get; } =
        issues ?? throw new ArgumentNullException(nameof(issues));

    /// <summary>Issues with <see cref="RelayValidationSeverity.Error"/> severity.</summary>
    public IEnumerable<RelayValidationIssue> Errors =>
        Issues.Where(i => i.Severity == RelayValidationSeverity.Error);

    /// <summary>Issues with <see cref="RelayValidationSeverity.Warning"/> severity.</summary>
    public IEnumerable<RelayValidationIssue> Warnings =>
        Issues.Where(i => i.Severity == RelayValidationSeverity.Warning);

    /// <summary><c>true</c> when no error-severity issue was found.</summary>
    public bool IsValid => !Errors.Any();

    /// <summary>
    /// Throws a <see cref="RelayValidationException"/> when <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public RelayValidationResult ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new RelayValidationException(this);
        }

        return this;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Issues.Count == 0)
        {
            return "No dependency injection issues found.";
        }

        var builder = new StringBuilder();
        builder.Append(Issues.Count).AppendLine(" dependency injection issue(s) found:");
        foreach (var issue in Issues)
        {
            builder.Append(" - ").AppendLine(issue.ToString());
        }

        return builder.ToString();
    }
}

/// <summary>
/// Thrown when a service collection fails validation.
/// </summary>
public sealed class RelayValidationException : InvalidOperationException
{
    /// <summary>Creates an exception carrying the failed <paramref name="result"/>.</summary>
    public RelayValidationException(RelayValidationResult result)
        : base(result?.ToString() ?? "Service collection validation failed.")
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>Creates an exception with a message and an empty result.</summary>
    public RelayValidationException(string message)
        : base(message)
    {
        Result = new RelayValidationResult([]);
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public RelayValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Result = new RelayValidationResult([]);
    }

    /// <summary>The validation result that caused the failure.</summary>
    public RelayValidationResult Result { get; }
}
