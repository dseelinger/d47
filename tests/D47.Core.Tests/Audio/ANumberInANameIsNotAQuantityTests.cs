using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Designation numbers written out before any provider sees them (#91).</summary>
public class ANumberInANameIsNotAQuantityTests
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

    // ---- The ruling ---------------------------------------------------------------------------

    /// <summary>Four or more digits are said one at a time; three or fewer keep the casual reading.</summary>
    [Theory]
    [InlineData("HIP 3269", "HIP three two six nine")]
    [InlineData("LHS 3447", "LHS three four four seven")]
    [InlineData("Col 385", "Col three eighty-five")]
    [InlineData("Sol 1", "Sol one")]
    [InlineData("Beta 42", "Beta forty-two")]
    [InlineData("Market 3704402688", "Market three seven zero four four zero two six eight eight")]
    public void TheLengthDecidesTheReading(string written, string expected) =>
        Assert.Equal(expected, SpokenDesignations.Rewrite(written));

    /// <summary>Every run in the name, and the punctuation between them left where it was.</summary>
    [Theory]
    [InlineData("NGC 1981 Sector AL-X c1-12", "NGC one nine eight one Sector AL-X c one-twelve")]
    [InlineData("Col 285 Sector MV-A b15-3", "Col two eighty-five Sector MV-A b fifteen-three")]
    [InlineData("HIP 3269.", "HIP three two six nine.")]
    [InlineData("Bound for HIP 3269, then Sol.", "Bound for HIP three two six nine, then Sol.")]
    public void EveryRunInTheNameIsRead(string written, string expected) =>
        Assert.Equal(expected, SpokenDesignations.Rewrite(written));

    /// <summary>A leading zero is part of the name, so those digits are said one at a time too.</summary>
    [Theory]
    [InlineData("Bay 07", "Bay zero seven")]
    [InlineData("WISE J000517", "WISE J zero zero zero five one seven")]
    public void ALeadingZeroIsPartOfTheName(string written, string expected) =>
        Assert.Equal(expected, SpokenDesignations.Rewrite(written));

    /// <summary>Digits that run into letters get a space, or the words run together into another word.</summary>
    [Theory]
    [InlineData("V371 Normae", "V three seventy-one Normae")]
    [InlineData("LHS 5217a", "LHS five two one seven a")]
    [InlineData("Fullerene C60", "Fullerene C sixty")]
    public void DigitsAgainstLettersAreSeparated(string written, string expected) =>
        Assert.Equal(expected, SpokenDesignations.Rewrite(written));

    // ---- What must never be rewritten ---------------------------------------------------------

    /// <summary>A measured quantity is not a designation and keeps its digits.</summary>
    [Theory]
    [InlineData("5.79")]
    [InlineData("6,680")]
    [InlineData("1,250,000")]
    [InlineData("0.5")]
    public void AMeasuredQuantityIsLeftAlone(string line) =>
        Assert.Equal(line, SpokenDesignations.Rewrite(line));

    /// <summary>
    /// A number with a unit after it belongs to <see cref="SpokenUnits"/>, which needs the digits.
    /// </summary>
    [Theory]
    [InlineData("395 ls")]
    [InlineData("128 t")]
    [InlineData("32 MW")]
    [InlineData("500 Cr")]
    [InlineData("395 ls out")]
    [InlineData("500 cr, then home")]
    public void AUnitKeepsItsNumberInDigits(string line) =>
        Assert.Equal(line, SpokenDesignations.Rewrite(line));

    /// <summary>A line with no digits in it comes back the same string.</summary>
    [Theory]
    [InlineData("Shinrarta Dezhra")]
    [InlineData("")]
    public void ALineWithNoDigitsIsUntouched(string line) =>
        Assert.Equal(line, SpokenDesignations.Rewrite(line));

    // ---- Through the seam, which is what reaches every voice -----------------------------------

    /// <summary>The two passes together: the designation is read and the unit still expands.</summary>
    [Fact]
    public async Task TheSeamReadsTheNameAndStillSaysTheUnit()
    {
        var spoken = await SpokenAsync("Perez Ring, LHS 2637 — 5.79 ly, 395 ls out, large pad.");

        Assert.Contains("LHS two six three seven", spoken, StringComparison.Ordinal);
        Assert.Contains("5.79 light years", spoken, StringComparison.Ordinal);
        Assert.Contains("395 light seconds out", spoken, StringComparison.Ordinal);
    }

    /// <summary>And the transcript is untouched, because only the provider's copy is rewritten.</summary>
    [Fact]
    public async Task TheTranscriptStillShowsTheDigits()
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
            pipeline.Push("Plotted to HIP 3269.");
            await pipeline.CompleteAsync();
        }

        var said = Assert.Single(log, line => line.Contains(" said: ", StringComparison.Ordinal));

        Assert.Contains("HIP 3269", said, StringComparison.Ordinal);
        Assert.DoesNotContain("three two six nine", said, StringComparison.Ordinal);
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
