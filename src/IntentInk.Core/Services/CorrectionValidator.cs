using IntentInk.Core.Models;
using System.Text.RegularExpressions;

namespace IntentInk.Core.Services;

/// <summary>
/// Validates a raw model correction before it is shown to the user.
/// All checks are deterministic and do not call the model again.
/// </summary>
public sealed class CorrectionValidator
{
    private const int MaxOutputLength = 3000;
    private const double MaxEditRatio  = 0.8;  // suppress if > 80% of chars changed

    // Protected token patterns: URLs, emails, version numbers
    private static readonly Regex[] ProtectedPatterns =
    [
        new(@"https?://\S+",        RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b[\w.+-]+@[\w-]+\.\w{2,}\b", RegexOptions.Compiled),
        new(@"\bv?\d+\.\d+(\.\d+)*\b",      RegexOptions.Compiled),
    ];

    /// <summary>
    /// Returns a validated <see cref="CorrectionCandidate"/> or null to suppress the result.
    /// </summary>
    public CorrectionCandidate? Validate(EditorSnapshot snapshot, string rawCorrected)
    {
        if (string.IsNullOrWhiteSpace(rawCorrected)) return null;

        if (rawCorrected.Length > MaxOutputLength) return null;

        // Reject if no change
        if (rawCorrected == snapshot.OriginalText) return null;

        // Edit ratio guard
        int distance = LevenshteinDistance(snapshot.OriginalText, rawCorrected);
        double ratio  = (double)distance / Math.Max(snapshot.OriginalText.Length, 1);
        if (ratio > MaxEditRatio) return null;

        // Protected token guard
        foreach (var pattern in ProtectedPatterns)
        {
            var originalTokens = pattern.Matches(snapshot.OriginalText).Select(m => m.Value).ToList();
            var correctedTokens = pattern.Matches(rawCorrected).Select(m => m.Value).ToList();
            if (!originalTokens.SequenceEqual(correctedTokens)) return null;
        }

        return new CorrectionCandidate
        {
            Source        = snapshot,
            CorrectedText = rawCorrected,
        };
    }

    // Simple Levenshtein distance (bounded for performance)
    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length > 2000 || b.Length > 2000) return int.MaxValue;
        int[,] dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                dp[i, j] = a[i - 1] == b[j - 1] ? dp[i - 1, j - 1] : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
        return dp[a.Length, b.Length];
    }
}
