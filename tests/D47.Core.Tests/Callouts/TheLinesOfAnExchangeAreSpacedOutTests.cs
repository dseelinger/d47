using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Air between the lines of an invented exchange
/// (<a href="https://github.com/dseelinger/d47/issues/259">#259</a>), reported as
/// <em>"it's like watching an episode of the Gilmore Girls"</em>. Nothing was putting a gap
/// anywhere, so four utterances arrived butted together in about two seconds.
/// <para>
/// Two things are asserted here and they pull against each other. The beat has to be real and
/// varied, and it must never be the reason a danger callout arrives late — the speaking lock is
/// held for a whole batch, so air added inside one is air the next batch waits through.
/// </para>
/// </summary>
public class TheLinesOfAnExchangeAreSpacedOutTests
{
    [Fact]
    public void TheFirstLineOfAnExchangeWaitsForNothing()
    {
        // The gap belongs in front of a reply, not in front of the scene. An exchange that opened
        // with a second of silence would delay every batch behind it for no effect at all.
        Assert.Equal(TimeSpan.Zero, NpcChatter.Beat(0));
        Assert.Equal(TimeSpan.Zero, NpcChatter.Beat(-1));
    }

    [Fact]
    public void EveryLaterLineGetsABeatInsideTheRange()
    {
        for (var line = 1; line <= NpcChatter.MostLines; line++)
        {
            var beat = NpcChatter.Beat(line);

            Assert.InRange(beat, NpcChatter.ShortestBeat, NpcChatter.LongestBeat);
        }
    }

    /// <summary>
    /// A fixed pause would sound mechanical in its own way, which is the argument this repository
    /// already made about the gap <em>between</em> exchanges. It holds inside one too.
    /// </summary>
    [Fact]
    public void TheBeatIsNotTheSameTwiceRunning()
    {
        Assert.NotEqual(NpcChatter.Beat(1), NpcChatter.Beat(2));
        Assert.NotEqual(NpcChatter.Beat(2), NpcChatter.Beat(3));
    }

    /// <summary>
    /// Off the line's position and nothing else — no clock, no seed — so a recorded session
    /// replays to the pacing it was heard at.
    /// </summary>
    [Fact]
    public void TheSameLineReplaysToTheSameBeat()
    {
        for (var line = 0; line <= NpcChatter.MostLines; line++)
        {
            Assert.Equal(NpcChatter.Beat(line), NpcChatter.Beat(line));
        }
    }

    /// <summary>
    /// What the speaking loop asks before it takes a beat. The Commander hearing about the heat
    /// two seconds late because a courier was chatting is a worse defect than the rapid fire.
    /// </summary>
    [Fact]
    public void AWarningQueuedBehindAnExchangeIsSeenWhileItIsStillWaiting()
    {
        var engine = Saying(
            new Announcement("fuel.low", "Fuel is low.", CalloutUrgency.Urgent));

        engine.Tick(Context());

        Assert.True(engine.AnythingUrgentWaiting);

        // Read rather than drained: the loop asks this again on every slice of a pause, and an
        // answer that consumed the warning would be an answer that lost it.
        Assert.Single(engine.Drain());
    }

    [Fact]
    public void RoutineTrafficIsNotAReasonToCutTheBeatShort()
    {
        var engine = Saying(new Announcement("route.progress", "Three jumps out."));

        engine.Tick(Context());

        Assert.False(engine.AnythingUrgentWaiting);
    }

    [Fact]
    public void AnEmptyQueueIsNothingWaiting()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);

        Assert.False(engine.AnythingUrgentWaiting);
    }

    private static CalloutContext Context() =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: false,
            State: null,
            GameStatus.Unknown,
            NavRoute.None,
            []);

    private static CalloutEngine Saying(params Announcement[] lines) =>
        new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new Scripted(new Queue<Announcement>(lines)));

    /// <summary>A callout that says exactly what the test hands it, one line per tick.</summary>
    private sealed class Scripted(Queue<Announcement> lines) : ICallout
    {
        public string Id => "scripted";

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            if (lines.Count > 0)
            {
                yield return lines.Dequeue();
            }
        }
    }
}
