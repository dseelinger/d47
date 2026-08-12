using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalSpineTests
{
    private static string FixturesDirectory { get; } = FindFixturesDirectory();

    [Fact]
    public void PollWithNoFilesYetReturnsNoEventsRatherThanThrowing()
    {
        using var install = new TempInstall();
        var spine = new JournalSpine(install.Root, new GameStateStore(), NullLoggerFactory.Instance);

        Assert.Empty(spine.Poll());
        Assert.Null(spine.CurrentFile);
    }

    [Fact]
    public void TailingTheOnlyFixtureAnswersTheCommandersLocation()
    {
        using var install = new TempInstall();
        CopyFixture("Journal.2026-02-10T090000.01.log", install.Root);

        var gameState = new GameStateStore();
        new JournalSpine(install.Root, gameState, NullLoggerFactory.Instance).Poll();

        Assert.Equal("Fixture One", gameState.Active!.Identity.Name);
        Assert.Equal("Fixture Nebula Point", gameState.Active!.Location.StarSystem);

        // The fixture ends with Docked then Undocked - the final state should reflect that,
        // not either event in isolation.
        Assert.False(gameState.Active!.Location.Docked);
        Assert.Null(gameState.Active!.Location.StationName);
    }

    [Fact]
    public void TwoCommandersRemainIsolatedWhenBothFixturesArePresent()
    {
        using var install = new TempInstall();
        CopyFixture("Journal.2026-02-10T090000.01.log", install.Root);
        CopyFixture("Journal.2026-02-10T113000.01.log", install.Root);

        var gameState = new GameStateStore();
        var spine = new JournalSpine(install.Root, gameState, NullLoggerFactory.Instance);
        spine.Poll();

        // The later-named file is latest, so it - Fixture Two - is the one actually tailed.
        Assert.EndsWith("Journal.2026-02-10T113000.01.log", spine.CurrentFile);
        Assert.Equal("Fixture Two", gameState.Active!.Identity.Name);
        Assert.Equal("Fixture Reach", gameState.Active!.Location.StarSystem);

        // Fixture One's file was never read by this spine at all - not merely "isolated", it
        // was never touched, which is the strongest form of not blending.
        Assert.Single(gameState.All);
    }

    [Fact]
    public void SwitchingToANewlyAppearedFileMidRunPicksItUpFromScratch()
    {
        using var install = new TempInstall();
        CopyFixture("Journal.2026-02-10T090000.01.log", install.Root);

        var gameState = new GameStateStore();
        var spine = new JournalSpine(install.Root, gameState, NullLoggerFactory.Instance);
        spine.Poll();
        Assert.Equal("Fixture One", gameState.Active!.Identity.Name);

        // Elite restarts as a different Commander mid-run: a new, later-named file appears.
        CopyFixture("Journal.2026-02-10T113000.01.log", install.Root);
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
        // The literal "1x vs 100x" property: hammering Poll() far more often than there is new
        // data must be harmless, and must converge on the same state as calling it once.
        using var install = new TempInstall();
        CopyFixture("Journal.2026-02-10T090000.01.log", install.Root);

        var singlePoll = new GameStateStore();
        new JournalSpine(install.Root, singlePoll, NullLoggerFactory.Instance).Poll();

        var manyPolls = new GameStateStore();
        var spine = new JournalSpine(install.Root, manyPolls, NullLoggerFactory.Instance);
        for (var i = 0; i < 100; i++)
        {
            spine.Poll();
        }

        Assert.Equal(singlePoll.Active!.Identity, manyPolls.Active!.Identity);
        Assert.Equal(singlePoll.Active!.Location, manyPolls.Active!.Location);
    }

    private static void CopyFixture(string fileName, string destinationDirectory) =>
        File.Copy(Path.Combine(FixturesDirectory, fileName), Path.Combine(destinationDirectory, fileName));

    private static string FindFixturesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException($"Could not find the repository root above {AppContext.BaseDirectory}.")
            : Path.Combine(directory.FullName, "tests", "fixtures", "journal");
    }
}
