using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ALaunchedFighterIsHeldUntilItIsGoneTests
{
    private static string Launch(bool playerControlled, long id = 7) =>
        $$"""{ "timestamp":"2025-07-05T02:18:48Z", "event":"LaunchFighter", "Loadout":"one", "ID":{{id}}, "PlayerControlled":{{(playerControlled ? "true" : "false")}} }""";

    private static string Event(string name, long? id = null) =>
        id is { } i
            ? $$"""{ "timestamp":"2025-07-05T02:20:00Z", "event":"{{name}}", "ID":{{i}} }"""
            : $$"""{ "timestamp":"2025-07-05T02:20:00Z", "event":"{{name}}" }""";

    private static CommanderGameState Fold(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    [Fact]
    public void ALaunchHoldsWhoFliesIt()
    {
        var crew = Fold(Launch(false)).Fighter;
        var commander = Fold(Launch(true)).Fighter;

        Assert.Equal(new LaunchedFighter(7, false, new DateTimeOffset(2025, 7, 5, 2, 18, 48, TimeSpan.Zero)), crew);
        Assert.True(commander!.CommanderFlies);
        Assert.Null(Fold().Fighter);
    }

    [Theory]
    [InlineData("DockFighter")]
    [InlineData("FighterDestroyed")]
    public void ThatFightersDockingOrLossClearsItAndAnotherOneDoesNot(string name)
    {
        Assert.Null(Fold(Launch(false), Event(name, 7)).Fighter);
        Assert.Equal(7, Fold(Launch(false), Event(name, 8)).Fighter!.Id);
    }

    [Theory]
    [InlineData("Died")]
    [InlineData("LoadGame")]
    [InlineData("Docked")]
    [InlineData("SupercruiseEntry")]
    [InlineData("FSDJump")]
    [InlineData("CarrierJump")]
    public void ChangingPlaceOrLifeClearsIt(string name)
    {
        Assert.Null(Fold(Launch(true), Event(name)).Fighter);
    }
}
