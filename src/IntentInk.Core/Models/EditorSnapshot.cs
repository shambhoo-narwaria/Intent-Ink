namespace IntentInk.Core.Models;

/// <summary>
/// Immutable capture of selected text.
/// Passed across thread boundaries; never mutate after creation.
/// </summary>
public sealed record EditorSnapshot
{
    /// <summary>The captured selected text.</summary>
    public required string OriginalText { get; init; }
}

/// <summary>A DPI-unaware bounding rectangle in screen coordinates.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public static readonly ScreenRect Empty = new(0, 0, 0, 0);
}
