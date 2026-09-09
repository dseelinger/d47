using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Systems that might be running High Grade Emissions.</summary>
public class EmissionCalloutTests
{
    private static JournalEvent Arrival(
        string system,
        long population,
        string allegiance,
        params string[] states)
    {
        var controlling = $"Ruling party of {system}";

        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-21T09:00:00Z",
            ["event"] = "FSDJump",
            ["StarSystem"] = system,
            ["Population"] = population,
            ["SystemAllegiance"] = allegiance,
            ["SystemFaction"] = new Dictionary<string, object?> { ["Name"] = controlling },
            ["Factions"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["Name"] = controlling,
                    ["Allegiance"] = allegiance,
                    ["FactionState"] = states.FirstOrDefault() ?? "None",
                    ["ActiveStates"] = states
                        .Select(state => new Dictionary<string, object?> { ["State"] = state })
                        .ToArray(),
                },
                new Dictionary<string, object?>
                {
                    ["Name"] = $"Federal minority of {system}",
                    ["Allegiance"] = "Federation",
                    ["FactionState"] = "Boom",
                },
            },
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static IReadOnlyList<Announcement> Heard(JournalEvent arrival) =>
        [.. new EmissionCallout().Examine(Context(Commander(), arrival))];

    private static CommanderGameState Commander() => new(new CommanderIdentity("F1", "Fixture"));

    private static CalloutContext Context(CommanderGameState state, params JournalEvent[] events) =>
        new(DateTimeOffset.UnixEpoch, IsPriming: false, state, GameStatus.Unknown, NavRoute.None, events);

    private static string Said(EmissionCallout callout, CommanderGameState state, JournalEvent arrival) =>
        Assert.Single(callout.Examine(Context(state, arrival))).Text;

    // ------------------------------------------------------------- the conditions

    [Fact]
    public void AnIndependentFactionInBoomOffersTheProtoMaterials()
    {
        var said = Said(
            new EmissionCallout(),
            Commander(),
            Arrival("Deciat", 5_000_000, "Independent", "Boom"));

        Assert.Contains("Proto Heat Radiators", said, StringComparison.Ordinal);
        Assert.Contains("Proto Light Alloys", said, StringComparison.Ordinal);
        Assert.Contains("Proto Radiolic Alloys", said, StringComparison.Ordinal);
    }

    /// <summary>Superpower beats state, and never the other way.</summary>
    [Fact]
    public void AnImperialFactionInOutbreakStillOnlyOffersShielding()
    {
        var said = Said(
            new EmissionCallout(),
            Commander(),
            Arrival("Cubeo", 20_000_000, "Empire", "Outbreak"));

        Assert.Contains("Imperial Shielding", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Pharmaceutical Isolators", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proprietary Composites rides with Core Dynamics Composites — the wiki lists both for Federal
    /// space and the 2017 guide lists only the one.
    /// </summary>
    [Fact]
    public void AFederalSystemOffersBothComposites()
    {
        var said = Said(
            new EmissionCallout(),
            Commander(),
            Arrival("Sol", 22_000_000, "Federation"));

        Assert.Contains("Core Dynamics Composites", said, StringComparison.Ordinal);
        Assert.Contains("Proprietary Composites", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reported defect, in the system it was reported in (2026-08-21): "Oppi could be running high
    /// grade emissions for Core Dynamics Composites.
    /// </summary>
    [Fact]
    public void OppiSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Oppi", 4_626_551, "Independent"))));
    }

    /// <summary>
    /// The case the Commander asked for by name — *"not just Core Dynamics Composites plus the related
    /// one … but when completely different ones are there"*.
    /// </summary>
    [Fact]
    public void AControllingFactionInTwoStatesOffersTwoUnrelatedGroups()
    {
        var said = Said(
            new EmissionCallout(),
            Commander(),
            Arrival("Shinrarta Dezhra", 85_000_000, "Independent", "CivilUnrest", "Expansion"));

        Assert.Contains("Improvised Components", said, StringComparison.Ordinal);
        Assert.Contains("Proto Radiolic Alloys", said, StringComparison.Ordinal);
    }

    /// <summary>Expansion counts, beside Boom.</summary>
    [Fact]
    public void ExpansionOffersTheProtoMaterialsToo()
    {
        var said = Said(new EmissionCallout(), Commander(), Arrival("Alioth", 8_000_000, "Independent", "Expansion"));

        Assert.Contains("Proto Heat Radiators", said, StringComparison.Ordinal);
        Assert.Contains("Proto Light Alloys", said, StringComparison.Ordinal);
        Assert.Contains("Proto Radiolic Alloys", said, StringComparison.Ordinal);
    }

    /// <summary>Alliance yields nothing at all.</summary>
    [Fact]
    public void AnAllianceSystemOffersNothingWhateverItsState()
    {
        Assert.Empty(Heard(Arrival("Alioth", 8_000_000, "Alliance", "Boom")));
        Assert.Empty(Heard(Arrival("Alioth", 8_000_000, "Alliance", "Outbreak")));
    }

    /// <summary>Every remaining row of the Commander's table, by name.</summary>
    [Theory]
    [InlineData("CivilUnrest", "Improvised Components")]
    [InlineData("War", "Military Grade Alloys")]
    [InlineData("CivilWar", "Military Supercapacitors")]
    [InlineData("Outbreak", "Pharmaceutical Isolators")]
    public void AnIndependentSystemOffersWhatItsControllingFactionsStateSays(string state, string material)
    {
        Assert.Contains(
            material,
            Said(new EmissionCallout(), Commander(), Arrival("Deciat", 5_000_000, "Independent", state)),
            StringComparison.Ordinal);
    }

    /// <summary>The state comes from the <c>Factions</c> array, not from <c>SystemFaction</c>.</summary>
    [Fact]
    public void AControllingFactionWithNoHeadlineStateOnSystemFactionIsStillRead()
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-21T09:00:00Z",
            ["event"] = "FSDJump",
            ["StarSystem"] = "Deciat",
            ["Population"] = 5_000_000,
            ["SystemAllegiance"] = "Independent",

            // Name only, which is how two jumps in five arrive.
            ["SystemFaction"] = new Dictionary<string, object?> { ["Name"] = "Deciat Blue Society" },
            ["Factions"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["Name"] = "Deciat Blue Society",
                    ["Allegiance"] = "Independent",
                    ["FactionState"] = "Outbreak",
                },
            },
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));

        Assert.Contains(
            "Pharmaceutical Isolators",
            Said(new EmissionCallout(), Commander(), parsed!),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// And the other direction of the table answers for every material, which is what
    /// <c>find_material</c> asks rather than deriving conditions of its own.
    /// </summary>
    [Fact]
    public void EveryEmissionMaterialResolvesBackToItsGroup()
    {
        foreach (var group in EmissionRules.Groups)
        {
            foreach (var symbol in group.Materials)
            {
                Assert.Same(group, EmissionRules.Holding(symbol));
            }
        }

        Assert.Null(EmissionRules.Holding("iron"));
        Assert.Null(EmissionRules.Holding(null));
    }

    /// <summary>The population floor, applied to every group rather than to Outbreak alone.</summary>
    [Fact]
    public void ASparselyPopulatedSystemSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Hyades Sector EG-X c1-8", 227_781, "Independent", "Boom"))));
    }

    [Fact]
    public void AFactionInNoUsefulStateSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Deciat", 5_000_000, "Independent"))));
    }

    // ------------------------------------------------------------------ the filter

    /// <summary>What there is no room for is not mentioned.</summary>
    [Fact]
    public void AMaterialAlreadyFullIsLeftOut()
    {
        var state = Commander();

        state.Apply(Collected(100, "protoheatradiators"));

        var said = Said(
            new EmissionCallout { Capacity = _ => 100 },
            state,
            Arrival("Deciat", 5_000_000, "Independent", "Boom"));

        Assert.DoesNotContain("Proto Heat Radiators", said, StringComparison.Ordinal);
        Assert.Contains("Proto Radiolic Alloys", said, StringComparison.Ordinal);
    }

    /// <summary>And a group entirely full says nothing at all rather than an empty sentence.</summary>
    [Fact]
    public void AGroupEntirelyFullIsSilent()
    {
        var state = Commander();

        // One event, not three: `Materials` is a whole-inventory snapshot, so three of them in a row leaves
        // only the last one's holding standing.
        state.Apply(Collected(100, "protoheatradiators", "protolightalloys", "protoradiolicalloys"));

        Assert.Empty(new EmissionCallout { Capacity = _ => 100 }.Examine(Context(
            state,
            Arrival("Deciat", 5_000_000, "Independent", "Boom"))));
    }

    /// <summary>An unknown capacity means say it.</summary>
    [Fact]
    public void AnUnknownCapacityIsNotTreatedAsFull()
    {
        var state = Commander();

        state.Apply(Collected(100, "protoheatradiators"));

        Assert.Contains(
            "Proto Heat Radiators",
            Said(new EmissionCallout(), state, Arrival("Deciat", 5_000_000, "Independent", "Boom")),
            StringComparison.Ordinal);
    }

 // --------------------------------------------------------- the headroom it says

 /// <summary>Each material named with the room left, with the Commander's own holdings.</summary>
    [Fact]
    public void EachMaterialIsNamedWithTheRoomLeftForIt()
    {
        var state = Commander();

        state.Apply(Held(
            ("protoheatradiators", 95),
            ("protolightalloys", 114),
            ("protoradiolicalloys", 80)));

        var said = Said(
            new EmissionCallout { Capacity = Capacities },
            state,
            Arrival("Sharru Sector GM-V b2-1", 5_000_000, "Independent", "Boom"));

        Assert.Contains("Proto Heat Radiators, 5 short", said, StringComparison.Ordinal);
        Assert.Contains("Proto Light Alloys, 36 short", said, StringComparison.Ordinal);
        Assert.Contains("Proto Radiolic Alloys, 20 short", said, StringComparison.Ordinal);
    }

    /// <summary>The number and the filter cannot disagree, asserted rather than reasoned about.</summary>
    [Fact]
    public void CollectingTheRoomItNamesIsExactlyWhatDropsTheMaterial()
    {
        IReadOnlyList<Announcement> Heard(int held)
        {
            var state = Commander();
            state.Apply(Held(("protoheatradiators", held)));

            return [.. new EmissionCallout { Capacity = Capacities }.Examine(
                Context(state, Arrival("Deciat", 5_000_000, "Independent", "Boom")))];
        }

        Assert.Contains("Proto Heat Radiators, 5 short", Heard(95)[0].Text, StringComparison.Ordinal);
        Assert.Contains("Proto Heat Radiators, 1 short", Heard(99)[0].Text, StringComparison.Ordinal);

        // At the capacity the line named, it is gone entirely rather than said as nothing left.
        Assert.DoesNotContain("Proto Heat Radiators", Heard(100)[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AMaterialWhoseCapacityIsUnknownIsNamedWithNoNumber()
    {
        var said = Said(
            new EmissionCallout(),
            Commander(),
            Arrival("Deciat", 5_000_000, "Independent", "Boom"));

        Assert.Contains("Proto Heat Radiators, Proto Light Alloys and Proto Radiolic Alloys", said, StringComparison.Ordinal);
        Assert.DoesNotContain("short", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// No near-full threshold ships, which the issue asked to be taken as a decision rather than left
    /// to drift.
    /// </summary>
    [Fact]
    public void ANearlyFullMaterialIsStillSaidAndSaysHowNearlyFull()
    {
        var state = Commander();

        state.Apply(Held(("protoheatradiators", 99)));

        Assert.Contains(
            "Proto Heat Radiators, 1 short",
            Said(new EmissionCallout { Capacity = Capacities }, state, Arrival("Deciat", 5_000_000, "Independent", "Boom")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The grade capacities for the three Proto materials, which is the mapping
    /// <c>MaterialGrades.CapacityOf</c> supplies in the app: grade 5 holds 100 and grade 4 holds 150.
    /// </summary>
    private static int? Capacities(string symbol) => symbol.ToLowerInvariant() switch
    {
        "protoheatradiators" => 100,
        "protoradiolicalloys" => 100,
        "protolightalloys" => 150,
        _ => null,
    };

    /// <summary>A whole-inventory snapshot with a different count per material, which <see cref="Collected"/> cannot express.</summary>
    private static JournalEvent Held(params (string Symbol, int Count)[] holdings)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-27T09:00:00Z",
            ["event"] = "Materials",
            ["Manufactured"] = holdings
                .Select(holding => new Dictionary<string, object?>
                {
                    ["Name"] = holding.Symbol,
                    ["Count"] = holding.Count,
                })
                .ToArray(),
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

 // ------------------------------------------- what the ship can actually collect

    /// <summary>
    /// A Commander flying <paramref name="module"/> and carrying <paramref name="limpets"/> — null for
    /// a hold that has never been read.
    /// </summary>
    private static CommanderGameState Flying(string module, int? limpets, string vessel = "Ship")
    {
        var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-05T170000.01.log"),
            [
                """{"timestamp":"2026-09-05T17:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                $$"""{"timestamp":"2026-09-05T17:00:01Z","event":"Loadout","Ship":"python","ShipID":7,"CargoCapacity":256,"Modules":[{"Slot":"Slot03_Size6","Item":"{{module}}","On":true,"Health":1.0}]}""",
            ]);

        if (limpets is { } aboard)
        {
            var inventory = aboard == 0
                ? ""
                : $$"""{ "Name":"drones", "Name_Localised":"Limpet", "Count":{{aboard}}, "Stolen":0 }""";

            File.WriteAllText(
                Path.Combine(install.Root, CargoManifestReader.ManifestFile),
                $$"""{ "timestamp":"2026-09-05T17:00:11Z", "event":"Cargo", "Vessel":"{{vessel}}", "Count":{{aboard}}, "Inventory":[ {{inventory}} ] }""");
        }

        var store = new GameStateStore();
        new JournalSpine(install.Root, store, NullLoggerFactory.Instance).Poll();

        return store.Active!;
    }

    private const string Collector = "int_dronecontrol_collection_size3_class5";

    /// <summary>
    /// The materials come out by limpet or not at all, so a ship with no Collector Limpet Controller
    /// hears nothing — the same "costs attention, cannot be acted on" rule the full hold already gets,
    /// applied to the other end of the same act.
    /// </summary>
    [Theory]
    [InlineData("int_cargorack_size6_class1")]
    [InlineData("int_dronecontrol_fueltransfer_size3_class5")]
    public void AShipThatCannotSendALimpetIsNotToldAboutEmissions(string module)
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Flying(module, limpets: 200),
            Arrival("Deciat", 5_000_000, "Independent", "Boom"))));
    }

    /// <summary>
    /// An unread loadout is not a ship without a controller. d47 joined this session in flight and
    /// knows nothing about the ship; silencing on that would silence the whole session.
    /// </summary>
    [Fact]
    public void AnUnreadLoadoutStillHearsAboutEmissions()
    {
        Assert.Contains(
            "Proto Heat Radiators",
            Said(new EmissionCallout(), Commander(), Arrival("Deciat", 5_000_000, "Independent", "Boom")),
            StringComparison.Ordinal);
    }

    /// <summary>A Multi Limpet Controller is a limpet controller.</summary>
    [Theory]
    [InlineData("int_multidronecontrol_universal_size7_class5")]
    [InlineData("int_multidronecontrol_mining_size3_class3")]
    [InlineData("int_multidronecontrol_miningv2_size5_class5")]
    [InlineData("int_multidronecontrol_operations_size3_class3")]
    public void AShipWithAMultiLimpetControllerHearsAboutEmissions(string module)
    {
        Assert.Contains(
            "Proto Heat Radiators",
            Said(
                new EmissionCallout(),
                Flying(module, limpets: 200),
                Arrival("Deciat", 5_000_000, "Independent", "Boom")),
            StringComparison.Ordinal);
    }

    /// <summary>An empty rack collects as much as no controller does, so it is the same silence.</summary>
    [Fact]
    public void AControllerWithNoLimpetsAboardHearsNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Flying(Collector, limpets: 0),
            Arrival("Deciat", 5_000_000, "Independent", "Boom"))));
    }

    /// <summary>
    /// And a Commander who can act on it hears the ordinary line, with nothing said about the hold
    /// either way.
    /// </summary>
    [Theory]
    [InlineData(200, "Ship")]
    [InlineData(null, "Ship")]
    [InlineData(0, "SRV")]
    public void SilenceTakesAHoldThatSaysTheLimpetsAreGone(int? limpets, string vessel)
    {
        var said = Said(
            new EmissionCallout(),
            Flying(Collector, limpets, vessel),
            Arrival("Deciat", 5_000_000, "Independent", "Boom"));

        Assert.Contains("Proto Heat Radiators", said, StringComparison.Ordinal);
        Assert.DoesNotContain("limpets", said, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ when it speaks

    /// <summary>Arriving twice in the same system is not news twice.</summary>
    [Fact]
    public void TheSameSystemIsSaidOnce()
    {
        var callout = new EmissionCallout();
        var state = Commander();
        var arrival = Arrival("Deciat", 5_000_000, "Independent", "Boom");

        Assert.Single(callout.Examine(Context(state, arrival)));
        Assert.Empty(callout.Examine(Context(state, arrival)));
    }

    /// <summary>
    /// Priming replays the session backlog, and the only jump in it that could still be acted on is the
    /// last.
    /// </summary>
    [Fact]
    public void PrimingSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: true,
            Commander(),
            GameStatus.Unknown,
            NavRoute.None,
            [Arrival("Deciat", 5_000_000, "Independent", "Boom")])));
    }

    // ------------------------------------------------------ the table it agrees with

    /// <summary>The rules are checked against the shipped table rather than trusted.</summary>
    [Fact]
    public void EveryRuleAgreesWithTheGeneratedMaterialsTable()
    {
        foreach (var group in EmissionRules.Groups)
        {
            foreach (var symbol in group.Materials)
            {
                var entry = MaterialCatalogue.Find(symbol);

                Assert.NotNull(entry);

                var origins = string.Join(" ", entry!.Origins);

                Assert.Contains("High grade emissions", origins, StringComparison.OrdinalIgnoreCase);

                // And the condition itself, in the words the generated table uses for it.
                var wanted = group switch
                {
                    { States.Count: 0, Allegiance: "Federation" } => "Federation systems",
                    { States.Count: 0 } => "Empire systems",
                    _ => group.States[0] switch
                    {
                        "CivilUnrest" => "Civil unrest",
                        "War" => "War/Civil war",
                        "Boom" => "Boom",
                        _ => "Outbreak",
                    },
                };

                Assert.Contains(wanted, origins, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>And nothing the table calls a high grade emission material is missing from the rules.</summary>
    [Fact]
    public void AndNoEmissionMaterialIsMissingFromTheRules()
    {
        var known = EmissionRules.Groups
            .SelectMany(group => group.Materials)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fromTable = MaterialCatalogue.All
            .Where(entry => entry.Origins.Any(origin =>
                origin.Contains("High grade emissions", StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.Symbol)
            .ToList();

        Assert.NotEmpty(fromTable);
        Assert.DoesNotContain(fromTable, symbol => !known.Contains(symbol));
    }

    /// <summary>A whole-inventory snapshot holding <paramref name="count"/> of each named material.</summary>
    private static JournalEvent Collected(int count, params string[] symbols)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-21T09:00:00Z",
            ["event"] = "Materials",
            ["Manufactured"] = symbols
                .Select(symbol => new Dictionary<string, object?> { ["Name"] = symbol, ["Count"] = count })
                .ToArray(),
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
