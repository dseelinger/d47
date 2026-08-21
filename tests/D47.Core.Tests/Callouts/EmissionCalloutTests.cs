using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Systems that might be running High Grade Emissions (list.md Phase 40, asked for 2026-08-21).
/// <para>
/// The conditions are the Elite Dangerous Wiki's, corroborated by the 2017 Frontier Forums USS
/// guide and by what <c>edgalaxy.net/hge</c> sorts live detections into — and settled where those
/// disagree by the Commander's own four rulings. <c>docs/plans/change-requests.md</c> item 22 has
/// all of it.
/// </para>
/// </summary>
public class EmissionCalloutTests
{
    /// <summary>
    /// One arrival. <paramref name="allegiance"/> is the <b>system's</b> and
    /// <paramref name="states"/> are the <b>controlling faction's</b> — the two things the
    /// Commander's table is keyed on, and the two that earlier readings each took from the wrong
    /// place.
    /// <para>
    /// A second, non-controlling Federal faction in Boom is always present, so every test here is
    /// also a test that a minority superpower faction changes nothing.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <b>Superpower beats state, and never the other way.</b> The one ruling the two sources
    /// contradict each other on: the 2017 forums guide says an Imperial system in Outbreak gives
    /// shielding *and* isolators, and the wiki says a superpower faction never yields anything but
    /// its own. Settled the wiki's way on the Commander's instruction, so this is the assertion
    /// that pins the choice rather than merely the behaviour.
    /// </summary>
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
    /// Proprietary Composites rides with Core Dynamics Composites — the wiki lists both for
    /// Federal space and the 2017 guide lists only the one. Settled the wiki's way.
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
    /// <b>The reported defect, in the system it was reported in</b> (2026-08-21):
    /// <i>"Oppi could be running high grade emissions for Core Dynamics Composites. No, it
    /// couldn't. Oppi is an Independent system."</i>
    /// <para>
    /// Oppi's real shape, off the journal: Independent, 4,626,551 people, seven factions of which
    /// exactly one is Federal and none of them is in any state. The controlling faction — United
    /// Fintamkina Left Party — is Independent. Reading allegiance per faction found the Federal
    /// minority and announced its composites.
    /// </para>
    /// <para>
    /// <b>A minority superpower faction is not rare.</b> 84 of 400 recent corpus jumps are into a
    /// system mixing a Federal faction with an Independent or Alliance one, so the old reading was
    /// wrong in roughly a fifth of populated systems.
    /// </para>
    /// </summary>
    [Fact]
    public void OppiSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Oppi", 4_626_551, "Independent"))));
    }

    /// <summary>
    /// <b>The case the Commander asked for by name</b> — *"not just Core Dynamics Composites plus
    /// the related one … but when completely different ones are there"*.
    /// <para>
    /// <b>It comes from the controlling faction wearing two states</b>, not from two factions.
    /// This test has now been wrong twice: first mixing a Federal faction with an Independent one,
    /// which asserted the Oppi defect; then two factions in different states, which the Commander's
    /// corrected table retired. The corpus has <c>CivilUnrest + Expansion</c>,
    /// <c>Expansion + War</c> and <c>Boom + Expansion</c> among controlling factions, so this shape
    /// is ordinary rather than contrived.
    /// </para>
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

    /// <summary>
    /// <b>Expansion counts, beside Boom.</b> Ruled Boom-only that morning and corrected the same
    /// day — and it is not a small widening, because Expansion is the second commonest faction
    /// state in the corpus after None.
    /// </summary>
    [Fact]
    public void ExpansionOffersTheProtoMaterialsToo()
    {
        var said = Said(new EmissionCallout(), Commander(), Arrival("Alioth", 8_000_000, "Independent", "Expansion"));

        Assert.Contains("Proto Heat Radiators", said, StringComparison.Ordinal);
        Assert.Contains("Proto Light Alloys", said, StringComparison.Ordinal);
        Assert.Contains("Proto Radiolic Alloys", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Alliance yields nothing at all.</b> A real exclusion in the Commander's table, and what a
    /// previous reading got wrong by treating the state groups as "anything not Federal or
    /// Imperial".
    /// </summary>
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

    /// <summary>
    /// <b>The state comes from the <c>Factions</c> array, not from <c>SystemFaction</c>.</b>
    /// Measured: <c>SystemFaction</c> carries <c>FactionState</c> on only 118 of 205 recent jumps,
    /// so reading it there loses two systems in five.
    /// </summary>
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

    /// <summary>
    /// <b>What there is no room for is not mentioned.</b> Being sent to collect a material that
    /// cannot be carried is a callout costing attention and offering nothing.
    /// </summary>
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

        // One event, not three: `Materials` is a whole-inventory snapshot, so three of them in a
        // row leaves only the last one's holding standing.
        state.Apply(Collected(100, "protoheatradiators", "protolightalloys", "protoradiolicalloys"));

        Assert.Empty(new EmissionCallout { Capacity = _ => 100 }.Examine(Context(
            state,
            Arrival("Deciat", 5_000_000, "Independent", "Boom"))));
    }

    /// <summary>
    /// <b>An unknown capacity means say it.</b> The opposite of the milestone callout's choice and
    /// deliberately: there an unknown means a percentage that would have to be invented, here it
    /// means only that d47 cannot prove the Commander is full.
    /// </summary>
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
    /// Priming replays the session backlog, and the only jump in it that could still be acted on
    /// is the last. Without this, starting d47 after Elite announces every system of the session
    /// at once.
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

    /// <summary>
    /// <b>The rules are checked against the shipped table rather than trusted.</b>
    /// <c>Materials.tsv</c> is generated by <c>tools/gen-materials.py</c> and carries these
    /// conditions in its own origins column, so this is a second source that ships in the repo —
    /// and a regenerated table that disagrees fails here instead of drifting quietly away from a
    /// callout nobody would think to re-read.
    /// </summary>
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

    /// <summary>
    /// And nothing the table calls a high grade emission material is missing from the rules. The
    /// assertion above would pass a table with nine of the ten in it.
    /// </summary>
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

    /// <summary>
    /// A whole-inventory snapshot holding <paramref name="count"/> of each named material.
    /// <b>One event covers every material</b>, because that is what `Materials` is — a second one
    /// replaces the first rather than adding to it.
    /// </summary>
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
