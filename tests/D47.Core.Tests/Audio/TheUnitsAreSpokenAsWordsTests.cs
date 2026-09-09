using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Unit abbreviations written out before any provider sees them.</summary>
public class TheUnitsAreSpokenAsWordsTests
{
    /// <summary>What the provider was actually asked to say, which is the only thing being changed.</summary>
    private static async Task<string> SpokenAsync(string sentence)
    {
        var arbiter = new AudioArbiter(
            new RecordingAudioSink(), NullLogger<AudioArbiter>.Instance).Start();

        var tts = new FakeTtsProvider();

        await using (var pipeline = new SpeechPipeline(
            arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance))
        {
            pipeline.Push(sentence);
            await pipeline.CompleteAsync();
        }

        return Assert.Single(tts.Requested);
    }

    // ---- The reported sentence ----------------------------------------------------------------

    /// <summary>The line the Commander heard, as the provider now receives it.</summary>
    [Fact]
    public async Task TheReportedSentenceIsSpokenInWords()
    {
        var spoken = await SpokenAsync(
            "Nearest 5H Guardian FSD Booster: Perez Ring, LHS 2637 — 5.79 ly, 395 ls out, large pad.");

        Assert.Contains("5.79 light years", spoken, StringComparison.Ordinal);
        Assert.Contains("395 light seconds out", spoken, StringComparison.Ordinal);
    }

    /// <summary>And the transcript is untouched.</summary>
    [Fact]
    public async Task TheTranscriptStillShowsTheAbbreviations()
    {
        var log = new List<string>();
        var arbiter = new AudioArbiter(
            new RecordingAudioSink(), NullLogger<AudioArbiter>.Instance).Start();

        await using (var pipeline = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider(),
            VoiceSelection.Default,
            "turn-1",
            new Capture(log)))
        {
            pipeline.Push("Perez Ring is 5.79 ly out.");
            await pipeline.CompleteAsync();
        }

        var said = Assert.Single(log, line => line.Contains(" said: ", StringComparison.Ordinal));

        Assert.Contains("5.79 ly", said, StringComparison.Ordinal);
        Assert.DoesNotContain("light year", said, StringComparison.Ordinal);
    }

    // ---- Every unit in the table --------------------------------------------------------------

    /// <summary>The five units, each anchored to a number.</summary>
    [Theory]
    [InlineData("5.79 ly", "5.79 light years")]
    [InlineData("395 ls", "395 light seconds")]
    [InlineData("128 t", "128 tonnes")]
    [InlineData("6,680 t", "6,680 tonnes")]
    [InlineData("32 MW", "32 megawatts")]
    [InlineData("1,250,000 cr", "1,250,000 credits")]
    public void EveryUnitInTheTableIsSaid(string written, string expected) =>
        Assert.Equal(expected, SpokenUnits.Rewrite(written));

    /// <summary>Singular at exactly one, plural everywhere else including 1.0.</summary>
    [Theory]
    [InlineData("1 ly", "1 light year")]
    [InlineData("1 ls", "1 light second")]
    [InlineData("1 cr", "1 credit")]
    [InlineData("1.5 ly", "1.5 light years")]
    [InlineData("1.0 ly", "1.0 light years")]
    [InlineData("0.5 ly", "0.5 light years")]
    [InlineData("21 ly", "21 light years")]
    public void OneIsSingularAndEverythingElseIsNot(string written, string expected) =>
        Assert.Equal(expected, SpokenUnits.Rewrite(written));

    /// <summary>Case is not required to match, because the game writes <c>Ls</c> and <c>Cr</c>.</summary>
    [Theory]
    [InlineData("395 Ls", "395 light seconds")]
    [InlineData("395 LS", "395 light seconds")]
    [InlineData("500 Cr", "500 credits")]
    public void TheSpellingTheGameUsesIsAlsoRead(string written, string expected) =>
        Assert.Equal(expected, SpokenUnits.Rewrite(written));

    // ---- What must never be rewritten ---------------------------------------------------------

    /// <summary>A name is not a unit, and this is the assertion the whole design exists to pass.</summary>
    [Theory]
    [InlineData("LHS 2637 — Perez Ring, large pad.")]
    [InlineData("Lys, Lyra and Ly Puuni are systems.")]
    [InlineData("The 5 lyrics were wrong.")]
    [InlineData("Hold 128 tonnes.")]
    [InlineData("Trading at 500 credits.")]
    [InlineData("Shinrarta Dezhra, 2 jumps.")]
    [InlineData("Cobra Mk III")]
    public void ALineWithNoAnchoredUnitComesBackUnchanged(string line) =>
        Assert.Equal(line, SpokenUnits.Rewrite(line));

    /// <summary>The unit has to follow a number, and the number has to be a token of its own.</summary>
    [Theory]
    [InlineData("ly out")]
    [InlineData("Booster ly")]
    [InlineData("B0-GQPI ly")]
    [InlineData("A2637 ly")]
    public void AUnitWithNoNumberInFrontOfItIsNotAUnit(string line) =>
        Assert.Equal(line, SpokenUnits.Rewrite(line));

    // ---- Through the ladder, which is the other half of the sound -----------------------------

    [Fact]
    public void TheLocalVoiceSaysLightYearsRatherThanASyllable()
    {
        var rules = new Phonemiser(new ShippedWords());

        var before = rules.ToPhonemes("5.79 ly");
        var after = rules.ToPhonemes(SpokenUnits.Rewrite("5.79 ly"));

        // The defect, written down: "ly" is a sayable English syllable, so the ladder said it.
        Assert.EndsWith("laɪ", before, StringComparison.Ordinal);

        Assert.EndsWith("lˈaɪt jˈɪɹz", after, StringComparison.Ordinal);
    }

    /// <summary>
    /// The four words the rewrite introduces, with the readings the shipped dictionary gives them —
    /// read out of it on 2026-08-29 rather than written by ear, which is the rule the number-word table
    /// already follows.
    /// </summary>
    private sealed class ShippedWords : IPronunciationDictionary
    {
        private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
        {
            ["light"] = "lˈaɪt",
            ["year"] = "jˈɪɹ",
            ["years"] = "jˈɪɹz",
            ["second"] = "sˈɛkənd",
            ["seconds"] = "sˈɛkəndz",
        };

        public string? Lookup(string word) => Words.GetValueOrDefault(word);
    }

    private sealed class Capture(List<string> lines) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
