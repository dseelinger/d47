using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What a hull and a module can do (list.md Phase 14, "Elite Dangerous Ships").
/// <para>
/// The counterpart to <see cref="JournalCapability"/>: that one reports what the Commander is
/// flying, and this one reports what a ship is capable of before anybody buys it. Both are
/// answered on this machine with no network, which is what separates them from the galaxy
/// search — a Type-9's landing pad requirement is a fact about the game, not about the galaxy,
/// and asking a third party for it would be absurd.
/// </para>
/// <para>
/// No setting. There is nothing to turn off: the table ships in the binary, nothing leaves the
/// machine, and a capability with an off switch that protects nothing is a row a Commander has to
/// read and decide about for no reason.
/// </para>
/// </summary>
public static class SpecificationCapability
{
    public const string Id = "specifications";

    public static CapabilityDescriptor Create(Func<CommanderGameState?> commander) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Ship and module specifications",
        Summary = "What a hull or a module can do, from a table built out of the community's own data.",
        Examples =
        [
            "how fast is a Python",
            "does a Type-9 need a large pad",
            "what does a 5A frame shift drive weigh",
        ],
        Keywords =
        [
            // Phrases only, and every one of them about a ship as a class of thing rather than
            // about this Commander's — "my ship" belongs to the journal capability and routing it
            // here would answer a question about a hull when one was asked about a hold.
            "what pad does",
            "how fast is a",
            "top speed of",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_ship_specification",
                Description =
                    "What a ship can do before it is outfitted: landing pad size, speed and boost, base "
                    + "hull and shield strength, hardpoint and internal compartment sizes, crew seats and "
                    + "hull cost. Ask about any ship by name, or leave it out for the one the Commander "
                    + "is flying.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "ship",
                        Type = ToolParameterType.String,
                        Description =
                            "The ship, by name — for example \"Python\" or \"Type-9 Heavy\". Defaults to "
                            + "the one the Commander is flying.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(DescribeShip(commander, arguments))),
            },
            new ToolDefinition
            {
                Name = "get_module_specification",
                Description =
                    "What a module weighs, draws and costs, for a frame shift drive the numbers a jump "
                    + "range is computed from, and for a bulkhead what it adds to the hull and how it "
                    + "resists each damage type. A module name without a size returns the sizes that "
                    + "exist, because an 8A drive and a 2E drive are not the same answer. Armour is "
                    + "per-hull, so name it with its ship: \"Python Reactive Surface Composite\".",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "module",
                        Type = ToolParameterType.String,
                        Description =
                            "The module, by name — for example \"Frame Shift Drive\" or \"Power Plant\".",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "size",
                        Type = ToolParameterType.Integer,
                        Description = "Module size, 1 to 8.",
                    },
                    new ToolParameter
                    {
                        Name = "rating",
                        Type = ToolParameterType.String,
                        Description = "Module rating, A to E.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(DescribeModule(arguments))),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Specifications", Order = 49 },
    };

    private static string DescribeShip(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        var asked = arguments.TryGetString("ship", out var spoken) && !string.IsNullOrWhiteSpace(spoken)
            ? spoken.Trim()
            : null;

        // The Loadout's symbol, not its display name: the symbol is what the table is keyed by,
        // and going through the display name would put a round trip through the matcher for a
        // lookup that is already exact.
        var wanted = asked ?? commander()?.Ship.Type;

        if (string.IsNullOrWhiteSpace(wanted))
        {
            return "No ship was named, and I don't know what the Commander is flying yet — that comes from "
                   + "the Loadout event, written when you enter the game or change your outfitting.";
        }

        if (EliteSpecifications.Ship(wanted) is not { } ship)
        {
            return Unknown(wanted, asked);
        }

        var report = new StringBuilder();

        report.Append(ship.Name);

        if (ship.Manufacturer is not null)
        {
            report.Append($", built by {ship.Manufacturer}");
        }

        if (ship.Pad is not null)
        {
            // First, because it is the fact that decides whether a station is even an option.
            report.Append($". Needs a {ship.Pad} pad");
        }

        report.AppendLine(".");

        if (ship.Speed is not null || ship.Boost is not null)
        {
            report.AppendLine($"Speed {Figure(ship.Speed)} m/s, boosting to {Figure(ship.Boost)}.");
        }

        var strength = new List<string>();

        if (ship.Armour is not null)
        {
            strength.Add($"{ship.Armour} armour");
        }

        if (ship.Shields is not null)
        {
            strength.Add($"{ship.Shields} base shields");
        }

        if (ship.Hardness is not null)
        {
            strength.Add($"hardness {ship.Hardness}");
        }

        if (ship.HullMass is not null)
        {
            strength.Add($"{ship.HullMass} t hull");
        }

        if (strength.Count > 0)
        {
            report.AppendLine(string.Join(", ", strength) + ".");
        }

        if (ship.Hardpoints.Count > 0)
        {
            report.AppendLine($"Hardpoints: {Slots(ship.Hardpoints)}.");
        }

        if (ship.Internals.Count > 0)
        {
            report.AppendLine($"Optional internals: {Slots(ship.Internals)}.");
        }

        if (ship.Crew is > 0)
        {
            report.AppendLine($"{ship.Crew} crew seat{(ship.Crew == 1 ? "" : "s")}"
                              + (ship.MassLock is not null ? $", mass lock {ship.MassLock}." : "."));
        }

        if (ship.Cost is not null)
        {
            // The hull alone. Saying "costs 142 million" about an Anaconda that is nearer half a
            // billion outfitted would be a true number answering a question nobody asked.
            report.AppendLine($"Hull {ship.Cost.Value.ToString("N0", CultureInfo.InvariantCulture)} cr, "
                              + "before any modules.");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// What to say about a hull with no figures, which is three different sentences.
    /// <para>
    /// A ship the table knows exists and has no numbers for is not the same as one nobody has
    /// heard of, and neither is the same as a name that was nearly something. Collapsing them
    /// into "I don't know that ship" would tell a Commander flying a brand new hull that d47 is
    /// broken, when the truth is that the table predates their ship.
    /// </para>
    /// </summary>
    private static string Unknown(string wanted, string? asked)
    {
        if (EliteSpecifications.KnownButUnmeasured
            .FirstOrDefault(name => Catalogue.Match([name], wanted) is not null) is { } known)
        {
            return $"{known} is a ship I know of and have no figures for — it is newer than the "
                   + "specification table d47 ships. I would rather say that than guess at its numbers.";
        }

        var near = EliteSpecifications.NearShips(wanted);

        if (near.Count > 0)
        {
            return Catalogue.Unknown("ship", wanted, near);
        }

        return asked is null
            ? $"The Commander is flying a '{wanted}', and I have no figures for it. It is either newer "
              + "than my specification table or not a ship I know."
            : $"I don't know a ship called '{wanted}'.";
    }

    private static string DescribeModule(ToolArguments arguments)
    {
        if (!arguments.TryGetString("module", out var spoken) || string.IsNullOrWhiteSpace(spoken))
        {
            return "No module was named.";
        }

        var size = arguments.TryGetInt32("size", out var value) ? value : (int?)null;
        var rating = arguments.TryGetString("rating", out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim().ToUpperInvariant()
            : null;

        var matches = EliteSpecifications.ModulesNamed(spoken, size, rating);

        if (matches.Count == 0)
        {
            // Two different failures. A name nothing matches gets suggestions; a real name with a
            // size that does not exist gets the sizes that do, which is the more useful answer and
            // the one a Commander asking for a 9A drive needs.
            var everything = EliteSpecifications.ModulesNamed(spoken, null, null);

            if (everything.Count == 0)
            {
                return Catalogue.Unknown("module", spoken.Trim(), EliteSpecifications.NearModules(spoken));
            }

            // Armour is the third failure. It has no size and no rating at all, so offering the
            // sizes it comes in would answer a question about a drive when one was asked about a
            // bulkhead.
            if (everything.All(module => module.IsBulkhead))
            {
                return $"{everything[0].Name} is armour. It has no size or rating — ask for it by name.";
            }

            return $"There is no {size}{rating} {everything[0].Name}. It comes in "
                   + $"{string.Join(", ", everything.Select(module => module.Size).Distinct(StringComparer.Ordinal))}.";
        }

        var report = new StringBuilder();

        if (matches.Count > Listed)
        {
            // Eleven drives at a dozen figures each is a table, not something anybody can hear.
            report.AppendLine(
                $"{matches[0].Name} comes in {matches.Count} variants: "
                + $"{string.Join(", ", matches.Select(module => module.Size).Distinct(StringComparer.Ordinal))}. "
                + "Ask for a size and rating for the figures.");

            return report.ToString().TrimEnd();
        }

        foreach (var module in matches)
        {
            report.AppendLine(Describe(module));
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// How many variants are worth reading out in full before the answer becomes a table. Four
    /// covers "a 5A drive" and every mount of one weapon size, and stops short of every drive.
    /// </summary>
    private const int Listed = 4;

    private static string Describe(ModuleSpecification module)
    {
        // A bulkhead's Size falls back to its name, so leading with both would say it twice.
        var report = new StringBuilder(module.IsBulkhead ? module.Name : $"{module.Size} {module.Name}");

        if (module.Mount is not null)
        {
            report.Append($", {module.Mount}");
        }

        var facts = new List<string>();

        if (module.Mass is not null)
        {
            facts.Add($"{module.Mass} t");
        }

        if (module.Power is not null)
        {
            facts.Add($"{module.Power} MW");
        }

        if (module.Integrity is not null)
        {
            facts.Add($"integrity {module.Integrity}");
        }

        if (module.Cost is not null)
        {
            facts.Add($"{module.Cost.Value.ToString("N0", CultureInfo.InvariantCulture)} cr");
        }

        if (facts.Count > 0)
        {
            report.Append($" — {string.Join(", ", facts)}");
        }

        if (module.IsDrive)
        {
            // The figures a jump range is actually computed from, and the reason the table carries
            // them at all: they are the ones nobody can eyeball and every jump calculator needs.
            report.Append($". Optimal mass {module.OptimalMass} t, max fuel per jump {module.MaxFuelPerJump} t");
        }

        if (module.HullBoost is not null)
        {
            // All four resistances, including the zeroes. Mirrored and Reactive are the same mass
            // and the same hull boost at a different price, so the resistances are the entire
            // reason to choose between them — and a resistance left unsaid reads as unknown when
            // "no effect" is the answer.
            report.Append($". Hull {Percentage(module.HullBoost)}, ")
                .Append($"kinetic {Percentage(module.KineticResistance)}, ")
                .Append($"thermal {Percentage(module.ThermalResistance)}, ")
                .Append($"explosive {Percentage(module.ExplosiveResistance)}, ")
                .Append($"caustic {Percentage(module.CausticResistance)}");
        }

        return report.Append('.').ToString();
    }

    /// <summary>
    /// A signed fraction as a percentage. Signed on purpose: a bulkhead's -20% kinetic is a hole
    /// and its +25% is a saving, and dropping the sign turns one into the other.
    /// <para>
    /// Rounded rather than exact because the source stores fractions — 0.8 × 100 is 80.000000000001
    /// in binary floating point, and nobody wants to hear that read out.
    /// </para>
    /// </summary>
    private static string Percentage(double? fraction) =>
        fraction is { } value
            ? $"{(value * 100).ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture)}%"
            : "unrecorded";

    private static string Figure(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "an unrecorded number of";

    /// <summary>
    /// Slot sizes as counts by size — "2 × 4, 3 × 3" — rather than a list. It is shorter to hear
    /// and it is how an outfitting screen is actually read.
    /// </summary>
    private static string Slots(IReadOnlyList<int> sizes) =>
        string.Join(
            ", ",
            sizes.GroupBy(size => size)
                .OrderByDescending(group => group.Key)
                .Select(group => group.Count() == 1 ? $"1 × size {group.Key}" : $"{group.Count()} × size {group.Key}"));
}
