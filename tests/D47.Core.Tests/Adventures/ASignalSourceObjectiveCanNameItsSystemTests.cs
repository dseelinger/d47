using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A signal-source objective that names a system fires only on a drop made while the ship is in it.</summary>
public sealed class ASignalSourceObjectiveCanNameItsSystemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private const string NothingObjected = """{"objections": []}""";

    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static AdventureStanding Fold(AdventureTrigger uss, IEnumerable<JournalEvent> events) =>
        events.Aggregate(
            AdventureFold.Start(LanternRoute(Accepted) with
            {
                Beats = [Beat("The Lantern", "setup", ToLantern, "Scoop here."), Beat("The Drop", "catalyst", uss, "Done.")],
            }),
            AdventureFold.Apply);

    private static JournalEvent Drop(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"USSDrop", "USSType":"$USS_Type_Salvage;", "USSThreat":0 }""");

    [Fact]
    public void ADropAfterArrivingInTheNamedSystemFires()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Hollow, Accepted.AddMinutes(2)), Drop(Accepted.AddMinutes(3))]);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public void ADropAfterArrivingElsewhereDoesNotFire()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Drop(Accepted.AddMinutes(2))]);

        Assert.False(standing.IsDone);
    }

    [Fact]
    public void ADropAfterLeavingTheNamedSystemDoesNotFire()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Hollow, Accepted.AddMinutes(2)), Jump(Lantern, Accepted.AddMinutes(3)), Drop(Accepted.AddMinutes(4))]);

        Assert.False(standing.IsDone);
    }

    [Fact]
    public void ADropWithNoSystemNamedFiresAnywhere()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1 };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Hollow, Accepted.AddMinutes(2)), Drop(Accepted.AddMinutes(3))]);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public async Task ADropInAPlannedChapterNamesNoSystem()
    {
        var story = PlannedChapterWorld.Story with { Slots = [new DrawnActivity(TriggerKind.Uss, null, 1)] };
        var plan = await PlannedChapterWorld.PlanAsync(PlannedChapterWorld.Ask(story));

        var drop = plan.Objectives.SelectMany(objective => objective.Steps).Single(step => step.Kind == TriggerKind.Uss);

        Assert.Null(drop.System);
        Assert.Null(drop.SystemAddress);
    }

    [Fact]
    public void TheConfirmationSaysWhereTheDropWasWhenTheObjectiveNamesASystem()
    {
        Assert.Equal(
            "the ship dropped into signal sources in Epsilon Indi — not who sent the signal or what was found there",
            TriggerConfirms.Of(TriggerKind.Uss, null, false, "Epsilon Indi"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Uss), TriggerConfirms.Of(TriggerKind.Uss, null, false, null));
    }

    [Fact]
    public void TheHandOffSaysWhereToDrop()
    {
        Assert.Equal("drop into 2 signal sources in Epsilon Indi", new AdventureTrigger { Kind = TriggerKind.Uss, Count = 2, System = "Epsilon Indi" }.Describe());
    }
}
