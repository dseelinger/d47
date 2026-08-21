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
    /// One arrival. <paramref name="allegiance"/> is the <b>system's</b> — the journal's
    /// <c>SystemAllegiance</c>, which is the controlling faction's — and each faction carries its
    /// own allegiance too, because a real journal does and because the defect reported on
    /// 2026-08-21 was reading the wrong one of the two.
    /// </summary>
    private static JournalEvent Arrival(
        string system,
        long population,
        string allegiance,
        params (string Allegiance, string State)[] factions)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-21T09:00:00Z",
            ["event"] = "FSDJump",
            ["StarSystem"] = system,
            ["Population"] = population,
            ["SystemAllegiance"] = allegiance,
            ["Factions"] = factions
                .Select((faction, at) => new Dictionary<string, object?>
                {
                    ["Name"] = $"{faction.Allegiance} party {at + 1} of {system}",
                    ["Allegiance"] = faction.Allegiance,
                    ["FactionState"] = faction.State,
                })
                .ToArray(),
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

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
            Arrival("Deciat", 5_000_000, "Independent", ("Independent", "Boom")));

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
            Arrival("Cubeo", 20_000_000, "Empire", ("Empire", "Outbreak")));

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
            Arrival("Sol", 22_000_000, "Federation", ("Federation", "None")));

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
    public void AMinorityFederalFactionDoesNotMakeAnIndependentSystemFederal()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival(
                "Oppi",
                4_626_551,
                "Independent",
                ("Federation", "None"),
                ("Independent", "None"),
                ("Independent", "None"),
                ("Independent", "None"),
                ("Independent", "None"),
                ("Independent", "None"),
                ("Independent", "None")))));
    }

    /// <summary>
    /// <b>The case the Commander asked for by name</b> — *"not just Core Dynamics Composites plus
    /// the related one … but when completely different ones are there"*.
    /// <para>
    /// <b>It comes from two factions in different states</b>, and never depended on reading
    /// allegiance per faction. The first version of this test mixed a Federal faction with an
    /// Independent one in Outbreak and passed for the wrong reason — it was asserting the defect
    /// reported on 2026-08-21.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoFactionsInDifferentStatesOfferTwoUnrelatedGroups()
    {
        var said = Said(
            new EmissionCallout(),
            Commander(),
            Arrival(
                "Shinrarta Dezhra",
                85_000_000,
                "Independent",
                ("Independent", "Boom"),
                ("Independent", "Outbreak")));

        Assert.Contains("Proto Radiolic Alloys", said, StringComparison.Ordinal);
        Assert.Contains("Pharmaceutical Isolators", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Boom only. Expansion is the second commonest state in the corpus, so reading the wiki's
    /// "Boom or Expansion" the other way would have made this the chattiest callout d47 has.
    /// </summary>
    [Fact]
    public void ExpansionIsNotBoom()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Alioth", 8_000_000, "Independent", ("Independent", "Expansion")))));
    }

    /// <summary>The population floor, applied to every group rather than to Outbreak alone.</summary>
    [Fact]
    public void ASparselyPopulatedSystemSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Hyades Sector EG-X c1-8", 227_781, "Independent", ("Independent", "Boom")))));
    }

    [Fact]
    public void AFactionInNoUsefulStateSaysNothing()
    {
        Assert.Empty(new EmissionCallout().Examine(Context(
            Commander(),
            Arrival("Deciat", 5_000_000, "Independent", ("Independent", "None")))));
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
            Arrival("Deciat", 5_000_000, "Independent", ("Independent", "Boom")));

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
            Arrival("Deciat", 5_000_000, "Independent", ("Independent", "Boom")))));
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
            Said(new EmissionCallout(), state, Arrival("Deciat", 5_000_000, "Independent", ("Independent", "Boom"))),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ when it speaks

    /// <summary>Arriving twice in the same system is not news twice.</summary>
    [Fact]
    public void TheSameSystemIsSaidOnce()
    {
        var callout = new EmissionCallout();
        var state = Commander();
        var arrival = Arrival("Deciat", 5_000_000, "Independent", ("Independent", "Boom"));

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
            [Arrival("Deciat", 5_000_000, "Independent", ("Independent", "Boom"))])));
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
                var wanted = group.Allegiance switch
                {
                    "Federation" => "Federation systems",
                    "Empire" => "Empire systems",
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
