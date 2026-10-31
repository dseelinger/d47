using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A Commander picked from the window, and the history walk's result, are taken by their tick subscribers.</summary>
public class APickAndTheHistoryAreTakenOnTheTickTests
{
    [Fact]
    public void ACommanderQueuedOnPicksIsShownByTheNextTick()
    {
        var fixture = new JournalTickFixture();

        fixture.Tick.Picks.Enqueue(new CommanderIdentity("F2", "Picked"));
        fixture.Tick.TakePicks(fixture.Next());

        Assert.Equal("F2", fixture.GameState.Active?.Identity.FrontierId);
        Assert.Empty(fixture.Tick.Picks);
    }

    [Fact]
    public void TheHistoryIsAdoptedOnceOnTheFirstTickAfterTheWalkIsDone()
    {
        var offered = 0;
        var fixture = new JournalTickFixture(new GameStateStore
        {
            RestoreCarrier = _ =>
            {
                offered++;
                return null;
            },
        });

        fixture.GameState.Pick(new CommanderIdentity("F1", "Fixture"));
        var before = offered;

        fixture.Tick.AdoptHistory(fixture.Next());
        Assert.Equal(before, offered);

        fixture.History.Run(TestContext.Current.CancellationToken);
        Assert.Equal(HistoryState.Done, fixture.History.State);

        fixture.Tick.AdoptHistory(fixture.Next());
        Assert.Equal(before + 1, offered);

        fixture.Tick.AdoptHistory(fixture.Next());
        Assert.Equal(before + 1, offered);
    }
}
