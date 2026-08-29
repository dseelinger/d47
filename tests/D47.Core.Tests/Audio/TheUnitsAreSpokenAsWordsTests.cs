using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Unit abbreviations written out before any provider sees them
/// (<a href="https://github.com/dseelinger/d47/issues/155">#155</a>).
/// <para>
/// <b>Reported against ElevenLabs on 2026-08-28.</b> The Commander asked for the nearest Guardian
/// FSD Booster and heard <em>"Perez Ring, LHS 2637 — 5.79 lee, 395 lez out, large pad"</em>. Neither
/// is the provider misbehaving: <c>ly</c> is a word to a text-to-speech service and <em>lee</em> is
/// a fair guess at it. Kokoro's dictionary would say <em>lie</em>, just as wrongly.
/// </para>
/// <para>
/// <b>Asserted at the pipeline rather than per provider</b>, which is the whole reason the rewrite
/// lives there: it is one change that covers ElevenLabs, Kokoro, Edge and OpenAI at once, and no
/// provider gets a chance to disagree with the others.
/// </para>
/// </summary>
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

    /// <summary>
    /// The line the Commander heard, as the provider now receives it. Both units in one sentence,
    /// because that is how the range callout writes them.
    /// </summary>
    [Fact]
    public async Task TheReportedSentenceIsSpokenInWords()
    {
        var spoken = await SpokenAsync(
            "Nearest 5H Guardian FSD Booster: Perez Ring, LHS 2637 — 5.79 ly, 395 ls out, large pad.");

        Assert.Contains("5.79 light years", spoken, StringComparison.Ordinal);
        Assert.Contains("395 light seconds out", spoken, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And the transcript is untouched.</b> The caption, the panel and the log are text rather
    /// than sound, and <c>5.79 ly</c> is how a Commander wants that written down. This is the
    /// assertion that says the rewrite is a speech rewrite and not a change to what d47 says.
    /// </summary>
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

    /// <summary>
    /// The five units, each anchored to a number. The table is the place a sixth is added, so the
    /// five are asserted by name rather than by sampling.
    /// </summary>
    [Theory]
    [InlineData("5.79 ly", "5.79 light years")]
    [InlineData("395 ls", "395 light seconds")]
    [InlineData("128 t", "128 tonnes")]
    [InlineData("6,680 t", "6,680 tonnes")]
    [InlineData("32 MW", "32 megawatts")]
    [InlineData("1,250,000 cr", "1,250,000 credits")]
    public void EveryUnitInTheTableIsSaid(string written, string expected) =>
        Assert.Equal(expected, SpokenUnits.Rewrite(written));

    /// <summary>
    /// <b>Singular at exactly one, plural everywhere else including 1.0.</b> A decimal is plural
    /// however small it is — a person says <em>1.5 light years</em> and would say <em>1.0 light
    /// years</em> — so the test is the digits as written rather than the value they parse to.
    /// </summary>
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

    /// <summary>
    /// <b>A name is not a unit, and this is the assertion the whole design exists to pass.</b>
    /// Every one of these carries the letters of a unit and none of them follows a number, so the
    /// line comes back exactly as it went in. <c>LHS 2637</c> is from the reported sentence itself:
    /// a catalogue number is a number, and what follows it is a comma rather than a unit.
    /// <para>
    /// Asserted by equality rather than by hunting for what should be absent, because a rewrite
    /// that fired somewhere unexpected is caught by equality and can slip past a search for one
    /// particular wrong word.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <b>The unit has to follow a number, and the number has to be a token of its own.</b> The
    /// second case is the one a lookbehind buys: without it the <c>2637</c> inside a designation
    /// could anchor a unit that happened to follow the designation.
    /// </summary>
    [Theory]
    [InlineData("ly out")]
    [InlineData("Booster ly")]
    [InlineData("B0-GQPI ly")]
    [InlineData("A2637 ly")]
    public void AUnitWithNoNumberInFrontOfItIsNotAUnit(string line) =>
        Assert.Equal(line, SpokenUnits.Rewrite(line));

    // ---- Through the ladder, which is the other half of the sound -----------------------------

    /// <summary>
    /// <b>The local voice says the words rather than a syllable.</b> Kokoro reads phonemes, and
    /// <c>ly</c> parses as a perfectly good English syllable — so the ladder had no reason to
    /// refuse it and said <em>lie</em>. After the rewrite it is two dictionary words, which is the
    /// point of rewriting to words rather than to IPA: every provider already says these.
    /// </summary>
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
    /// read out of it on 2026-08-29 rather than written by ear, which is the rule the number-word
    /// table already follows. The ladder's top rung is a dictionary, and this stands in for it.
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
