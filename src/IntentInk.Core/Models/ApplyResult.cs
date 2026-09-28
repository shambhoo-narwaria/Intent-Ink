namespace IntentInk.Core.Models;

/// <summary>
/// Outcome of attempting to write a correction back to the target editor.
/// </summary>
public sealed record ApplyResult
{
    public required bool Success { get; init; }

    /// <summary>Human-readable reason for failure (if !Success).</summary>
    public string? FailureReason { get; init; }

    public static ApplyResult Ok() => new() { Success = true };

    public static ApplyResult Fail(string reason) => new() { Success = false, FailureReason = reason };
}
