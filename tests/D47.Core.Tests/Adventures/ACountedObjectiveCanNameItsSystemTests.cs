using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A map, signal or organic objective that names a system counts only events in it, and is written only where the system can supply the count.</summary>
public sealed class ACountedObjectiveCanNameItsSystemTests
{
    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    public static TheoryData<TriggerKind> Counted => new(TriggerKind.Map, TriggerKind.Signal, TriggerKind.Organic);

    private static AdventureStanding Fold(AdventureTrigger counted, IEnumerable<JournalEvent> events) =>
        events.Aggregate(
            AdventureFold.Start(LanternRoute(Accepted) with
            {
                Beats = [Beat("The Lantern", "setup", ToLantern, "Scoop here."), Beat("The Count", "catalyst", counted, "Done.")],
            }),
            AdventureFold.Apply);

    private static JournalEvent Happening(TriggerKind kind, long address, DateTimeOffset at) => kind switch
    {
        TriggerKind.Map =>
            Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"SAAScanComplete", "BodyName":"A 1", "SystemAddress":{{address}}, "BodyID":4, "ProbesUsed":5, "EfficiencyTarget":6 }"""),
        TriggerKind.Signal =>
            Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"SAASignalsFound", "BodyName":"A 1", "SystemAddress":{{address}}, "BodyID":4, "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2}] }"""),
        _ =>
            Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"ScanOrganic", "ScanType":"Analyse", "Genus":"$Codex_Ent_Aleoids_Genus_Name;", "Species":"$Codex_Ent_Aleoids_01_Name;", "SystemAddress":{{address}}, "Body":4 }"""),
    };

    [Theory]
    [MemberData(nameof(Counted))]
    public void AnEventInTheNamedSystemCounts(TriggerKind kind)
    {
        var counted = new AdventureTrigger { Kind = kind, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(counted, [Jump(Lantern, Accepted.AddMinutes(1)), Happening(kind, Hollow, Accepted.AddMinutes(2))]);

        Assert.True(standing.IsDone);
    }

    [Theory]
    [MemberData(nameof(Counted))]
    public void AnEventElsewhereDoesNotCount(TriggerKind kind)
    {
        var counted = new AdventureTrigger { Kind = kind, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(counted, [Jump(Lantern, Accepted.AddMinutes(1)), Happening(kind, Lantern, Accepted.AddMinutes(2))]);

        Assert.False(standing.IsDone);
    }

    [Theory]
    [MemberData(nameof(Counted))]
    public void AnEventAnywhereCountsWhenNoSystemIsNamed(TriggerKind kind)
    {
        var counted = new AdventureTrigger { Kind = kind, Count = 1 };

        var standing = Fold(counted, [Jump(Lantern, Accepted.AddMinutes(1)), Happening(kind, Hollow, Accepted.AddMinutes(2))]);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public void TheConfirmationSaysWhereWhenTheObjectiveNamesASystem()
    {
        Assert.Contains("in Epsilon Indi", TriggerConfirms.Of(TriggerKind.Map, null, false, "Epsilon Indi"));
        Assert.Contains("in Epsilon Indi", TriggerConfirms.Of(TriggerKind.Signal, null, false, "Epsilon Indi"));
        Assert.Contains("in Epsilon Indi", TriggerConfirms.Of(TriggerKind.Organic, null, false, "Epsilon Indi"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Map), TriggerConfirms.Of(TriggerKind.Map, null, false, null));
    }

    [Fact]
    public void TheHandOffSaysWhereToCount()
    {
        Assert.Equal("map 2 bodies in Epsilon Indi with the surface mapper", new AdventureTrigger { Kind = TriggerKind.Map, Count = 2, System = "Epsilon Indi" }.Describe());
    }
}
