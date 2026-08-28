using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The reporting surface over the depot fold (Phase 18, "Colonisation and construction
/// tracking"): what a site still wants, what is already aboard, and the two things d47 must refuse
/// to say — that the figures are live, and what is on the carrier.
/// </summary>
public class ColonisationCapabilityTests
{
    private static GameStateStore Store(params string[] lines)
    {
        var gameState = new GameStateStore();

        gameState.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var line in lines)
        {
            gameState.Apply(Parse(line));
        }

        return gameState;
    }

    /// <summary>
    /// The same, but through a real <see cref="JournalSpine"/> over a folder holding a real
    /// <c>Cargo.json</c> — because the hold is the one part of this that does not come from an
    /// event, and a test that assigned it directly would not exercise the path that reads it.
    /// </summary>
    private static GameStateStore StoreWithHold(TempInstall install, string manifest, params string[] lines)
    {
        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-08-16T090000.01.log"),
            [
                """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                .. lines.Select(line => line.ReplaceLineEndings(" ")),
            ]);

        File.WriteAllText(Path.Combine(install.Root, CargoManifestReader.ManifestFile), manifest);

        var gameState = new GameStateStore();
        new JournalSpine(install.Root, gameState, NullLoggerFactory.Instance).Poll();

        return gameState;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Task<ToolResult> Ask(GameStateStore gameState, string tool, string json = "{}") =>
        CapabilityRegistry
            .Build([ColonisationCapability.Create(() => gameState.Active)])
            .InvokeAsync(tool, ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

    private const string Docked =
        """
        {"timestamp":"2026-08-16T09:30:00Z","event":"Docked","StationName":"Ratraii Construction Site",
         "StarSystem":"Ratraii","MarketID":3960809986}
        """;

    /// <summary>A site wanting two things, one of them part-delivered.</summary>
    private const string Depot =
        """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
          "MarketID":3960809986, "ConstructionProgress":0.25,
          "ConstructionComplete":false, "ConstructionFailed":false,
          "ResourcesRequired":[
            { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
              "RequiredAmount":500, "ProvidedAmount":100, "Payment":3239 },
            { "Name":"$computercomponents_name;", "Name_Localised":"Computer Components",
              "RequiredAmount":200, "ProvidedAmount":200, "Payment":5000 } ] }
        """;

    [Fact]
    public async Task WithNoJournalDetectedTheAnswerSaysSoRatherThanGuessing()
    {
        var result = await Ask(new GameStateStore(), "get_construction_sites");

        Assert.False(result.IsError);
        Assert.Contains("No Elite Dangerous journal", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two silences with different next actions: "you have not visited one yet" sends the Commander
    /// somewhere, and "everything I know of is finished" does not.
    /// </summary>
    [Fact]
    public async Task NeverHavingSeenASiteIsSaidDifferentlyFromHavingNoneOpen()
    {
        var never = await Ask(Store(), "get_construction_sites");
        Assert.Contains("while you are docked at it", never.Content, StringComparison.Ordinal);

        var finished = await Ask(
            Store(
                Docked,
                """
                { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
                  "MarketID":3960809986, "ConstructionProgress":1.0, "ConstructionComplete":true,
                  "ConstructionFailed":false, "ResourcesRequired":[] }
                """),
            "get_construction_sites");

        Assert.Contains("complete or failed", finished.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASiteReportsWhereItIsHowFarAlongAndWhatIsLeft()
    {
        var result = await Ask(Store(Docked, Depot), "get_construction_sites");

        Assert.Contains("Ratraii Construction Site, Ratraii", result.Content, StringComparison.Ordinal);
        Assert.Contains("25% built", result.Content, StringComparison.Ordinal);

        // One of the two rows is met, so it is not outstanding — and the tonnage is the remainder
        // of the other, never the total.
        Assert.Contains("1 of 2 commodities outstanding", result.Content, StringComparison.Ordinal);
        Assert.Contains("400 tonnes left", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The caveat that stops an exact number being read as a live one. It is the failure this
    /// capability is most exposed to: the arithmetic is right and the moment it describes has passed.
    /// </summary>
    [Fact]
    public async Task EveryAnswerSaysTheFiguresAreFromTheLastVisit()
    {
        foreach (var tool in new[] { "get_construction_sites", "get_construction_needs" })
        {
            var result = await Ask(Store(Docked, Depot), tool);

            Assert.Contains("only while you are docked at it", result.Content, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The join, end to end: the depot writes <c>$aluminium_name;</c> and the hold writes
    /// <c>aluminium</c>. If these missed each other the shortfall would be reported in full against
    /// a hold that already has some of it, and it would look right.
    /// </summary>
    [Fact]
    public async Task WhatIsAlreadyInTheHoldIsNettedOffTheShortfall()
    {
        using var install = new TempInstall();

        var gameState = StoreWithHold(
            install,
            """
            { "event":"Cargo", "Vessel":"Ship", "Count":150,
              "Inventory":[ { "Name":"aluminium", "Count":150, "Stolen":0 } ] }
            """,
            Docked,
            Depot);

        var result = await Ask(gameState, "get_construction_needs");

        Assert.Contains("150 tonnes in the hold", result.Content, StringComparison.Ordinal);
        Assert.Contains("leaving 250 tonnes to find", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Twenty aboard against six wanted is six tonnes of progress, not twenty. Uncapped, the line
    /// reads "20 in the hold" beside "6 left" and invites a second trip that buys nothing.
    /// </summary>
    [Fact]
    public async Task CarryingMoreThanIsWantedCountsOnlyWhatIsWanted()
    {
        using var install = new TempInstall();

        var gameState = StoreWithHold(
            install,
            """
            { "event":"Cargo", "Vessel":"Ship", "Count":900,
              "Inventory":[ { "Name":"aluminium", "Count":900, "Stolen":0 } ] }
            """,
            Docked,
            Depot);

        var result = await Ask(gameState, "get_construction_needs");

        Assert.Contains("all of it in the hold", result.Content, StringComparison.Ordinal);
        Assert.Contains("leaving 0 tonnes to find", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("900 tonnes in the hold", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheNumberOfRunsComesOffTheShipsOwnCapacity()
    {
        var gameState = Store(
            Docked,
            Depot,
            """
            {"timestamp":"2026-08-16T09:00:00Z","event":"Loadout","Ship":"type9","ShipID":7,
             "CargoCapacity":128,"Modules":[]}
            """);

        var result = await Ask(gameState, "get_construction_needs");

        // 400 outstanding, nothing aboard, 128 a run.
        Assert.Contains("At 128 tonnes a run, that is 4 more full loads", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The measured refusal. Elite writes a carrier cargo tonnage and nothing that says what those
    /// tonnes are, and deriving it from <c>CargoTransfer</c> was wrong 679 times against 347 right.
    /// So the total is quoted and the manifest is declined out loud.
    /// </summary>
    [Fact]
    public async Task TheCarrierIsATonnageAndTheRefusalToItemiseIsSaidOutLoud()
    {
        var result = await Ask(
            Store(
                Docked,
                Depot,
                """
                {"timestamp":"2026-08-16T09:45:00Z","event":"CarrierStats","CarrierID":3712682240,
                 "Callsign":"B0X-79X","Name":"GDS PREDATOR","DockingAccess":"all","FuelLevel":500,
                 "SpaceUsage":{"TotalCapacity":25000,"Crew":0,"Cargo":656,"CargoSpaceReserved":0,
                 "ShipPacks":0,"ModulePacks":0,"FreeSpace":24344}}
                """),
            "get_construction_needs");

        Assert.Contains("carrier was holding 656 tonnes", result.Content, StringComparison.Ordinal);
        Assert.Contains("does not write what those tonnes are", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The depot's delivered figures are everybody's; a Commander asking "did my run land" wants
    /// theirs. The event that says so writes mixed case on every symbol, which is the whole reason
    /// the fold lowercases.
    /// </summary>
    [Fact]
    public async Task YourOwnDeliveriesAreReportedApartFromEverybodyElses()
    {
        var result = await Ask(
            Store(
                Docked,
                Depot,
                """
                { "timestamp":"2026-08-16T10:30:00Z", "event":"ColonisationContribution",
                  "MarketID":3960809986, "Contributions":[
                    { "Name":"$Aluminium_name;", "Name_Localised":"Aluminium", "Amount":60 } ] }
                """),
            "get_construction_needs");

        Assert.Contains("You have delivered 60 tonnes here", result.Content, StringComparison.Ordinal);
        Assert.Contains("figures above are everybody's", result.Content, StringComparison.Ordinal);
    }

    /// <summary>Several sites can be open at once — measured, three at a time — so this asks rather than picks.</summary>
    [Fact]
    public async Task WithSeveralSitesOpenItAsksWhichRatherThanChoosing()
    {
        var gameState = Store(
            Docked,
            Depot,
            """
            {"timestamp":"2026-08-16T11:00:00Z","event":"Docked","StationName":"Second Site",
             "StarSystem":"Hyades Sector","MarketID":3959687426}
            """,
            """
            { "timestamp":"2026-08-16T11:01:00Z", "event":"ColonisationConstructionDepot",
              "MarketID":3959687426, "ConstructionProgress":0.1, "ConstructionComplete":false,
              "ConstructionFailed":false, "ResourcesRequired":[
                { "Name":"$steel_name;", "Name_Localised":"Steel", "RequiredAmount":100,
                  "ProvidedAmount":0, "Payment":1 } ] }
            """);

        var asked = await Ask(gameState, "get_construction_needs");
        Assert.Contains("Which one?", asked.Content, StringComparison.Ordinal);

        // And it answers when told which, matched on the station the Commander would say.
        var named = await Ask(gameState, "get_construction_needs", """{"site":"Second Site"}""");
        Assert.Contains("Steel", named.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Aluminium", named.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownSiteNameListsTheOnesItDoesKnow()
    {
        var result = await Ask(Store(Docked, Depot), "get_construction_needs", """{"site":"Sol"}""");

        Assert.Contains("no construction site called \"Sol\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("Ratraii Construction Site", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASiteWithNothingOutstandingSaysSoRatherThanListingNothing()
    {
        var result = await Ask(
            Store(
                Docked,
                """
                { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
                  "MarketID":3960809986, "ConstructionProgress":0.9, "ConstructionComplete":false,
                  "ConstructionFailed":false, "ResourcesRequired":[
                    { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
                      "RequiredAmount":500, "ProvidedAmount":500, "Payment":1 } ] }
                """),
            "get_construction_needs");

        Assert.Contains("Nothing outstanding on the manifest", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A finished site is excluded by default because it cannot be hauled to — and the flag is what
    /// says finished, never "the events stopped": a completed site keeps reporting, 2 to 60 more
    /// times in the corpus.
    /// </summary>
    [Fact]
    public async Task FinishedSitesAreOutOfTheWayButStillAskableFor()
    {
        var gameState = Store(
            Docked,
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
              "MarketID":3960809986, "ConstructionProgress":1.0, "ConstructionComplete":true,
              "ConstructionFailed":false, "ResourcesRequired":[] }
            """);

        var hidden = await Ask(gameState, "get_construction_sites");
        Assert.DoesNotContain("Ratraii Construction Site", hidden.Content, StringComparison.Ordinal);

        var shown = await Ask(gameState, "get_construction_sites", """{"include_finished":true}""");
        Assert.Contains("Ratraii Construction Site", shown.Content, StringComparison.Ordinal);
        Assert.Contains("complete", shown.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no hold read, the shortfall must not silently claim nothing is aboard — an unread hold
    /// and an empty one are different facts.
    /// </summary>
    [Fact]
    public async Task AnUnreadHoldIsSaidRatherThanTreatedAsEmpty()
    {
        var result = await Ask(Store(Docked, Depot), "get_construction_needs");

        Assert.Contains("have not read your cargo hold yet", result.Content, StringComparison.Ordinal);
    }
}
