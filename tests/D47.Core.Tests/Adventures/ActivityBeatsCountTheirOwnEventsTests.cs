using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class ActivityBeatsCountTheirOwnEventsTests
{
    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static AdventureStanding Fold(AdventureTrigger counted, params string[] events)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", ToLantern, "Scoop here."),
                Beat("The Count", "catalyst", counted, "Done."),
            ],
        };

        var standing = AdventureFold.Apply(
            AdventureFold.Start(adventure),
            Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""));

        return events.Select((json, index) => Event(json.Replace("@", Stamp(Accepted.AddMinutes(2 + index)), StringComparison.Ordinal)))
            .Aggregate(standing, AdventureFold.Apply);
    }

    private static AdventureTrigger Of(TriggerKind kind, int count = 1, string? filter = null, bool? organic = null) =>
        new() { Kind = kind, Count = count, Filter = filter, Organic = organic };

    private static string Analyse(string scanType) =>
        $$"""{ "timestamp":"@", "event":"ScanOrganic", "ScanType":"{{scanType}}", "Genus":"$Codex_Ent_Aleoids_Genus_Name;" }""";

    private static string Signals(params string[] types) =>
        $$"""{ "timestamp":"@", "event":"SAASignalsFound", "Signals":[{{string.Join(",", types.Select(type => $$"""{ "Type":"{{type}}", "Count":1 }"""))}}] }""";

    private static string Touchdown(string destination) =>
        $$"""{ "timestamp":"@", "event":"Touchdown", "NearestDestination":"{{destination}}" }""";

    private static string Cargo(string type) => $$"""{ "timestamp":"@", "event":"CollectCargo", "Type":"{{type}}" }""";

    [Fact]
    public void AnOrganicBeatCountsAnalysesAndNotLogsOrSamples()
    {
        var trigger = Of(TriggerKind.Organic, 3);

        Assert.Equal(2, Fold(trigger, Analyse("Log"), Analyse("Sample"), Analyse("Analyse"), Analyse("Analyse")).Counted);
        Assert.Equal(2, Fold(trigger, Analyse("Analyse"), Analyse("Analyse"), Analyse("Analyse")).Current);
    }

    [Fact]
    public void AGenusFilterIgnoresTheWrapping()
    {
        Assert.Equal(1, Fold(Of(TriggerKind.Organic, 2, "aleoids"), Analyse("Analyse")).Counted);
        Assert.Equal(0, Fold(Of(TriggerKind.Organic, 2, "Stratum"), Analyse("Analyse")).Counted);
    }

    [Fact]
    public void AThargoidSignalBeatFiresOnAThargoidSignalAndNotOnHumanAndGeological()
    {
        var trigger = Of(TriggerKind.Signal, 1, "Thargoid");

        Assert.Equal(1, Fold(trigger, Signals("$SAA_SignalType_Human;", "$SAA_SignalType_Geological;")).Current);
        Assert.Equal(2, Fold(trigger, Signals("$SAA_SignalType_Human;", "$SAA_SignalType_Thargoid;")).Current);
    }

    [Fact]
    public void ARingHotspotSignalIsMatchedWithoutRegardToCase()
    {
        Assert.Equal(2, Fold(Of(TriggerKind.Signal, 1, "Platinum"), Signals("Platinum")).Current);
        Assert.Equal(2, Fold(Of(TriggerKind.Signal, 1, "tritium"), Signals("Tritium")).Current);
        Assert.Equal(1, Fold(Of(TriggerKind.Signal, 1, "Platinum"), Signals("Tritium")).Current);
    }

    [Fact]
    public void AnUnfilteredWreckBeatIsAThargoidShipAndAFilteredOneIsItsType()
    {
        const string unknown = "$Settlement_Unflattened_WreckedUnknown:#index=1;";
        const string type9 = "$Settlement_Unflattened_WreckedType9:#index=1;";

        Assert.Equal(2, Fold(Of(TriggerKind.Wreck), Touchdown(unknown)).Current);
        Assert.Equal(1, Fold(Of(TriggerKind.Wreck), Touchdown(type9)).Current);
        Assert.Equal(2, Fold(Of(TriggerKind.Wreck, 1, "Type9"), Touchdown(type9)).Current);
        Assert.Equal(1, Fold(Of(TriggerKind.Wreck, 1, "Type9"), Touchdown(unknown)).Current);
    }

    [Fact]
    public void SalvageCountsBothSpellingsOfACargoType()
    {
        Assert.Equal(2, Fold(Of(TriggerKind.Salvage, 2, "OccupiedCryoPod"), Cargo("OccupiedCryoPod"), Cargo("occupiedcryopod")).Current);
    }

    [Fact]
    public void ADataSaleBeatAddsCartographicEarningsAndOrganicValueWithBonus()
    {
        var trigger = Of(TriggerKind.DataSale, 5_000_000);

        var standing = Fold(
            trigger,
            """{ "timestamp":"@", "event":"MultiSellExplorationData", "TotalEarnings":3200000 }""",
            """{ "timestamp":"@", "event":"SellOrganicData", "BioData":[{ "Value":1000000, "Bonus":500000 }, { "Value":300000, "Bonus":0 }] }""");

        Assert.Equal(2, standing.Current);

        var organicOnly = Fold(
            Of(TriggerKind.DataSale, 5_000_000, organic: true),
            """{ "timestamp":"@", "event":"MultiSellExplorationData", "TotalEarnings":3200000 }""",
            """{ "timestamp":"@", "event":"SellOrganicData", "BioData":[{ "Value":1000000, "Bonus":500000 }] }""");

        Assert.Equal(1_500_000, organicOnly.Counted);
    }

    [Fact]
    public void AnEngineerBeatAcceptsTheSingleFormTheStartupListAndALaterStage()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = "Tiana Fortune", Stage = "Invited" };

        Assert.Equal(2, Fold(trigger, """{ "timestamp":"@", "event":"EngineerProgress", "Engineer":"Tiana Fortune", "EngineerID":300270, "Progress":"Invited" }""").Current);
        Assert.Equal(2, Fold(trigger, """{ "timestamp":"@", "event":"EngineerProgress", "Engineers":[{ "Engineer":"Felicity Farseer", "Progress":"Unlocked" }, { "Engineer":"Tiana Fortune", "Progress":"Unlocked" }] }""").Current);
        Assert.Equal(1, Fold(trigger, """{ "timestamp":"@", "event":"EngineerProgress", "Engineer":"Tiana Fortune", "Progress":"Known" }""").Current);
        Assert.Equal(1, Fold(trigger, """{ "timestamp":"@", "event":"EngineerProgress", "Engineer":"Felicity Farseer", "Progress":"Unlocked" }""").Current);
    }

    [Fact]
    public void AnOnFootEngineerBeatFiresOnTheStartupListThatShowsTheStage()
    {
        var unlocked = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = "Domino Green", Stage = "Unlocked" };
        var invited = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = "Domino Green", Stage = "Invited" };
        const string invitedList = """{ "timestamp":"@", "event":"EngineerProgress", "Engineers":[{ "Engineer":"Domino Green", "EngineerID":400002, "Progress":"Invited" }, { "Engineer":"Hero Ferrari", "EngineerID":400004, "Progress":"Invited" }] }""";
        const string unlockedList = """{ "timestamp":"@", "event":"EngineerProgress", "Engineers":[{ "Engineer":"Domino Green", "EngineerID":400002, "Progress":"Unlocked", "RankProgress":0, "Rank":0 }] }""";
        const string listWithoutHer = """{ "timestamp":"@", "event":"EngineerProgress", "Engineers":[{ "Engineer":"Hero Ferrari", "EngineerID":400004, "Progress":"Invited" }] }""";

        Assert.Equal(1, Fold(unlocked, invitedList).Current);
        Assert.Equal(1, Fold(unlocked, listWithoutHer).Current);
        Assert.Equal(2, Fold(unlocked, invitedList, unlockedList).Current);
        Assert.Equal(2, Fold(invited, unlockedList).Current);
    }

    [Fact]
    public void AnEngineerBeatMayNameAnyEngineerInTheDirectory()
    {
        foreach (var engineer in D47.Core.Knowledge.EngineerDirectory.All)
        {
            var trigger = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = engineer.Name, Stage = "Unlocked" };

            Assert.Empty(AdventureValidation.EngineerProblems("Beat one", trigger));
        }

        Assert.Contains(D47.Core.Knowledge.EngineerDirectory.All, engineer => engineer.IsOnFoot);
    }

    [Fact]
    public void AnOnFootBeatNeedsAPlanetAndNotAStation()
    {
        var trigger = Of(TriggerKind.OnFoot, 1);

        Assert.Equal(1, Fold(trigger, """{ "timestamp":"@", "event":"Disembark", "OnStation":true, "OnPlanet":false }""").Current);
        Assert.Equal(2, Fold(trigger, """{ "timestamp":"@", "event":"Disembark", "OnStation":false, "OnPlanet":true, "SystemAddress":1, "BodyID":4 }""").Current);
    }

    [Fact]
    public void ACollectBeatCountsItemsAndMatchesTypeOrName()
    {
        var trigger = Of(TriggerKind.Collect, 5, "Component");

        Assert.Equal(3, Fold(trigger, """{ "timestamp":"@", "event":"CollectItems", "Name":"$x;", "Type":"Component", "Count":3 }""", """{ "timestamp":"@", "event":"CollectItems", "Name":"$y;", "Type":"Item", "Count":9 }""").Counted);
    }

    [Fact]
    public void EveryActivityBeatSurvivesTheStore()
    {
        var kinds = Enum.GetValues<TriggerKind>().Select(kind => kind.ToString().ToLowerInvariant());

        Assert.All(kinds, kind => Assert.Contains(kind, AdventureValidation.Kinds));
    }
}
