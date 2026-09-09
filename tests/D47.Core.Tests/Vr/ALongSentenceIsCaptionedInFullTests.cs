using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>A sentence that wraps past two lines reaches the Commander whole.</summary>
public class ALongSentenceIsCaptionedInFullTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Four lines' worth: long enough to need two events, short of the sentence cap.</summary>
    private const string Long =
        "Fuel is at nineteen per cent and the nearest scoopable star is two jumps behind you, "
        + "so the honest options are a detour to Hyades Sector or a very careful approach to the "
        + "station you are already pointed at.";

    /// <summary>
    /// Every line the surface was ever shown, in order, with the window's own repetition taken out — a
    /// rolling window shows the same line in consecutive frames, and that is not the same as it having
    /// been shown twice.
    /// </summary>
    private static List<string> Shown(CaptionLayer layer, Action say, TimeSpan over)
    {
        var seen = new List<string>();

        void Record()
        {
            foreach (var line in layer.Lines)
            {
                if (!seen.Contains(line, StringComparer.Ordinal))
                {
                    seen.Add(line);
                }
            }
        }

        // Subscribed before the utterance, because the first event goes up inside Say and a recorder attached
        // afterwards would miss exactly the lines this file exists to check.
        layer.Changed += Record;
        say();

        // A tenth of a second, which is the tick loop's own rate — so this drives the layer at exactly the
        // cadence the headset path drives it at, rather than at one convenient for the assertion.
        for (var elapsed = TimeSpan.Zero; elapsed <= over; elapsed += TimeSpan.FromSeconds(0.1))
        {
            layer.Tick(Now + elapsed);
        }

        layer.Changed -= Record;

        return seen;
    }

    /// <summary>The test the issue asked for first.</summary>
    [Fact]
    public void EveryLineOfALongSentenceReachesTheSurface()
    {
        var layer = new CaptionLayer();
        var wrapped = Caption.Wrap(Long);

        Assert.True(wrapped.Count > Caption.WindowLines, "the fixture no longer wraps past the window");

        var shown = Shown(layer, () => layer.Say(Long, Now), TimeSpan.FromSeconds(30));

        Assert.Equal(wrapped, shown);
    }

    /// <summary>And the words, which is the claim the wrapping is only the mechanism for.</summary>
    [Fact]
    public void NoWordIsDroppedBetweenTheVoiceAndTheGlass()
    {
        var layer = new CaptionLayer();

        var said = string.Join(' ', Shown(layer, () => layer.Say(Long, Now), TimeSpan.FromSeconds(30)));

        Assert.Equal(
            string.Join(' ', Long.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
            said);
    }

    /// <summary>The beginning arrives first, which is the half a reader cannot detect the loss of.</summary>
    [Fact]
    public void TheFirstEventIsTheBeginningOfTheSentence()
    {
        var layer = new CaptionLayer();

        layer.Say(Long, Now);

        Assert.Equal(Caption.Wrap(Long).Take(Caption.WindowLines), layer.Lines);
    }

    /// <summary>One event at a time, never a wall of text.</summary>
    [Fact]
    public void TheWindowNeverHoldsMoreThanTheStandardAllows()
    {
        var layer = new CaptionLayer();
        var widest = 0;

        layer.Changed += () => widest = Math.Max(widest, layer.Lines.Count);
        layer.Say(Long, Now);

        for (var elapsed = TimeSpan.Zero; elapsed <= TimeSpan.FromSeconds(30); elapsed += TimeSpan.FromSeconds(0.1))
        {
            layer.Tick(Now + elapsed);
        }

        Assert.Equal(Caption.WindowLines, widest);
    }

    /// <summary>The events are paced by the reading speed row, not by a constant.</summary>
    [Fact]
    public void ASlowerReaderGetsLongerOnEveryEventAndNotOnlyTheLast()
    {
        var quick = new CaptionLayer { Settings = new CaptionSettings { CharactersPerSecond = 20 } };
        var slow = new CaptionLayer { Settings = new CaptionSettings { CharactersPerSecond = 8 } };

        quick.Say(Long, Now);
        slow.Say(Long, Now);

        // Five seconds in, the fast reader has been moved on and the slow one has not: the first event is 78
        // characters, which is 3.9 seconds at twenty a second and 9.8 at eight.
        for (var elapsed = TimeSpan.Zero; elapsed <= TimeSpan.FromSeconds(5); elapsed += TimeSpan.FromSeconds(0.1))
        {
            quick.Tick(Now + elapsed);
            slow.Tick(Now + elapsed);
        }

        Assert.NotEqual(quick.Lines, slow.Lines);
        Assert.Equal(Caption.Wrap(Long).Take(Caption.WindowLines), slow.Lines);
    }

    /// <summary>The dwell cannot start while lines are still waiting.</summary>
    [Fact]
    public void TheVoiceStoppingDoesNotThrowAwayWhatIsStillQueued()
    {
        var layer = new CaptionLayer();

        var shown = Shown(
            layer,
            () =>
            {
                layer.Say(Long, Now);

                // The voice finishes almost immediately, well inside the first event.
                layer.Quiet(Now + TimeSpan.FromSeconds(0.5));
            },
            TimeSpan.FromSeconds(30));

        Assert.Equal(Caption.Wrap(Long), shown);

        // And it does eventually go away rather than sitting there for the rest of the session.
        Assert.False(layer.Visible);
    }

    /// <summary>The voice is the master clock, and this is the ruling.</summary>
    [Fact]
    public void ANewSentenceTakesTheGlassFromWhateverIsStillQueued()
    {
        var layer = new CaptionLayer();

        layer.Say(Long, Now, utterance: 1);
        layer.Say("Interdiction.", Now + TimeSpan.FromSeconds(0.5), utterance: 2);

        Assert.Contains("Interdiction.", layer.Lines);

        // And nothing left over from the abandoned one ever surfaces afterwards.
        var after = Shown(layer, () => { }, TimeSpan.FromSeconds(30));

        Assert.DoesNotContain(after, line => Caption.Wrap(Long).Skip(Caption.WindowLines).Contains(line));
    }

    /// <summary>
    /// Consecutive short sentences still share the window, which is the roll-up form live captioning
    /// uses and the behaviour that already worked.
    /// </summary>
    [Fact]
    public void ShortSentencesStillRollUpTogether()
    {
        var layer = new CaptionLayer();

        layer.Say("One.", Now, utterance: 1);
        layer.Say("Two.", Now, utterance: 2);
        layer.Say("Three.", Now, utterance: 3);

        Assert.Equal(["Two.", "Three."], layer.Lines);
    }
}
