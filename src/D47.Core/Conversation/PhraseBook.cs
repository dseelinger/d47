using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;

namespace D47.Core.Conversation;

/// <summary>Which declared list a model-free phrase came from.</summary>
public enum PhraseSource
{
    Keyword,

    /// <summary>A keyword that only counts when it was spoken.</summary>
    SpokenKeyword,

    InterruptKeyword,

    ToolCommand,

    SettingCommand,

    /// <summary>A phrase outside every descriptor: a macro, a standing offer, a found system.</summary>
    Dynamic,
}

/// <summary>One phrase the model-free router accepts, and what it reaches.</summary>
public sealed record PhraseEntry
{
    public required string Phrase { get; init; }

    public required PhraseSource Source { get; init; }

    public required string CapabilityId { get; init; }

    /// <summary>The tool reached, or null for a setting command or a keyword no single tool answers.</summary>
    public string? ToolName { get; init; }

    public IReadOnlyDictionary<string, string> Arguments { get; init; } =
        System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty;

    /// <summary>The row a setting command writes.</summary>
    public SettingRow? Row { get; init; }

    /// <summary>The value a setting command writes.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// Must never run on a guess: the tool is <see cref="ToolDefinition.Protected"/> or
    /// <see cref="ToolDefinition.SendsInput"/>, or the row is <see cref="SettingRow.Protected"/>.
    /// </summary>
    public required bool Guarded { get; init; }
}

/// <summary>A phrase a near miss could have meant, guarded when any phrase it stands for is.</summary>
public sealed record PhraseCandidate(string Phrase, PhraseMatch Match, bool Guarded);

/// <summary>Every phrase the model-free router accepts, with what each reaches.</summary>
public sealed class PhraseBook
{
    private PhraseBook(IReadOnlyList<PhraseEntry> entries) => Entries = entries;

    public IReadOnlyList<PhraseEntry> Entries { get; }

    /// <summary>
    /// The book for a registry and the dynamic commands live now, plus the clipboard and course phrases
    /// whether or not an offer is standing.
    /// </summary>
    public static PhraseBook From(CapabilityRegistry registry, IEnumerable<DynamicCommand> dynamicCommands)
    {
        var entries = new List<PhraseEntry>();

        foreach (var capability in registry.All.Select(registered => registered.Descriptor))
        {
            entries.AddRange(Keywords(capability, capability.Keywords, PhraseSource.Keyword));
            entries.AddRange(Keywords(capability, capability.SpokenKeywords, PhraseSource.SpokenKeyword));

            var interrupting = capability.Tools.FirstOrDefault(t => t.Interrupting && t.Parameters.Count == 0);

            entries.AddRange(capability.InterruptKeywords.Select(phrase => new PhraseEntry
            {
                Phrase = phrase,
                Source = PhraseSource.InterruptKeyword,
                CapabilityId = capability.Id,
                ToolName = interrupting?.Name,
                Guarded = interrupting is null ? AnyGuarded(capability) : Guards(interrupting),
            }));
        }

        foreach (var capability in registry.All.Select(registered => registered.Descriptor))
        {
            entries.AddRange(
                from tool in capability.Tools
                from command in tool.Commands
                select new PhraseEntry
                {
                    Phrase = command.Phrase,
                    Source = PhraseSource.ToolCommand,
                    CapabilityId = capability.Id,
                    ToolName = tool.Name,
                    Arguments = command.Arguments,
                    Guarded = Guards(tool),
                });
        }

        foreach (var capability in registry.All.Select(registered => registered.Descriptor))
        {
            entries.AddRange(
                from row in capability.Settings
                from command in row.Commands
                select new PhraseEntry
                {
                    Phrase = command.Phrase,
                    Source = PhraseSource.SettingCommand,
                    CapabilityId = capability.Id,
                    Row = row,
                    Value = command.Value,
                    Guarded = row.Protected,
                });
        }

        var dynamic = dynamicCommands.ToList();

        entries.AddRange(dynamic.Select(command => Dynamic(
            registry, command.Phrase, command.CapabilityId, command.ToolName, command.Arguments)));

        var claimed = dynamic.Select(command => command.Phrase).ToHashSet(StringComparer.Ordinal);

        entries.AddRange(
            ClipboardOffer.EveryPhrase
                .Where(phrase => !claimed.Contains(phrase))
                .Select(phrase => Dynamic(registry, phrase, NavigationCapability.Id, "copy_to_clipboard", null)));

        if (!claimed.Contains(CommunityGoalCourse.SetCourse))
        {
            entries.Add(Dynamic(registry, CommunityGoalCourse.SetCourse, NavigationCapability.Id, "plot_course", null));
        }

        return new PhraseBook(entries);
    }

    /// <summary>
    /// The phrases an utterance could have meant, best first: read the four ways
    /// <see cref="KeywordRouter.MatchSetting"/> reads it, one candidate per phrase and per thing reached,
    /// named by the phrase closest to what was said.
    /// </summary>
    public IReadOnlyList<PhraseCandidate> Candidates(string utterance, InputSource source)
    {
        var said = KeywordRouter.Utterance(utterance);

        string[] readings =
        [
            .. new[]
            {
                said,
                SpokenOpeners.Strip(said),
                SpokenTails.Strip(said),
                SpokenTails.Strip(SpokenOpeners.Strip(said)),
            }.Distinct(StringComparer.OrdinalIgnoreCase),
        ];

        var scored = Entries
            .Select((entry, order) => (Entry: entry, Order: order))
            .Where(item => source == InputSource.Spoken || item.Entry.Source != PhraseSource.SpokenKeyword)
            .Select(item => (
                item.Entry,
                item.Order,
                Match: readings
                    .Select(reading => PhraseScore.Score(reading, item.Entry.Phrase))
                    .Where(match => match is not null)
                    .OrderBy(match => match == PhraseMatch.Equivalent ? 0 : 1)
                    .FirstOrDefault(),
                Distance: readings.Min(reading => PhraseScore.Distance(reading, item.Entry.Phrase))))
            .Where(item => item.Match is not null)
            .OrderBy(item => item.Match == PhraseMatch.Equivalent ? 0 : 1)
            .ThenBy(item => item.Distance)
            .ThenBy(item => item.Order)
            .ToList();

        return
        [
            .. scored
                .GroupBy(item => Target(item.Entry), StringComparer.Ordinal)
                .Select(group => (Best: group.First(), Guarded: group.Any(item => item.Entry.Guarded)))
                .GroupBy(choice => choice.Best.Entry.Phrase, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PhraseCandidate(
                    group.First().Best.Entry.Phrase,
                    group.First().Best.Match!.Value,
                    group.Any(choice => choice.Guarded))),
        ];
    }

    /// <summary>What an entry reaches, so two phrases for the same thing are one candidate.</summary>
    internal static string Target(PhraseEntry entry)
    {
        if (entry.Row is { } row)
        {
            return $"setting {row.Key}={entry.Value}";
        }

        if (entry.ToolName is null)
        {
            return $"phrase {entry.Phrase.ToLowerInvariant()}";
        }

        var arguments = entry.Arguments
            .OrderBy(argument => argument.Key, StringComparer.Ordinal)
            .Select(argument => $"{argument.Key}={argument.Value}");

        return $"tool {entry.CapabilityId}/{entry.ToolName}?{string.Join('&', arguments)}";
    }

    private static IEnumerable<PhraseEntry> Keywords(
        CapabilityDescriptor capability, IEnumerable<CapabilityKeyword> keywords, PhraseSource source) =>
        from keyword in keywords
        let tool = KeywordRouter.Answering(capability, keyword)
        select new PhraseEntry
        {
            Phrase = keyword.Phrase,
            Source = source,
            CapabilityId = capability.Id,
            ToolName = tool?.Name,
            Arguments = keyword.Arguments,
            Guarded = tool is null ? AnyGuarded(capability) : Guards(tool),
        };

    /// <summary>A target the registry does not hold is guarded, since nothing says it is safe.</summary>
    private static PhraseEntry Dynamic(
        CapabilityRegistry registry,
        string phrase,
        string capabilityId,
        string toolName,
        IReadOnlyDictionary<string, string>? arguments)
    {
        var tool = registry.Find(capabilityId)?.Descriptor.Tools
            .FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal));

        return new PhraseEntry
        {
            Phrase = phrase,
            Source = PhraseSource.Dynamic,
            CapabilityId = capabilityId,
            ToolName = toolName,
            Arguments = arguments ?? System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty,
            Guarded = tool is null || Guards(tool),
        };
    }

    private static bool Guards(ToolDefinition tool) => tool.Protected || tool.SendsInput;

    private static bool AnyGuarded(CapabilityDescriptor capability) => capability.Tools.Any(Guards);
}
