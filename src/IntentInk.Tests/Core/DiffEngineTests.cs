using IntentInk.Core.Services;
using Xunit;

namespace IntentInk.Tests.Core;

public class DiffEngineTests
{
    private readonly DiffEngine _diff = new();

    [Fact]
    public void Compute_EqualStrings_ProducesSingleEqualSegment()
    {
        var segments = _diff.Compute("hello world", "hello world");
        Assert.All(segments, s => Assert.Equal(DiffEngine.ChangeKind.Equal, s.Kind));
        var reconstructed = string.Concat(segments.Select(s => s.Text));
        Assert.Equal("hello world", reconstructed);
    }

    [Fact]
    public void Compute_Insertion_IdentifiesInsertedTokens()
    {
        var segments = _diff.Compute("he to office", "he goes to office");
        var inserts = segments.Where(s => s.Kind == DiffEngine.ChangeKind.Insert).ToList();
        Assert.NotEmpty(inserts);
        Assert.Contains(inserts, s => s.Text.Contains("goes"));
    }

    [Fact]
    public void Compute_Deletion_IdentifiesDeletedTokens()
    {
        var segments = _diff.Compute("he goes to the the office", "he goes to the office");
        var deletions = segments.Where(s => s.Kind == DiffEngine.ChangeKind.Delete).ToList();
        Assert.NotEmpty(deletions);
        Assert.Contains(deletions, s => s.Text.Contains("the"));
    }

    [Fact]
    public void Compute_Replacement_HasDeleteAndInsert()
    {
        var segments = _diff.Compute("he go home", "he goes home");
        Assert.Contains(segments, s => s.Kind == DiffEngine.ChangeKind.Delete && s.Text == "go");
        Assert.Contains(segments, s => s.Kind == DiffEngine.ChangeKind.Insert && s.Text == "goes");
        Assert.Contains(segments, s => s.Kind == DiffEngine.ChangeKind.Equal && s.Text == "he");
        Assert.Contains(segments, s => s.Kind == DiffEngine.ChangeKind.Equal && s.Text == "home");
    }
}
