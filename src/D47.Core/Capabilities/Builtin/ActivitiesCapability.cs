using System.Globalization;
using D47.Core.Activities;
using D47.Core.Audio;
using D47.Core.Utilities;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Voice answers over the activity ledger: when each activity was last done (#586).</summary>
public static class ActivitiesCapability
{
    public const string Id = "activities";

    private const string LastDoneTool = "get_activity_last_done";

    private const string Folding = "I'm still reading your journals. Ask again in a minute.";

    private const string NoLedger = "I am not keeping activity dates in this configuration.";

    public static CapabilityDescriptor Create(
        ActivityLedger? ledger,
        Func<string?> commander,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Conversation",
        Name = "Activities",
        Summary = "Say when you last did an activity.",
        Examples =
        [
            "when did I last go mining",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Activities", Order = 17, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = LastDoneTool,
                Exercise =
                    "Say \"when did I last do mining\". The reply gives the date and how long ago.",
                Description = "Say when the Commander last did one activity, with the date and how long ago.",
                Parameters = [ActivityParameter()],
                Commands = [.. LastDonePhrases()],
                Handler = (arguments, _) => Task.FromResult(LastDone(ledger, commander(), now(), arguments)),
            },
        ],
    };

    private static ToolParameter ActivityParameter() =>
        ToolParameter.Text("activity", "Which activity, by name or by what the Commander calls it.", required: true);

    private static IEnumerable<ToolCommandPhrase> LastDonePhrases() =>
        from entry in ActivityCatalogue.All
        from term in entry.Phrases.Distinct(StringComparer.OrdinalIgnoreCase)
        from lead in new[] { "when did i last do", "when did i last go" }
        select new ToolCommandPhrase($"{lead} {term}", Arguments(entry.Key));

    private static Dictionary<string, string> Arguments(string key)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["activity"] = key };

        return arguments;
    }

    private static ToolResult LastDone(
        ActivityLedger? ledger,
        string? commander,
        DateTimeOffset now,
        ToolArguments arguments)
    {
        if (ledger is null)
        {
            return ToolResult.Error(NoLedger);
        }

        arguments.TryGetString("activity", out var said);

        if (string.IsNullOrWhiteSpace(said))
        {
            return ToolResult.Error("Which activity?");
        }

        if (ActivityCatalogue.Find(said) is not { } entry)
        {
            return ToolResult.Ok(NotKept(said));
        }

        if (!ledger.HistoryFolded)
        {
            return ToolResult.Ok(Folding);
        }

        if (ledger.LastDone(entry.Key, commander) is { } at)
        {
            var age = ActivityAge.SayInWords(at, now);

            return ToolResult.Ok($"{entry.Name}, last on {Date(at)}. {char.ToUpperInvariant(age[0])}{age[1..]}.");
        }

        return ToolResult.Ok(
            ledger.CorpusFrom(commander) is { } from
                ? $"{entry.Name}: not in your journals since {Date(from)}."
                : $"{entry.Name}: not in your journals yet.");
    }

    private static string NotKept(string said) =>
        $"I don't keep a date for {said.Trim()}. The Activities page lists the {Words(ActivityCatalogue.All.Count)} I do.";

    private static string Words(int number) => SpokenNumbers.Expand(number.ToString(CultureInfo.InvariantCulture));

    private static string Date(DateTimeOffset at) =>
        GalacticTime.Galactic(at.ToUniversalTime()).ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
}
