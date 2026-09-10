using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Spelling a value onto a drawn keyboard by voice (#51).</summary>
public class SayEachLetterAsItsWordTests
{
    /// <summary>Every word in the table presses the key it names, and nothing else.</summary>
    [Fact]
    public void EveryDeclaredWordPressesTheKeyItNames()
    {
        foreach (var (character, word) in Spelling.Alphabet)
        {
            Assert.Equal([SpelledKey.Types(character)], Spelling.Parse(word).Keys);
        }

        foreach (var (character, word) in Spelling.Figures)
        {
            Assert.Equal([SpelledKey.Types(character)], Spelling.Parse(word).Keys);
        }

        foreach (var (character, word) in Spelling.Marks)
        {
            Assert.Equal([SpelledKey.Types(character)], Spelling.Parse(word).Keys);
        }

        foreach (var (press, word) in Spelling.Commands)
        {
            Assert.Equal([new SpelledKey(press)], Spelling.Parse(word).Keys);
        }
    }

    /// <summary>Every character key has a word, and every word a key.</summary>
    [Fact]
    public void TheVocabularyCoversEveryCharacterItCanPress()
    {
        foreach (var character in Spelling.Characters)
        {
            var word = Spelling.Alphabet.GetValueOrDefault(character)
                ?? Spelling.Figures.GetValueOrDefault(character)
                ?? Spelling.Marks.GetValueOrDefault(character);

            Assert.False(word is null, $"There is no word for the key '{character}'.");
            Assert.Equal([SpelledKey.Types(character)], Spelling.Parse(word).Keys);
        }

        Assert.Equal(Spelling.Characters.Count, Spelling.Characters.Distinct().Count());
    }

    /// <summary>Ordinary English is not spelling, however plausible the word.</summary>
    [Theory]
    [InlineData("centauri")]
    [InlineData("shinrarta")]
    [InlineData("the")]
    [InlineData("a")]
    [InlineData("hyphen")]
    [InlineData("period")]
    [InlineData("backspace")]
    [InlineData("enter")]
    public void AWordOffTheTableIsRefused(string word)
    {
        var spelled = Spelling.Parse(word);

        Assert.False(spelled.IsSpelling);
        Assert.Equal(word, spelled.Refused);
    }

    /// <summary>A run, in order, with the figure said as a word.</summary>
    [Fact]
    public void AWholeUtteranceBecomesAWholeRunOfPresses()
    {
        Assert.Equal(
            [
                SpelledKey.Types('a'),
                SpelledKey.Types('b'),
                SpelledKey.Types('7'),
                new SpelledKey(SpelledPress.Done),
            ],
            Spelling.Parse("alpha bravo seven done").Keys);
    }

    /// <summary>Punctuation is Whisper's, not the Commander's.</summary>
    [Theory]
    [InlineData("Alpha, bravo, seven.")]
    [InlineData("alpha bravo 7")]
    [InlineData("AB7")]
    public void TheSameValueArrivesHoweverWhisperWroteIt(string said)
    {
        Assert.Equal(
            [SpelledKey.Types('a'), SpelledKey.Types('b'), SpelledKey.Types('7')],
            Spelling.Parse(said).Keys);
    }

    /// <summary>Whole utterance or nothing: one word off the table presses nothing at all.</summary>
    [Fact]
    public void OneUnknownWordPressesNothingAndNamesTheWord()
    {
        var spelled = Spelling.Parse("alpha centauri");

        Assert.Empty(spelled.Keys);
        Assert.Equal("centauri", spelled.Refused);
        Assert.Contains("centauri", Spelling.Refusal(spelled.Refused!), StringComparison.Ordinal);
    }

    /// <summary>Below the bar nothing is pressed, and the board says so.</summary>
    [Fact]
    public void AnUtteranceHeardBadlyPressesNothing()
    {
        var read = Spelling.Hear(new Heard("alpha bravo", TextEntryLoop.ConfidentEnough - 0.01, Final: true));

        Assert.Equal(SpelledOutcome.NotCaught, read.Outcome);
        Assert.Empty(read.Keys);
        Assert.False(string.IsNullOrWhiteSpace(read.Say));
    }

    /// <summary>A partial is shown and never pressed.</summary>
    [Fact]
    public void APartialIsNeverPressed()
    {
        var read = Spelling.Hear(new Heard("alpha", 1, Final: false));

        Assert.Equal(SpelledOutcome.Waiting, read.Outcome);
        Assert.Empty(read.Keys);
    }

    /// <summary>Anything that is not a spelling is a value.</summary>
    [Fact]
    public void AnUtteranceThatIsNotSpellingIsDictated()
    {
        var read = Spelling.Hear(new Heard("Shinrarta Dezhra", 1, Final: true));

        Assert.Equal(SpelledOutcome.Dictation, read.Outcome);
        Assert.Equal("Shinrarta Dezhra", read.Text);
        Assert.Contains("Shinrarta", read.Say!, StringComparison.Ordinal);
    }

    /// <summary>A value that is itself one word of the alphabet is spelled, which the help page says.</summary>
    [Fact]
    public void ASystemCalledDeltaIsSpelledRatherThanDictated()
    {
        Assert.Equal([SpelledKey.Types('d')], Spelling.Parse("delta").Keys);
    }

    /// <summary>The tutor, from the parser's own table.</summary>
    [Fact]
    public void TheTutorTeachesTheShapeTheWholeAlphabetAndOneLetter()
    {
        Assert.Equal(Spelling.Shape, Spelling.Tutor(null));
        Assert.Equal("K is kilo.", Spelling.Tutor("k"));
        Assert.Equal("K is kilo.", Spelling.Tutor("K"));

        // The words alone, one to a line, rather than each one preceded by the letter it stands for.
        var whole = Spelling.Whole();

        foreach (var word in Spelling.Alphabet.Values)
        {
            Assert.Contains(
                $"- {char.ToUpperInvariant(word[0])}{word[1..]}",
                whole,
                StringComparison.Ordinal);
        }

        Assert.DoesNotContain(" is ", whole, StringComparison.Ordinal);
    }

    /// <summary>And it is the router that answers, so no model is asked what a letter is called.</summary>
    [Theory]
    [InlineData("what is the word for K", "kilo")]
    [InlineData("what's the word for z", "zulu")]
    public async Task AskingForOneLetterIsAnsweredWithoutAModel(string asked, string expected)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var match = new KeywordRouter(surface.Registry).Match(asked, InputSource.Spoken);

        Assert.NotNull(match);
        Assert.Equal(ListeningCapability.AlphabetTool, match!.ToolName);

        var answered = await surface.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.Contains(expected, answered.Content, StringComparison.Ordinal);
    }

    /// <summary>And asking for the whole of it gets every letter.</summary>
    [Fact]
    public async Task AskingForThePhoneticAlphabetGetsEveryLetter()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var match = new KeywordRouter(surface.Registry).Match("the phonetic alphabet", InputSource.Spoken);

        Assert.NotNull(match);

        var answered = await surface.Registry.InvokeAsync(
            match!.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        foreach (var word in Spelling.Alphabet.Values)
        {
            Assert.Contains(word, answered.Content, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Asking how to spell at all teaches the shape rather than the whole table.</summary>
    [Fact]
    public async Task AskingHowToSpellTeachesTheShape()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var match = new KeywordRouter(surface.Registry).Match("how do i spell something", InputSource.Spoken);

        Assert.NotNull(match);
        Assert.Equal(ListeningCapability.AlphabetTool, match!.ToolName);
        Assert.Empty(match.Arguments.Values);

        var answered = await surface.Registry.InvokeAsync(
            match.ToolName, ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(Spelling.Shape, answered.Content);
    }
}
