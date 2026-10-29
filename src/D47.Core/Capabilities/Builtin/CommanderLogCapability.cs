using System.Globalization;
using System.Text;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Whether the Commander has met another Commander before, answered from the journals.</summary>
public static class CommanderLogCapability
{
    public const string Id = "commander-log";

    private const int Cap = 10;

    public static CapabilityDescriptor Create(CommanderLog? log, Func<string> frontierId) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Commander log",
        Display = new CapabilityDisplay { PanelTitle = "Commander log", Order = 46, ShowOnPanel = false },
        Summary = "Which other Commanders you have met, where and what happened, read from your journals.",
        Examples =
        [
            "have I met this commander",
            "have I met Arden Vale before",
            "which commanders have I met",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_commander_log",
                Exercise =
                    "Say \"have I met this commander\" with another Commander targeted, or ask about one by name. "
                    + "The reply lists the encounters with when, the system, their ship and what happened, and never "
                    + "includes message text.",
                Description =
                    "Encounters with other Commanders, read from the journals: when, the system, their ship "
                    + "where known, and what happened. With a name, that Commander's encounters, newest first. "
                    + "Without one, the Commander targeted most recently this session, or if none, the "
                    + "Commanders met most recently. Never includes message text.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "Who to look up. Leave out for the one just targeted.",
                    },
                ],
                Commands = [new ToolCommandPhrase("have i met this commander", new Dictionary<string, string>())],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Answer(log, frontierId(), arguments))),
            },
        ],
    };

    private static string Answer(CommanderLog? log, string fid, ToolArguments arguments)
    {
        if (log is null || fid.Length == 0)
        {
            return "No Commander is loaded, so there is no log of Commanders met.";
        }

        var partial = log.HistoryFolded
            ? string.Empty
            : "Older journals are still being read, so this is what is known so far. ";

        if (arguments.TryGetString("name", out var asked) && !string.IsNullOrWhiteSpace(asked))
        {
            return partial + About(log, fid, asked, "No encounters with " + asked.Trim() + " in the journals read.");
        }

        if (log.LastTargeted() is { } targeted)
        {
            return partial + About(log, fid, targeted.Name, "No encounters recorded.");
        }

        var met = log.Met(fid);

        if (met.Count == 0)
        {
            return partial + "No other Commanders met in the journals read.";
        }

        var text = new StringBuilder(partial);
        text.Append(CultureInfo.InvariantCulture, $"No Commander targeted this session. Most recently met, of {met.Count}:");

        foreach (var one in met.Take(Cap))
        {
            var last = log.Encounters(fid, one.Name)[0];
            text.Append(CultureInfo.InvariantCulture, $"\n{one.Name}, {one.Encounters} encounter{(one.Encounters == 1 ? string.Empty : "s")}, last: {Line(last)}");
        }

        return text.ToString();
    }

    private static string About(CommanderLog log, string fid, string name, string none)
    {
        var encounters = log.Encounters(fid, name);

        if (encounters.Count == 0)
        {
            return none;
        }

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{encounters[0].Name}: {encounters.Count} encounter{(encounters.Count == 1 ? string.Empty : "s")}");
        text.Append(encounters.Count > Cap ? $", newest {Cap}:" : ":");

        foreach (var encounter in encounters.Take(Cap))
        {
            text.Append('\n').Append(Line(encounter));
        }

        return text.ToString();
    }

    private static string Line(CommanderEncounter encounter)
    {
        var text = new StringBuilder(encounter.Last.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC");

        if (encounter.System.Length > 0)
        {
            text.Append(", ").Append(encounter.System);
        }

        if (!string.IsNullOrEmpty(encounter.Ship))
        {
            text.Append(", ").Append(encounter.Ship);
        }

        return text.Append(": ").Append(Kinds(encounter.Kinds)).ToString();
    }

    private static string Kinds(EncounterKind kinds)
    {
        var words = Phrases.Where(pair => (kinds & pair.Kind) != 0).Select(pair => pair.Phrase).ToList();
        return words.Count == 0 ? "met" : string.Join(", ", words);
    }

    private static readonly (EncounterKind Kind, string Phrase)[] Phrases =
    [
        (EncounterKind.Targeted, "targeted"),
        (EncounterKind.InterdictedBy, "interdicted you"),
        (EncounterKind.EscapedInterdictionBy, "you escaped their interdiction"),
        (EncounterKind.Interdicted, "you interdicted them"),
        (EncounterKind.Crime, "crime against you"),
        (EncounterKind.KilledBy, "killed you"),
        (EncounterKind.Killed, "you killed them"),
        (EncounterKind.Winged, "wing"),
        (EncounterKind.Messaged, "messages"),
    ];
}
