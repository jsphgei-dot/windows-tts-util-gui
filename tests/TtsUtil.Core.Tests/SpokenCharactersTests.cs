using TtsUtil.Core.Settings;
using TtsUtil.Core.Text;
using Xunit;

namespace TtsUtil.Core.Tests;

public sealed class SpokenCharactersTests
{
    private static SpokenCharacterOptions Latin(string extra = "") =>
        new() { Policy = SpokenCharacterPolicy.LatinOnly, AllowedExtra = extra };

    [Fact]
    public void TheDefaultPolicyIsTheStrictOne()
    {
        Assert.Equal(SpokenCharacterPolicy.LatinOnly, new AppSettings().SpokenCharacters);
    }

    [Theory]
    [InlineData("Read this #hashtag", "Read this hashtag")]
    [InlineData("Costs $4 or 50% more", "Costs 4 or 50 more")]
    [InlineData("Email a@b and c&d", "Email a b and c d")]
    [InlineData("A (parenthetical) aside", "A parenthetical aside")]
    [InlineData("Use the ~tilde~ and _underscore_", "Use the tilde and underscore")]
    public void SymbolsTheEngineWouldNameAreDropped(string input, string expected)
    {
        Assert.Equal(expected, SpokenCharacters.Clean(input, Latin()));
    }

    [Theory]
    [InlineData("Hello there, friend.")]
    [InlineData("Really? Yes! Listen; then stop: done.")]
    public void PunctuationThatPhrasesSpeechIsKept(string input)
    {
        Assert.Equal(input, SpokenCharacters.Clean(input, Latin()));
    }

    [Fact]
    public void DigitsAreKeptSoYearsAndPricesStillRead()
    {
        Assert.Equal("In 1999 he paid 4 50", SpokenCharacters.Clean("In 1999 he paid $4 50", Latin()));
    }

    [Theory]
    [InlineData("don't", "dont")]
    [InlineData("Smith’s book", "Smiths book")]
    [InlineData("'quoted'", "quoted")]
    public void ApostrophesInsideAWordVanishRatherThanSplittingIt(string input, string expected)
    {
        Assert.Equal(expected, SpokenCharacters.Clean(input, Latin()));
    }

    [Fact]
    public void ADashBetweenWordsBecomesAGapNotAJoin()
    {
        Assert.Equal("well known", SpokenCharacters.Clean("well-known", Latin()));
        Assert.Equal("one two", SpokenCharacters.Clean("one—two", Latin()));
    }

    [Fact]
    public void RunsOfSymbolsCollapseToASingleGap()
    {
        Assert.Equal("a b", SpokenCharacters.Clean("a ***>>> b", Latin()));
    }

    [Fact]
    public void LeadingAndTrailingSymbolsLeaveNoStrayGap()
    {
        Assert.Equal("hello", SpokenCharacters.Clean("***hello***", Latin()));
    }

    [Fact]
    public void ExtraAllowedCharactersComeThrough()
    {
        Assert.Equal("50% of $4", SpokenCharacters.Clean("50% of $4", Latin("%$")));
    }

    [Fact]
    public void TheStrictPolicyFoldsAccentsAndDropsOtherScripts()
    {
        Assert.Equal("cafe naive", SpokenCharacters.Clean("café naïve", Latin()));
        Assert.Equal(string.Empty, SpokenCharacters.Clean("你好世界", Latin()));
    }

    [Fact]
    public void TheAnyLetterPolicyKeepsAccentsAndOtherScripts()
    {
        var options = new SpokenCharacterOptions { Policy = SpokenCharacterPolicy.AnyLetter };

        Assert.Equal("café naïve", SpokenCharacters.Clean("café naïve", options));
        Assert.Equal("你好世界", SpokenCharacters.Clean("你好世界", options));
        Assert.Equal("costs 4", SpokenCharacters.Clean("costs $4", options));
    }

    [Fact]
    public void TurningThePolicyOffLeavesTheTextExactlyAsItWas()
    {
        const string text = "Costs $4 (50%) — café 你好 #tag";
        var options = new SpokenCharacterOptions { Policy = SpokenCharacterPolicy.Off };

        Assert.Equal(text, SpokenCharacters.Clean(text, options));
    }

    [Fact]
    public void TheChunkerAppliesThePolicyAndCountsWhatItRemoved()
    {
        var options = new ChunkerOptions();
        options.Spoken.Policy = SpokenCharacterPolicy.LatinOnly;

        var chunker = new TextChunker(options);
        var chunks = chunker.Read("Pay $40 (now)!").ToList();

        Assert.Equal("Pay 40 now!", chunks[0].Text);
        Assert.True(chunker.CharactersFiltered > 0);
    }

    [Fact]
    public void TheSpaceAfterAPausedSentenceIsNotCountedAsFiltered()
    {
        var options = new ChunkerOptions { Silence = { SentenceMs = 300 } };
        options.Spoken.Policy = SpokenCharacterPolicy.LatinOnly;

        var chunker = new TextChunker(options);
        var chunks = chunker.Read("The first sentence.  The second one.").ToList();

        Assert.Equal("The second one", chunks[1].Text);
        Assert.Equal(0, chunker.CharactersFiltered);
    }

    [Fact]
    public void AnUtteranceStrippedToNothingStillCarriesItsSilence()
    {
        var options = new ChunkerOptions { Silence = { SentenceMs = 300 } };
        options.Spoken.Policy = SpokenCharacterPolicy.LatinOnly;

        var chunks = new TextChunker(options).Read("$$$. Then words.").ToList();

        Assert.Equal(300, chunks[0].SilenceMs);
        Assert.Equal(string.Empty, chunks[0].Text);
    }

    [Fact]
    public void SettingsCarryThePolicyIntoTheChunkerOptions()
    {
        var settings = new AppSettings
        {
            SpokenCharacters = SpokenCharacterPolicy.AnyLetter,
            AllowedExtraCharacters = "%",
        };

        var options = settings.ToChunkerOptions();

        Assert.Equal(SpokenCharacterPolicy.AnyLetter, options.Spoken.Policy);
        Assert.Equal("%", options.Spoken.AllowedExtra);
    }
}
