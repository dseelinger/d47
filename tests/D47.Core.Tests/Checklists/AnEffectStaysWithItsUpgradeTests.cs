using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>An experimental effect stays with the upgrade it belongs to.</summary>
public class AnEffectStaysWithItsUpgradeTests
{
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"3311-04-08T18:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"3311-04-08T18:00:01Z","event":"Loadout","Ship":"krait_mkii","ShipID":12,"ShipName":"Nightjar","Modules":[]}""",
                     """{"timestamp":"3311-04-08T18:00:02Z","event":"Location","StarSystem":"Sol","Docked":false}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistItem Line(
        string key,
        ChecklistIntentKind kind,
        string slot,
        ChecklistState state) => new()
    {
        Key = key,
        Scope = ChecklistScope.Ship(12),
        Kind = ChecklistItemKind.Derived,
        Text = $"{kind} on {slot}",
        Source = ChecklistSource.EngineeringPlan,
        Intent = new ChecklistIntent(kind, slot) { Detail = "Dirty Drive Tuning", Grade = 5 },
        State = state,
    };

    private static ChecklistDocument Holding(params ChecklistItem[] items) =>
        ChecklistDocument.For("F1", "Jameson") with { Items = items };

    private static string[] Keys(ChecklistDocument document) =>
        [.. ChecklistOrdering.Arrange(document, State()).Select(item => item.Key)];

    /// <summary>The worst separation, and the case a Commander most needs them together.</summary>
    [Fact]
    public void AGatedUpgradeKeepsItsEffectBesideItRatherThanAtTheTop()
    {
        var document = Holding(
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Blocked),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open),
            Line("other", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Open));

        Assert.Equal(["other", "upgrade", "effect"], Keys(document));
    }

    [Fact]
    public void AnEffectAppendedLongAfterItsUpgradeStillSitsBesideIt()
    {
        var document = Holding(
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Open),
            Line("a", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Open),
            Line("b", ChecklistIntentKind.Blueprint, "FSD", ChecklistState.Open),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open));

        Assert.Equal(["upgrade", "effect", "a", "b"], Keys(document));
    }

    /// <summary>
    /// The upgrade first, which is the order they are born in and the order the work happens in — an
    /// experimental effect does not exist without one.
    /// </summary>
    [Fact]
    public void TheUpgradeComesFirstEvenWhereTheFileHoldsThemTheOtherWayRound()
    {
        var document = Holding(
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open),
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Open));

        Assert.Equal(["upgrade", "effect"], Keys(document));
    }

    /// <summary>The case that is already right, and must stay right.</summary>
    [Fact]
    public void AnEffectWithNoUpgradeOnTheListKeepsItsOwnPlace()
    {
        var document = Holding(
            Line("blocked", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Blocked),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open));

        Assert.Equal(["effect", "blocked"], Keys(document));
    }

    /// <summary>Two modules' pairs do not braid.</summary>
    [Fact]
    public void TwoPairsStayTwoPairs()
    {
        var document = Holding(
            Line("engines", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Open),
            Line("plant", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Blocked),
            Line("plant effect", ChecklistIntentKind.Experimental, "PowerPlant", ChecklistState.Open),
            Line("engine effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open));

        Assert.Equal(["engines", "engine effect", "plant", "plant effect"], Keys(document));
    }

    /// <summary>A done upgrade still keeps its effect, and the pair sinks together.</summary>
    [Fact]
    public void ADoneUpgradeTakesItsOpenEffectDownWithIt()
    {
        var document = Holding(
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Done),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open),
            Line("other", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Open));

        Assert.Equal(["other", "upgrade", "effect"], Keys(document));
    }

    /// <summary>The kinship itself, since three mechanisms now share it.</summary>
    [Fact]
    public void LinesWithNoSlotAreNotKin()
    {
        var one = Line("one", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Open)
            with { Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, string.Empty) };

        var other = Line("other", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open)
            with { Intent = new ChecklistIntent(ChecklistIntentKind.Experimental, string.Empty) };

        Assert.False(ChecklistKinship.SameModule(one, other));
    }

    /// <summary>And two ships' slot 1 are two different modules.</summary>
    [Fact]
    public void TheSameSlotOnTwoShipsIsNotOneModule()
    {
        var one = Line("one", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Open);

        var other = Line("other", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open)
            with { Scope = ChecklistScope.Ship(13) };

        Assert.False(ChecklistKinship.SameModule(one, other));
    }
}
