using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>While Elite is not running, chatter is refused and every other announcement is queued (#2011).</summary>
public class NoChatterWhileEliteIsClosedTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class Says(params Announcement[] announcements) : ICallout
    {
        public string Id => "test.says";

        public IEnumerable<Announcement> Examine(CalloutContext context) => announcements;
    }

    private static readonly Announcement Chatter =
        new("test.chatter", "Quiet out here.") { Chatter = TimeSpan.FromMinutes(1) };

    private static readonly Announcement Invented =
        new("test.invented", "Two voices.") { Origin = AnnouncementOrigin.InventedChatter };

    private static readonly Announcement Resume = new("continuity.resume", "Welcome back.");

    private static IReadOnlyList<Announcement> Tick(bool running)
    {
        var store = new GameStateStore();
        store.GameRunning(running);
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(new Says(Chatter, Invented, Resume));

        engine.Tick(CalloutContext.For(Start, false, store, GameStatus.Unknown, new NavRoute(), []));
        return engine.Drain();
    }

    [Fact]
    public void ChatterIsNotQueuedWhileEliteIsClosed()
    {
        var said = Tick(running: false);

        Assert.DoesNotContain(said, a => a.Key is "test.chatter" or "test.invented");
    }

    [Fact]
    public void AnythingElseIsStillQueuedWhileEliteIsClosed()
    {
        var said = Tick(running: false);

        Assert.Contains(said, a => a.Key == "continuity.resume");
    }

    [Fact]
    public void ChatterIsQueuedWhileEliteRuns()
    {
        var said = Tick(running: true);

        Assert.Contains(said, a => a.Key == "test.chatter");
        Assert.Contains(said, a => a.Key == "test.invented");
    }
}
