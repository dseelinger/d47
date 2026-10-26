using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>A sentence that wraps past the window reaches the Commander whole.</summary>
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

    /// <summary>Speaks short sentences quickly enough that the measured speaking speed reaches its ceiling.</summary>
    private static void Prime(CaptionLayer layer, int sentences)
    {
        var at = Now - TimeSpan.FromMinutes(5);

        for (var i = 0; i < sentences; i++)
        {
            layer.Say(new string('x', 60), at, utterance: 1000 + i);
            at += TimeSpan.FromSeconds(2.5);
            layer.Quiet(at);
            at += TimeSpan.FromSeconds(10);
        }
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

        Prime(quick, 12);
        Prime(slow, 12);
        quick.Say(Long, Now);
        slow.Say(Long, Now);

        // With the voice measured at the fastest speed, the first event of three lines is held for about six
        // seconds by the quick reader and seven (the longest dwell) by the slow one.
        for (var elapsed = TimeSpan.Zero; elapsed <= TimeSpan.FromSeconds(6.5); elapsed += TimeSpan.FromSeconds(0.1))
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

        Assert.Equal(["One.", "Two.", "Three."], layer.Lines);
    }

    /// <summary>An event with lines behind it is held until the voice has reached its end.</summary>
    [Fact]
    public void AnEventWithLinesBehindItWaitsForTheVoice()
    {
        var layer = new CaptionLayer();

        for (var i = 0; i < 2; i++)
        {
            var at = Now - TimeSpan.FromMinutes(5) + TimeSpan.FromMinutes(i);

            layer.Say(new string('x', 55), at, utterance: 100 + i);
            layer.Quiet(at + TimeSpan.FromSeconds(5));
        }

        var speed = layer.SpeakingSpeed;
        Assert.InRange(speed, 11, 13);

        var sentence = Long + " " + Long[..60];
        var wrapped = Caption.Wrap(sentence);
        Assert.True(wrapped.Count > Caption.WindowLines);

        var first = wrapped.Take(Caption.WindowLines).ToList();
        var holds = string.Join(' ', first).Length / speed;

        layer.Say(sentence, Now, utterance: 1);
        layer.Tick(Now + TimeSpan.FromSeconds(holds - 0.2));
        Assert.Equal(first, layer.Lines);

        layer.Tick(Now + TimeSpan.FromSeconds(holds + 0.2));
        Assert.NotEqual(first, layer.Lines);
    }

    [Fact]
    public void ASentenceEndedBySilenceLeavesTheSpeakingSpeedAlone()
    {
        var layer = new CaptionLayer();
        var before = layer.SpeakingSpeed;

        layer.Say(new string('x', 200), Now, utterance: 1);
        layer.Silence();
        layer.Quiet(Now + TimeSpan.FromSeconds(30));

        Assert.Equal(before, layer.SpeakingSpeed);
    }

    [Fact]
    public void ASentenceUnderOneAndAHalfSecondsLeavesTheSpeakingSpeedAlone()
    {
        var layer = new CaptionLayer();
        var before = layer.SpeakingSpeed;

        layer.Say(new string('x', 200), Now, utterance: 1);
        layer.Quiet(Now + TimeSpan.FromSeconds(1.4));

        Assert.Equal(before, layer.SpeakingSpeed);
    }

    [Fact]
    public void TheSpeakingSpeedStaysBetweenEightAndTwentyHoweverExtremeTheSamples()
    {
        var fast = new CaptionLayer();
        var slow = new CaptionLayer();

        for (var i = 0; i < 30; i++)
        {
            var at = Now + TimeSpan.FromMinutes(i);

            fast.Say(new string('x', 400), at, utterance: i);
            fast.Quiet(at + TimeSpan.FromSeconds(1.5));
            slow.Say("x", at, utterance: i);
            slow.Quiet(at + TimeSpan.FromSeconds(500));
        }

        Assert.Equal(CaptionLayer.FastestSpeakingSpeed, fast.SpeakingSpeed);
        Assert.Equal(CaptionLayer.SlowestSpeakingSpeed, slow.SpeakingSpeed);
    }
}
