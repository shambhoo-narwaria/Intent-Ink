using IntentInk.Core.Models;

namespace IntentInk.Core.Services.Interfaces;

/// <summary>
/// Grammar correction back-end contract.
/// Implemented by OllamaClient (local) or any future remote backend.
/// Both implementations return the same CorrectionCandidate type.
/// </summary>
public interface IGrammarClient
{
    /// <summary>
    /// Request a grammar correction for the given snapshot.
    /// Returns null when the model output is unchanged or must be suppressed.
    /// Throws on timeout, invalid JSON, or length-exceeded response.
    /// </summary>
    Task<CorrectionCandidate?> CorrectAsync(EditorSnapshot snapshot, CancellationToken ct);
}
