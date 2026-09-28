using IntentInk.Core.Models;
using IntentInk.Core.Services;
using Xunit;

namespace IntentInk.Tests.Core;

public class CorrectionValidatorTests
{
    private readonly CorrectionValidator _validator = new();

    private static EditorSnapshot CreateSnapshot(string text) => new()
    {
        OriginalText = text,
    };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public void Validate_RejectsEmptyOrWhitespace(string raw)
    {
        var snapshot = CreateSnapshot("hello world");
        var result = _validator.Validate(snapshot, raw);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsIdenticalText()
    {
        var snapshot = CreateSnapshot("This sentence is already correct.");
        var result = _validator.Validate(snapshot, "This sentence is already correct.");
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsOversizedOutput()
    {
        var snapshot = CreateSnapshot("Short input.");
        var hugeOutput = new string('a', 3001);
        var result = _validator.Validate(snapshot, hugeOutput);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsExcessiveEditRatio()
    {
        // 80%+ changed
        var snapshot = CreateSnapshot("the cat sat on the mat");
        var completelyDifferent = "completely different unrelated words here now";
        var result = _validator.Validate(snapshot, completelyDifferent);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsAlteredUrl()
    {
        var snapshot = CreateSnapshot("Check https://example.com/api/v1 for details.");
        var alteredUrl = "Check https://malicious.com/api/v1 for details.";
        var result = _validator.Validate(snapshot, alteredUrl);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsAlteredEmail()
    {
        var snapshot = CreateSnapshot("Send it to john.doe@example.com please.");
        var alteredEmail = "Send it to jane.doe@example.com please.";
        var result = _validator.Validate(snapshot, alteredEmail);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsAlteredVersion()
    {
        var snapshot = CreateSnapshot("Deploy v1.2.3 to production.");
        var alteredVersion = "Deploy v2.0.0 to production.";
        var result = _validator.Validate(snapshot, alteredVersion);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_AcceptsValidGrammarCorrection()
    {
        var snapshot = CreateSnapshot("he go to office yesterday");
        var corrected = "he went to the office yesterday";
        var result = _validator.Validate(snapshot, corrected);

        Assert.NotNull(result);
        Assert.Equal(corrected, result.CorrectedText);
        Assert.Equal(snapshot, result.Source);
    }

    [Fact]
    public void Validate_AcceptsPreservedProtectedTokens()
    {
        var snapshot = CreateSnapshot("please review https://github.com/test and v1.4.2 docs");
        var corrected = "Please review https://github.com/test and v1.4.2 docs.";
        var result = _validator.Validate(snapshot, corrected);

        Assert.NotNull(result);
        Assert.Equal(corrected, result.CorrectedText);
    }
}
