using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// An experimental effect stays with the upgrade it belongs to (GitHub issue 31).
/// <para>
/// They are born paired and in the right order — <see cref="EngineeringPlan"/> writes the blueprint
/// line and then the experimental line for one slot, back to back — and two later mechanisms pulled
/// them apart: banding, which sorts on state, and <c>Revise</c>, which appends every newly opened
/// item after every kept one.
/// </para>
/// </summary>
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

    /// <summary>
    /// <b>The worst separation, and the case a Commander most needs them together.</b> A module
    /// that is unengineered and rank-gated has a Blocked blueprint and an Open effect — "has no
    /// experimental effect on it" is true of an unrolled module — so the two used to land in bands
    /// 3 and 0, at opposite ends of the project, with the effect at the very top as though it were
    /// the next thing to do.
    /// </summary>
    [Fact]
    public void AGatedUpgradeKeepsItsEffectBesideItRatherThanAtTheTop()
    {
        var document = Holding(
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Blocked),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open),
            Line("other", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Open));

        Assert.Equal(["other", "upgrade", "effect"], Keys(document));
    }

    /// <summary>
    /// The second half of the report. <c>Revise</c> rebuilds the sequence as
    /// <c>[untouched, kept, revived, opened, dropped]</c>, so an effect added to a module whose
    /// blueprint was already on the list lands at the bottom of the plan — with the whole rest of
    /// the build between it and the line it belongs to. Nothing chose that; it falls out of the
    /// diff order, and it is fixed in the reading rather than by touching <c>Revise</c>.
    /// </summary>
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
    /// The upgrade first, which is the order they are born in and the order the work happens in —
    /// an experimental effect does not exist without one.
    /// </summary>
    [Fact]
    public void TheUpgradeComesFirstEvenWhereTheFileHoldsThemTheOtherWayRound()
    {
        var document = Holding(
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open),
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Open));

        Assert.Equal(["upgrade", "effect"], Keys(document));
    }

    /// <summary>
    /// <b>The case the report says is already right, and must stay right.</b> An effect whose
    /// upgrade is done and gone from the list is a real and ordinary line on its own, and keeps its
    /// own place rather than being dragged anywhere.
    /// </summary>
    [Fact]
    public void AnEffectWithNoUpgradeOnTheListKeepsItsOwnPlace()
    {
        var document = Holding(
            Line("blocked", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Blocked),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open));

        Assert.Equal(["effect", "blocked"], Keys(document));
    }

    /// <summary>
    /// Two modules' pairs do not braid. The effect follows <em>its own</em> upgrade, which is what
    /// matching on ship and slot buys.
    /// </summary>
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

    /// <summary>
    /// A done upgrade still keeps its effect, and the pair sinks together. Splitting them here
    /// would put the effect at the top of the list as the next thing to do, which is the same
    /// failure the other way up.
    /// </summary>
    [Fact]
    public void ADoneUpgradeTakesItsOpenEffectDownWithIt()
    {
        var document = Holding(
            Line("upgrade", ChecklistIntentKind.Blueprint, "MainEngines", ChecklistState.Done),
            Line("effect", ChecklistIntentKind.Experimental, "MainEngines", ChecklistState.Open),
            Line("other", ChecklistIntentKind.Blueprint, "PowerPlant", ChecklistState.Open));

        Assert.Equal(["other", "upgrade", "effect"], Keys(document));
    }

    /// <summary>
    /// The kinship itself, since three mechanisms now share it. A line with no slot is not kin to
    /// everything else that also has none.
    /// </summary>
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
