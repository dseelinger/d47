using System.Globalization;
using System.Text;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The arithmetic between what the Commander's plans need and what they are carrying (Phase 27,
/// "Gap analysis").
/// <para>
/// <b>Not a wishlist.</b> A wishlist is a list of things you want, which is what the ship builds
/// and the on-foot plans already are. This is the subtraction, read across all of them at once —
/// because a Commander gathering materials does not care which ship wanted them.
/// </para>
/// <para>
/// <b>Its own capability rather than a fourth tool on Ships</b>, because it is the one question
/// that spans both plan kinds and belongs to neither: a suit's graphene and a hull's zirconium are
/// the same shopping trip, and filing the answer under either would be filing it under half of
/// what it reads.
/// </para>
/// <para>
/// <b>Not <c>get_plan_shortfall</c>, which reads a different set.</b> That one nets what is on the
/// <em>checklist</em> — work the Commander has accepted. This one nets what is <em>planned</em>,
/// including builds that have never been promoted, which is most of them while a build is being
/// decided. Both are true at once and they answer different questions.
/// </para>
/// <para>
/// <b>Protected, so it is advertised to nothing and reachable by phrase and by press.</b> Phase 26
/// measured the advertised surface at 39,693 bytes against the 40,000 ceiling of the day, and
/// argued against raising it — which the Commander then did, to 50,000, on 2026-08-26. The
/// argument for advertising nothing here is untouched either way: nothing here needs a model to
/// read free English.
/// </para>
/// </summary>
public static class GapCapability
{
    public const string Id = "gap";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <param name="ships">The Commander's ship builds, or null where nothing composed them.</param>
    /// <param name="onFoot">Their suit and weapon plans, on the same terms.</param>
    public static CapabilityDescriptor Create(
        ShipPlanService? ships = null,
        OnFootPlanService? onFoot = null,
        Func<CommanderGameState?>? commander = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "The gap",
        Summary = "What everything you have planned needs that you are not carrying, ledger by ledger.",
        Examples =
        [
            "what do my plans still need",
            "what am I short of",
            "what do I still have to find",
        ],
        Display = new CapabilityDisplay { PanelTitle = "The gap", Order = 43 },

        // Phrases, never bare words.
        Keywords = ["what am I short of", "what do I still have to find"],

        Tools =
        [
            new ToolDefinition
            {
                Protected = true,
                Name = "get_build_gap",
                Description =
                    "What the Commander's ship builds and on-foot plans still need, netted against what "
                    + "they hold. Ledger by ledger and never totalled together, with what each shortfall "
                    + "is for. Not the checklist: this counts plans that have never been promoted.",
                Parameters =
                [
                    // <b>A parameter on the tool that already exists</b>, not a second tool
                    // (change-requests.md 37). "What is this shortfall for" is the same question
                    // narrowed to one material, and the narrowed answer is the one with room to
                    // name the blueprint - which the fleet-wide list deliberately does not.
                    new ToolParameter
                    {
                        Name = "material",
                        Type = ToolParameterType.String,
                        Description =
                            "One material, by name or symbol. Narrows the answer to what wants that "
                            + "one and names the blueprint each demand is for. Omit for everything.",
                    },
                    new ToolParameter
                    {
                        Name = "include_unowned",
                        Type = ToolParameterType.Boolean,
                        Description =
                            "Whether hulls and items they do not own yet are counted. True is the whole "
                            + "ambition; false is what can be finished now. Defaults to true.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("what am I short of", Nothing),
                    new ToolCommandPhrase("what do I still have to find", Nothing),
                ],
                Handler = (arguments, _) => Task.FromResult(
                    ToolResult.Ok(Report(ships, onFoot, commander, arguments))),
            },
        ],
    };

    /// <summary>
    /// The gap, said. <b>Ledger by ledger, and the one figure that spans everything counts units
    /// still to find</b> — a shopping list rather than a balance, because adding a tonne of gold to
    /// a unit of iron would be adding two different things.
    /// </summary>
    private static string Report(
        ShipPlanService? ships,
        OnFootPlanService? onFoot,
        Func<CommanderGameState?>? commander,
        ToolArguments arguments)
    {
        if (ships is null && onFoot is null)
        {
            return "I am not tracking any plans.";
        }

        var intended = !arguments.TryGetBoolean("include_unowned", out var include) || include;

        var report = PlanGap.Of(
            ships?.Store.Builds ?? [],
            onFoot?.Store.Builds ?? [],
            commander?.Invoke(),
            intended);

        // <b>One material asked about is a different answer, not a filtered one</b>
        // (change-requests.md 37). It has room to say which blueprint each demand is for, which
        // the fleet-wide list does not, and it is the question the Commander actually asked:
        // "what is a shortfall of Conductive Polymers for".
        if (arguments.TryGetString("material", out var asked) && !string.IsNullOrWhiteSpace(asked))
        {
            return About(report, asked);
        }

        if (report.Plans == 0)
        {
            return "Nothing is planned yet, so there is nothing to be short of.";
        }

        if (report.IsEmpty)
        {
            return "You are carrying everything your plans need. Nothing to go and find.";
        }

        var said = new StringBuilder();

        said.AppendLine(
            $"{report.UnitsToFind.ToString(CultureInfo.InvariantCulture)} units still to find, "
            + $"across {report.Plans.ToString(CultureInfo.InvariantCulture)} plan"
            + (report.Plans == 1 ? string.Empty : "s") + ".");

        if (!intended && report.Intended > 0)
        {
            said.AppendLine(
                $"Not counting {report.Intended.ToString(CultureInfo.InvariantCulture)} plan"
                + (report.Intended == 1 ? string.Empty : "s")
                + " for something you do not own yet.");
        }

        // Caps first. A certainty stated after a list of possibilities reads as one more
        // possibility, and it is the only line here that gathering cannot fix.
        foreach (var over in report.Ledgers.SelectMany(ledger => ledger.Lines).Where(line => line.ExceedsCapacity))
        {
            said.AppendLine(CultureInfo.InvariantCulture,
                $"{over.Material.Name}: your plans need {over.Needed} and you can only hold "
                + $"{over.Capacity}. That is at least two trips whatever happens.");
        }

        foreach (var gate in report.Gates)
        {
            said.AppendLine(gate);
        }

        foreach (var ledger in report.Ledgers)
        {
            said.AppendLine();
            said.AppendLine(CultureInfo.InvariantCulture, $"{ledger.Name} — {ledger.UnitsToFind} to find:");

            foreach (var line in ledger.Lines)
            {
                said.AppendLine(
                    $"  {line.Material.Name}: {line.Short.ToString(CultureInfo.InvariantCulture)} "
                    + $"short ({line.Held.ToString(CultureInfo.InvariantCulture)} of "
                    + $"{line.Needed.ToString(CultureInfo.InvariantCulture)})"
                    + $" — for {string.Join(", ", line.Wanted.Select(demand => demand.What))}."
                    + (line.Trade is { } trade ? $" {trade.Describe()}." : string.Empty));
            }
        }

        // Kept and marked, never refused: a plan line nothing can price is still a plan line.
        foreach (var unknown in report.Uncovered)
        {
            said.AppendLine(unknown);
        }

        return said.ToString().TrimEnd();
    }

    /// <summary>
    /// What one material is for (change-requests.md 37), asked for indirectly on 2026-08-20 when
    /// the Commander asked what a shortfall of Conductive Polymers was for and d47 said:
    /// <para>
    /// <i>"I can't tell you from here which single blueprint eats them — the shortfall is netted
    /// across every live plan at once, and there are a great many."</i>
    /// </para>
    /// <para>
    /// <b>That was honest about the tool and harder on itself than it needed to be.</b> The demands
    /// always knew the ship and the slot; what was missing was one field, and it is recorded at the
    /// fold now. This is where it gets said, because a question about one material has room for it
    /// and a fleet-wide list does not.
    /// </para>
    /// </summary>
    private static string About(GapReport report, string asked)
    {
        if (MaterialCatalogue.Find(asked) is not { } material)
        {
            return $"I do not know a material called \"{asked}\".";
        }

        var line = report.Ledgers
            .SelectMany(ledger => ledger.Lines)
            .FirstOrDefault(row => string.Equals(
                row.Material.Symbol, material.Symbol, StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return $"Nothing you have planned wants {material.Name}.";
        }

        var said = new StringBuilder();

        said.AppendLine(line.Short > 0
            ? $"{material.Name}: {line.Short.ToString(CultureInfo.InvariantCulture)} short "
              + $"({line.Held.ToString(CultureInfo.InvariantCulture)} of "
              + $"{line.Needed.ToString(CultureInfo.InvariantCulture)})."
            : $"{material.Name}: you have all "
              + $"{line.Needed.ToString(CultureInfo.InvariantCulture)} your plans want.");

        foreach (var demand in line.Wanted)
        {
            said.AppendLine(CultureInfo.InvariantCulture,
                $"  {demand.Fully()} — {demand.Units.ToString(CultureInfo.InvariantCulture)}");
        }

        if (line.Trade is { } trade)
        {
            said.AppendLine(trade.Describe() + ".");
        }

        return said.ToString().TrimEnd();
    }

    /// <summary>
    /// <em>"What is Conductive Polymers for"</em>, one phrase per material something actually
    /// wants (change-requests.md 37).
    /// <para>
    /// <b>Dynamic for the reason the carrier's course is</b>: the material is not knowable when the
    /// descriptor is registered, and a command is not part of a tool's schema, so a vocabulary that
    /// changes as plans change cannot move a byte of the cached prefix.
    /// </para>
    /// <para>
    /// <b>Only what is planned.</b> Generating a phrase for all 100-odd materials would put most of
    /// the catalogue in front of every utterance to answer "nothing wants that" — so the vocabulary
    /// is exactly the materials some plan asks for, which is also the only set the question means
    /// anything about.
    /// </para>
    /// </summary>
    public static IEnumerable<DynamicCommand> Phrases(
        ShipPlanService? ships,
        OnFootPlanService? onFoot,
        Func<CommanderGameState?>? commander)
    {
        if (ships is null && onFoot is null)
        {
            yield break;
        }

        var report = PlanGap.Of(
            ships?.Store.Builds ?? [],
            onFoot?.Store.Builds ?? [],
            commander?.Invoke(),
            includeIntended: true);

        foreach (var line in report.Ledgers.SelectMany(ledger => ledger.Lines))
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["material"] = line.Material.Symbol,
            };

            foreach (var shape in new[]
                     {
                         $"what is {line.Material.Name} for",
                         $"what do i need {line.Material.Name} for",
                         $"what wants {line.Material.Name}",
                     })
            {
                yield return new DynamicCommand(shape, Id, "get_build_gap", arguments);
            }
        }
    }
}
