using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Reading a system's name back to the Commander (Phase 18, "Read a system name").</summary>
public static class SystemNameCapability
{
    public const string Id = "system-names";

    /// <summary><param name="commander"> The active Commander, for the "where I am standing" case.</summary>
    /// <param name="commander">The active Commander, for the "where I am standing" case.</param>
    public static CapabilityDescriptor Create(Func<CommanderGameState?> commander) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "System names",
        Summary =
            "What a system's own name says about it — its sector, its boxel, and the mass code that "
            + "sizes it. Computed from the string, with no network.",
        Examples =
        [
            "read this system name",
            "what does this system name mean",
            "what is the mass code here",
            "what does Dryafea PO-X d2-0 mean",
        ],
        Keywords =
        [
            "system name",
            "mass code",
            "read this system",
            "what does this name mean",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "read_system_name",
                Description =
                    "Decode what a procedurally-generated system name says about the system: its "
                    + "sector, its boxel, the mass code and the size of the cube that code implies. "
                    + "Works offline on any name, including one read off the Galaxy Map and never "
                    + "visited. Says nothing about what a system is worth: the mass code orders by "
                    + "mass and does not predict payout.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description =
                            "The system name to read. Leave out for the system the Commander is in.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Read(commander(), arguments))),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "System names", Order = 55 },
    };

    private static string Read(CommanderGameState? state, ToolArguments arguments)
    {
        var spoken = arguments.TryGetString("name", out var given) && !string.IsNullOrWhiteSpace(given)
            ? given.Trim()
            : null;

        // Only the Commander's own system carries a star class, because that is the one d47 watched them
        // arrive at.
        var here = spoken is null || string.Equals(spoken, state?.Location.StarSystem, StringComparison.OrdinalIgnoreCase);

        var value = spoken ?? state?.Location.StarSystem;

        if (string.IsNullOrWhiteSpace(value))
        {
            return
                "I do not know what system you are in yet, and you have not named one. Say a name and "
                + "I will read it — I do not need to have been there.";
        }

        var name = SystemName.Read(value);
        var report = new StringBuilder();

        if (!name.IsProcedural)
        {
            // A real answer rather than a failure.
            report.AppendLine(
                $"{name.Value} is a hand-named system, so there is no mass code in it to read. Only "
                + "procedurally-generated names carry one.");

            AppendStar(report, state, here);

            return report.ToString().TrimEnd();
        }

        report.AppendLine($"{name.Value}");
        report.AppendLine($"  Sector: {name.Sector} — a {Number(SystemName.SectorLightYears)} light year cube.");
        report.AppendLine(
            $"  Boxel: {name.Boxel}, system {Number(name.SystemNumber!.Value)} within it.");

        var measured = name.BoxSizeMeasured
            ? "measured against real coordinates"
            : "from the doubling, not measured here";

        report.AppendLine(
            $"  Mass code {name.MassCode}: {Ordinal(name.MassRank!.Value)} of eight, a to h, least to "
            + $"most massive. Its boxel is {Number(name.BoxLightYears!.Value)} light years across "
            + $"({measured}).");

        AppendStar(report, state, here);

        report.AppendLine();
        report.AppendLine(
            "The letter orders systems by mass and sizes the box. It is not established to predict "
            + "what a system pays — I measured that against real scan history and the sample could "
            + "not settle it, so I will not pretend otherwise.");

        return report.ToString().TrimEnd();
    }

    /// <summary>The star, where d47 saw the Commander arrive at it.</summary>
    private static void AppendStar(StringBuilder report, CommanderGameState? state, bool here)
    {
        if (!here || state?.Location.StarClass is not { } starClass)
        {
            return;
        }

        report.AppendLine($"  Main star: {StarClasses.Speak(starClass)}.");
    }

    private static string Ordinal(int rank) => rank switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "eighth",
        _ => rank.ToString(CultureInfo.InvariantCulture),
    };

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
