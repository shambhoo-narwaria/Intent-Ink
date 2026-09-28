namespace IntentInk.Core.Services;

/// <summary>
/// Produces a minimal word-level diff between original and corrected text.
/// Used to highlight changes in the PreviewWindow.
/// </summary>
public sealed class DiffEngine
{
    public enum ChangeKind { Equal, Delete, Insert }

    public sealed record DiffSegment(ChangeKind Kind, string Text);

    /// <summary>
    /// Returns a list of segments describing how <paramref name="original"/>
    /// becomes <paramref name="corrected"/>.
    /// </summary>
    public IReadOnlyList<DiffSegment> Compute(string original, string corrected)
    {
        var origWords    = Tokenize(original);
        var corrWords    = Tokenize(corrected);
        var lcs          = LcsMatrix(origWords, corrWords);
        var segments     = new List<DiffSegment>();

        BuildSegments(lcs, origWords, corrWords,
                      origWords.Count, corrWords.Count, segments);

        return segments;
    }

    private static List<string> Tokenize(string text)
    {
        // Split on whitespace boundaries, keeping the whitespace as part of the token
        var tokens = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            int start = i;
            if (char.IsWhiteSpace(text[i]))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            }
            else
            {
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            }
            tokens.Add(text[start..i]);
        }
        return tokens;
    }

    private static int[,] LcsMatrix(List<string> a, List<string> b)
    {
        int m = a.Count, n = b.Count;
        int[,] dp = new int[m + 1, n + 1];
        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);
        return dp;
    }

    private static void BuildSegments(int[,] dp, List<string> a, List<string> b,
                                      int i, int j, List<DiffSegment> out_)
    {
        if (i == 0 && j == 0) return;
        if (i > 0 && j > 0 && a[i - 1] == b[j - 1])
        {
            BuildSegments(dp, a, b, i - 1, j - 1, out_);
            out_.Add(new DiffSegment(ChangeKind.Equal, a[i - 1]));
        }
        else if (j > 0 && (i == 0 || dp[i, j - 1] >= dp[i - 1, j]))
        {
            BuildSegments(dp, a, b, i, j - 1, out_);
            out_.Add(new DiffSegment(ChangeKind.Insert, b[j - 1]));
        }
        else
        {
            BuildSegments(dp, a, b, i - 1, j, out_);
            out_.Add(new DiffSegment(ChangeKind.Delete, a[i - 1]));
        }
    }
}
