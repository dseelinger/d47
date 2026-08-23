using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A word on getting into a game and on leaving one (change-requests.md 29).
/// <para>
/// The request is one sentence and the corpus decided two things in it. Roughly one session in
/// eleven never writes <c>Shutdown</c>, so the departure line has to be allowed not to happen;
/// and of 433 consecutive <c>LoadGame</c> pairs 57% are under thirty minutes apart, which is what
/// makes the asked-for cooldown the right default rather than a guess.
/// </para>
/// </summary>
public class SessionCalloutTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 23, 19, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, string? fid = null)
    {
        var json = fid is null
            ? $$"""{"timestamp":"2026-08-23T19:00:00Z","event":"{{kind}}"}"""
            : $$"""{"timestamp":"2026-08-23T19:00:00Z","event":"{{kind}}","FID":"{{fid}}"}""";

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext At(TimeSpan since, bool priming, params JournalEvent[] events) =>
        new(Start + since, priming, null, GameStatus.Unknown, NavRoute.None, events);

    private static string[] Said(SessionCallout callout, CalloutContext context) =>
        [.. callout.Examine(context).Select(announcement => announcement.Text)];

    [Fact]
    public void ItSaysSomethingOnGettingIn()
    {
        var callout = new SessionCallout();

        var said = Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1")));

        Assert.Single(said);
        Assert.Contains("Welcome back", said[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AndOnLeaving()
    {
        var callout = new SessionCallout();

        Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1")));

        var said = Said(callout, At(TimeSpan.FromHours(2), priming: false, Event("Shutdown")));

        Assert.Single(said);
        Assert.Contains("Ship secured", said[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The reported irritation.</b> A re-log inside the window says nothing; the median gap in
    /// the corpus is 21.2 minutes, so this is the ordinary case rather than an edge one.
    /// </summary>
    [Fact]
    public void ARelogInsideTheWindowIsSilent()
    {
        var callout = new SessionCallout();

        Assert.Single(Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1"))));

        Assert.Empty(Said(
            callout, At(TimeSpan.FromMinutes(21), priming: false, Event("LoadGame", "F1"))));
    }

    /// <summary>
    /// <b>And the clock does not restart on a suppressed one.</b> Stamping it every time would
    /// mean a Commander bouncing in and out every ten minutes never hears the line again, however
    /// long they eventually stay away — the bug this is the fix for rather than a nicety.
    /// </summary>
    [Fact]
    public void ASuppressedRelogDoesNotPushTheWindowOut()
    {
        var callout = new SessionCallout();

        Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1")));
        Said(callout, At(TimeSpan.FromMinutes(10), priming: false, Event("LoadGame", "F1")));
        Said(callout, At(TimeSpan.FromMinutes(20), priming: false, Event("LoadGame", "F1")));

        // Thirty-one minutes after the one that was said, not after the last one attempted.
        Assert.Single(Said(
            callout, At(TimeSpan.FromMinutes(31), priming: false, Event("LoadGame", "F1"))));
    }

    /// <summary>
    /// A second Commander is arriving for the first time, not re-logging. One clock between them
    /// would silence whoever came second.
    /// </summary>
    [Fact]
    public void ASecondCommanderGetsTheirOwnWindow()
    {
        var callout = new SessionCallout();

        Assert.Single(Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1"))));
        Assert.Single(Said(
            callout, At(TimeSpan.FromMinutes(2), priming: false, Event("LoadGame", "F2"))));
    }

    /// <summary>
    /// <b>Priming folds the backlog and says nothing.</b> d47 starting reads every LoadGame of the
    /// day; without this, launching it after an evening's flying announces an arrival from four
    /// hours ago.
    /// </summary>
    [Fact]
    public void TheStartupBacklogIsFoldedRatherThanAnnounced()
    {
        var callout = new SessionCallout();

        Assert.Empty(Said(
            callout,
            At(TimeSpan.Zero, priming: true, Event("LoadGame", "F1"), Event("Shutdown"), Event("LoadGame", "F1"))));
    }

    /// <summary>Off is off, both directions.</summary>
    [Fact]
    public void SwitchedOffItSaysNeither()
    {
        var callout = new SessionCallout { Enabled = () => false };

        Assert.Empty(Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1"))));
        Assert.Empty(Said(callout, At(TimeSpan.FromHours(3), priming: false, Event("Shutdown"))));
    }

    /// <summary>
    /// The two directions keep separate clocks, so a quick out-and-back still says goodbye and
    /// hello once each rather than one of them swallowing the other.
    /// </summary>
    [Fact]
    public void LeavingAndArrivingDoNotShareAWindow()
    {
        var callout = new SessionCallout();

        Assert.Single(Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1"))));
        Assert.Single(Said(callout, At(TimeSpan.FromMinutes(5), priming: false, Event("Shutdown"))));
    }

    /// <summary>
    /// <b>A session that never says goodbye is not reconstructed.</b> 84 of 925 journals end with
    /// no Shutdown at all, and a timeout that guessed at a departure would eventually say it to a
    /// Commander who is still flying.
    /// </summary>
    [Fact]
    public void SilenceIsNotADeparture()
    {
        var callout = new SessionCallout();

        Said(callout, At(TimeSpan.Zero, priming: false, Event("LoadGame", "F1")));

        // Hours pass with nothing in the journal at all.
        Assert.Empty(Said(callout, At(TimeSpan.FromHours(6), priming: false)));
    }
}
