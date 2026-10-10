using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalSpineTests
{
    private const string Root = @"C:\d47-test\journals";

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void PollWithNoFilesYetReturnsNoEventsRatherThanThrowing()
    {
        var spine = new JournalSpine(Root, _files, new GameStateStore(), NullLoggerFactory.Instance);

        Assert.Empty(spine.Poll());
        Assert.Null(spine.CurrentFile);
    }

    [Fact]
    public void TailingTheOnlyFixtureAnswersTheCommandersLocation()
    {
        CopyFixture("Journal.2026-02-10T090000.01.log");

        var gameState = new GameStateStore();
        new JournalSpine(Root, _files, gameState, NullLoggerFactory.Instance).Poll();

        Assert.Equal("Fixture One", gameState.Active!.Identity.Name);
        Assert.Equal("Fixture Nebula Point", gameState.Active!.Location.StarSystem);

        // The fixture ends with Docked then Undocked - the final state should reflect that, not either event
        // in isolation.
        Assert.False(gameState.Active!.Location.Docked);
        Assert.Null(gameState.Active!.Location.StationName);
    }

    [Fact]
    public void TwoCommandersRemainIsolatedWhenBothFixturesArePresent()
    {
        CopyFixture("Journal.2026-02-10T090000.01.log");
        CopyFixture("Journal.2026-02-10T113000.01.log");

        var gameState = new GameStateStore();
        var spine = new JournalSpine(Root, _files, gameState, NullLoggerFactory.Instance);
        spine.Poll();

        // The later-named file is latest, so it - Fixture Two - is the one actually tailed.
        Assert.EndsWith("Journal.2026-02-10T113000.01.log", spine.CurrentFile);
        Assert.Equal("Fixture Two", gameState.Active!.Identity.Name);
        Assert.Equal("Fixture Reach", gameState.Active!.Location.StarSystem);

        // Fixture One's file was never read by this spine at all - not merely "isolated", it was never
        // touched, which is the strongest form of not blending.
        Assert.Single(gameState.All);
    }

    [Fact]
    public void SwitchingToANewlyAppearedFileMidRunPicksItUpFromScratch()
    {
        CopyFixture("Journal.2026-02-10T090000.01.log");

        var gameState = new GameStateStore();
        var spine = new JournalSpine(Root, _files, gameState, NullLoggerFactory.Instance);
        spine.Poll();
        Assert.Equal("Fixture One", gameState.Active!.Identity.Name);

        // Elite restarts as a different Commander mid-run: a new, later-named file appears.
        CopyFixture("Journal.2026-02-10T113000.01.log");
        spine.Poll();

        Assert.Equal("Fixture Two", gameState.Active!.Identity.Name);
        Assert.Equal(2, gameState.All.Count); // both buckets still exist
        Assert.Equal(
            "Fixture Nebula Point",
            gameState.All.Single(c => c.Identity.Name == "Fixture One").Location.StarSystem);
    }

    [Fact]
    public void ReplayingTheSameFixtureIsDeterministicRegardlessOfHowOftenPollIsCalled()
    {
        // The literal "1x vs 100x" property: hammering Poll() far more often than there is new data must be
        // harmless, and must converge on the same state as calling it once.
        CopyFixture("Journal.2026-02-10T090000.01.log");

        var singlePoll = new GameStateStore();
        new JournalSpine(Root, _files, singlePoll, NullLoggerFactory.Instance).Poll();

        var manyPolls = new GameStateStore();
        var spine = new JournalSpine(Root, _files, manyPolls, NullLoggerFactory.Instance);
        for (var i = 0; i < 100; i++)
        {
            spine.Poll();
        }

        Assert.Equal(singlePoll.Active!.Identity, manyPolls.Active!.Identity);
        Assert.Equal(singlePoll.Active!.Location, manyPolls.Active!.Location);
    }

    private void CopyFixture(string fileName) =>
        _files.WriteText(Path.Combine(Root, fileName), EmbeddedFixture.Text("journal." + fileName));
}
