namespace IntentInk.Core.Models;

/// <summary>
/// A validated grammar correction ready to display in the overlay.
/// Tied to the source text that produced it.
/// </summary>
public sealed record CorrectionCandidate
{
    /// <summary>The snapshot this result belongs to.</summary>
    public required EditorSnapshot Source { get; init; }

    /// <summary>The corrected text from the model.</summary>
    public required string CorrectedText { get; init; }
}
