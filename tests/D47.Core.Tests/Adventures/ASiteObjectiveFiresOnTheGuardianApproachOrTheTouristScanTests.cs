using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class ASiteObjectiveFiresOnTheGuardianApproachOrTheTouristScanTests
{
    private const long System = 5069455938273;

    private const long Elsewhere = 3932277478106;

    private static readonly AdventureTrigger Guardian = new()
    {
        Kind = TriggerKind.Site, Filter = AdventureTrigger.GuardianSite, SystemAddress = System, BodyId = 6, System = "Synuefe XR-H d11-102", Body = "Synuefe XR-H d11-102 A 3",
    };

    private static readonly AdventureTrigger Tourist = new()
    {
        Kind = TriggerKind.Site, Filter = AdventureTrigger.TouristSite, SystemAddress = System, System = "Synuefe XR-H d11-102", SiteName = "Lantern's Rest",
    };

    private static readonly AdventureTrigger Generation = new()
    {
        Kind = TriggerKind.Site, Filter = AdventureTrigger.GenerationSite, SystemAddress = System, System = "Alaunus", SiteName = "Generation Ship Lycaon",
    };

    private static JournalEvent Drop(string type) =>
        Event($$"""{ "timestamp":"2026-10-20T12:00:00Z", "event":"SupercruiseDestinationDrop", "Type":"{{type}}", "Threat":0 }""");

    [Fact]
    public void ADropAtTheGenerationShipMeetsItInAnySystem()
    {
        Assert.True(AdventureFold.Matches(Generation, Drop("Generation Ship Lycaon")));
        Assert.True(AdventureFold.Matches(Generation, Drop("generation ship lycaon"), Elsewhere));
    }

    [Fact]
    public void ADropAtAnotherMegashipOrAShipUplinkScanOrABeaconScanDoesNotMeetIt()
    {
        Assert.False(AdventureFold.Matches(Generation, Drop("Generation Ship Argo")));
        Assert.False(AdventureFold.Matches(Generation, Drop("Cobra Wanderer")));
        Assert.False(AdventureFold.Matches(Generation, Scanned("$Datascan_ShipUplink;"), System));
        Assert.False(AdventureFold.Matches(Generation, Scanned("$Datascan_TouristBeacon;"), System));
    }

    [Fact]
    public void AGenerationSiteNeedsItsSystemAndShipAndIsDescribedInWords()
    {
        Assert.True(Generation.IsResolved);
        Assert.False((Generation with { SiteName = null }).IsResolved);
        Assert.False((Generation with { SystemAddress = null }).IsResolved);
        Assert.Equal("drop out at Generation Ship Lycaon in Alaunus", Generation.Describe());
    }

    [Fact]
    public void AGenerationSiteSurvivesTheStore()
    {
        var path = Path.Combine(MemoryInstall.FakeRoot, "d47-site", "generation.json");
        var files = new MemoryFileSystem();
        var adventure = new Adventure
        {
            Key = "generation-chapter",
            Name = "Generation Chapter",
            Source = AdventureSource.Generated,
            Beats = [new AdventureBeat { Title = "The Ship", Function = "setup", Trigger = Generation, Lines = [new AdventureLine { Text = "Go there." }] }],
        };

        Assert.Null(new AdventureStore(path, files, NullLogger<AdventureStore>.Instance).Save("F1", adventure));

        var store = new AdventureStore(path, files, NullLogger<AdventureStore>.Instance);
        store.Poll();

        Assert.Equal(Generation, store.Find("F1", "generation-chapter")!.Beats[0].Trigger);
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
        return parsed!;
    }

    private static JournalEvent Approach(string name, long system = System, int body = 6) =>
        Event($$"""{ "timestamp":"2026-10-20T12:00:00Z", "event":"ApproachSettlement", "Name":"{{name}}", "SystemAddress":{{system}}, "BodyID":{{body}} }""");

    private static JournalEvent Scanned(string type) =>
        Event($$"""{ "timestamp":"2026-10-20T12:00:00Z", "event":"DataScanned", "Type":"{{type}}" }""");

    [Theory]
    [InlineData("$Ancient:#index=1;")]
    [InlineData("$Ancient_Small_005:#index=1;")]
    public void AnApproachToAGuardianSiteOnTheBodyMeetsIt(string name) =>
        Assert.True(AdventureFold.Matches(Guardian, Approach(name)));

    [Fact]
    public void AnApproachOnAnotherBodyOrSystemOrToAnotherSettlementDoesNot()
    {
        Assert.False(AdventureFold.Matches(Guardian, Approach("$Ancient:#index=1;", body: 7)));
        Assert.False(AdventureFold.Matches(Guardian, Approach("$Ancient:#index=1;", system: Elsewhere)));
        Assert.False(AdventureFold.Matches(Guardian, Approach("$Ouroboros_Mining_Settlement:#index=1;")));
    }

    [Fact]
    public void ATouristBeaconScanInTheSystemMeetsItAndElsewhereDoesNot()
    {
        var scan = Scanned("$Datascan_TouristBeacon;");

        Assert.True(AdventureFold.Matches(Tourist, scan, System));
        Assert.False(AdventureFold.Matches(Tourist, scan, Elsewhere));
        Assert.False(AdventureFold.Matches(Tourist, scan));
    }

    [Fact]
    public void AnotherDataScanInTheSystemDoesNotMeetATouristSite()
    {
        Assert.False(AdventureFold.Matches(Tourist, Scanned("$Datascan_DataPoint;"), System));
        Assert.False(AdventureFold.Matches(Tourist, Scanned("$Datascan_AncientPylon;"), System));
    }

    [Fact]
    public void ASiteNeedsItsPlaceAndAKnownType()
    {
        Assert.True(Guardian.IsResolved);
        Assert.True(Tourist.IsResolved);
        Assert.False((Guardian with { BodyId = null }).IsResolved);
        Assert.False((Tourist with { SystemAddress = null }).IsResolved);
        Assert.False((Guardian with { Filter = "pylon" }).IsResolved);
        Assert.False((Guardian with { Filter = null }).IsResolved);
    }

    [Fact]
    public void ASiteIsNeitherCountedNorAuthorable()
    {
        Assert.False(AdventureTrigger.IsCountedKind(TriggerKind.Site));
        Assert.False(AdventureTrigger.IsAuthorable(TriggerKind.Site));
    }

    [Fact]
    public void ASiteIsDescribedAndHandedOffInWords()
    {
        Assert.Equal("fly to the Guardian site on Synuefe XR-H d11-102 A 3 in Synuefe XR-H d11-102", Guardian.Describe());
        Assert.Equal("scan a tourist beacon in Synuefe XR-H d11-102", Tourist.Describe());
        Assert.Equal("Next: scan a tourist beacon in Synuefe XR-H d11-102 with the ship's data-link scanner.", Tourist.HandOff());
        Assert.Equal("scan a tourist beacon in Synuefe XR-H d11-102", (Tourist with { SiteName = "Balise de visite de Synuefe" }).Describe());
        Assert.StartsWith("Next: fly to the Guardian site", Guardian.HandOff());
        Assert.DoesNotContain("data-link", Guardian.HandOff());
    }

    [Fact]
    public void ASiteObjectiveSurvivesTheStore()
    {
        var path = Path.Combine(MemoryInstall.FakeRoot, "d47-site", "adventures.json");
        var files = new MemoryFileSystem();
        var adventure = new Adventure
        {
            Key = "site-chapter",
            Name = "Site Chapter",
            Source = AdventureSource.Generated,
            Beats =
            [
                new AdventureBeat { Title = "The Ruins", Function = "setup", Trigger = Guardian, Lines = [new AdventureLine { Text = "Go there." }] },
                new AdventureBeat { Title = "The Beacon", Function = "resolution", Trigger = Tourist, Lines = [new AdventureLine { Text = "Scan it." }] },
            ],
        };

        Assert.Null(new AdventureStore(path, files, NullLogger<AdventureStore>.Instance).Save("F1", adventure));

        var store = new AdventureStore(path, files, NullLogger<AdventureStore>.Instance);
        store.Poll();
        var reopened = store.Find("F1", "site-chapter")!;

        Assert.Equal(Guardian, reopened.Beats[0].Trigger);
        Assert.Equal(Tourist, reopened.Beats[1].Trigger);
    }
}
