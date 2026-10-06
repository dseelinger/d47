using System.Globalization;
using D47.Core.Activities;
using D47.Core.Audio;
using D47.Core.Utilities;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Voice answers over the activity ledger: what has not been done lately, and when each was last done (#586).</summary>
public static class ActivitiesCapability
{
    public const string Id = "activities";

    private const string StaleTool = "get_stale_activities";

    private const string LastDoneTool = "get_activity_last_done";

    private const string SuggestTool = "set_activity_suggested";

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
        Summary = "Say which activities you have not done in a while, when you last did one, and stop suggesting one.",
        Examples =
        [
            "what's something I haven't done in a while",
            "when did I last go mining",
            "don't suggest bounty hunting anymore",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Activities", Order = 17, ShowOnPanel = false },
        Keywords =
        [
            "what haven't i done in a while",
            "what's something i haven't done",
            "something i haven't done in a while",
            "something i haven't done",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = StaleTool,
                AlwaysLoaded = true,
                Description =
                    "Name the three activities the Commander has gone longest without doing, oldest first, "
                    + "each with how long ago. Activities the Commander switched off are left out.",
                Handler = (_, _) => Task.FromResult(Stale(ledger, commander(), now())),
            },
            new ToolDefinition
            {
                Name = LastDoneTool,
                Description = "Say when the Commander last did one activity, with the date and how long ago.",
                Parameters = [ActivityParameter()],
                Commands = [.. LastDonePhrases()],
                Handler = (arguments, _) => Task.FromResult(LastDone(ledger, commander(), now(), arguments)),
            },
            new ToolDefinition
            {
                Name = SuggestTool,
                Description =
                    "Stop suggesting one activity, or suggest it again. Its date stays on the Activities page. "
                    + "The Commander's own decision.",
                Parameters =
                [
                    ActivityParameter(),
                    new ToolParameter
                    {
                        Name = "suggested",
                        Type = ToolParameterType.Boolean,
                        Description = "False to stop suggesting it, true to suggest it again.",
                        Required = true,
                    },
                ],
                Commands = [.. SuggestPhrases()],
                Protected = true,
                RefusalExample = "don't suggest combat zones",
                Handler = (arguments, _) => Task.FromResult(Suggest(ledger, commander(), arguments)),
            },
        ],
    };

    private static ToolParameter ActivityParameter() => new()
    {
        Name = "activity",
        Type = ToolParameterType.String,
        Description = "Which activity, by name or by what the Commander calls it.",
        Required = true,
    };

    private static IEnumerable<ToolCommandPhrase> LastDonePhrases() =>
        from entry in ActivityCatalogue.All
        from term in entry.Phrases.Distinct(StringComparer.OrdinalIgnoreCase)
        from lead in new[] { "when did i last do", "when did i last go" }
        select new ToolCommandPhrase($"{lead} {term}", Arguments(entry.Key));

    private static IEnumerable<ToolCommandPhrase> SuggestPhrases() =>
        from entry in ActivityCatalogue.All
        from term in entry.Phrases.Distinct(StringComparer.OrdinalIgnoreCase)
        from phrase in new[]
        {
            ($"don't suggest {term}", false),
            ($"don't suggest {term} anymore", false),
            ($"suggest {term} again", true),
        }
        select new ToolCommandPhrase(phrase.Item1, Arguments(entry.Key, phrase.Item2));

    private static Dictionary<string, string> Arguments(string key, bool? suggested = null)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["activity"] = key };

        if (suggested is { } value)
        {
            arguments["suggested"] = value ? "true" : "false";
        }

        return arguments;
    }

    private static ToolResult Stale(ActivityLedger? ledger, string? commander, DateTimeOffset now)
    {
        if (ledger is null)
        {
            return ToolResult.Error(NoLedger);
        }

        if (!ledger.HistoryFolded)
        {
            return ToolResult.Ok(Folding);
        }

        var stalest = ledger.Stalest(3, commander);

        if (stalest.Count == 0)
        {
            var dated = ActivityCatalogue.All.Any(entry => ledger.LastDone(entry.Key, commander) is not null);

            return ToolResult.Ok(
                dated
                    ? "Nothing to suggest. Every activity I can date is switched off. The Activities page has them all."
                    : "Nothing to suggest. None of your activities has a date in your journals yet. "
                    + "The Activities page has them all.");
        }

        var lines = string.Join(
            ' ',
            stalest.Select(item => $"{ActivityCatalogue.ByKey(item.Key)!.Name}, {ActivityAge.SayInWords(item.At, now)}."));

        return ToolResult.Ok(
            stalest.Count < 3
                ? $"Only {Words(stalest.Count)} I can suggest. {lines}"
                : $"{lines} The rest are on the Activities page.");
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

    private static ToolResult Suggest(ActivityLedger? ledger, string? commander, ToolArguments arguments)
    {
        if (ledger is null)
        {
            return ToolResult.Error(NoLedger);
        }

        arguments.TryGetString("activity", out var said);

        if (string.IsNullOrWhiteSpace(said) || !arguments.TryGetBoolean("suggested", out var suggested))
        {
            return ToolResult.Error("Which activity, and suggest it or not?");
        }

        if (ActivityCatalogue.Find(said) is not { } entry)
        {
            return ToolResult.Ok(NotKept(said));
        }

        if (string.IsNullOrEmpty(commander))
        {
            return ToolResult.Error("I do not know which Commander this is yet.");
        }

        if (ledger.IsSuggested(entry.Key, commander) == suggested)
        {
            return ToolResult.Ok($"{entry.Name} is already {(suggested ? "in" : "off")} the suggestions.");
        }

        ledger.SetSuggested(entry.Key, commander, suggested);

        var name = entry.Name.ToLowerInvariant();

        return ToolResult.Ok(
            suggested
                ? $"Back in the suggestions: {name}."
                : $"Not suggesting: {name}. It stays on the Activities page, with its date.");
    }

    private static string NotKept(string said) =>
        $"I don't keep a date for {said.Trim()}. The Activities page lists the {Words(ActivityCatalogue.All.Count)} I do.";

    private static string Words(int number) => SpokenNumbers.Expand(number.ToString(CultureInfo.InvariantCulture));

    private static string Date(DateTimeOffset at) =>
        GalacticTime.Galactic(at.ToUniversalTime()).ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
}
