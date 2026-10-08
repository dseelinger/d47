using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Capabilities;

namespace D47.Core.Conversation;

/// <summary>How an input reached d47.</summary>
public enum InputSource
{
    /// <summary>The ask box.</summary>
    Typed,

    /// <summary>Transcribed from the microphone.</summary>
    Spoken,
}

public sealed record KeywordMatch(string CapabilityId, string ToolName)
{
    /// <summary>
    /// What the matched keyword declared it means, where the tool answers more than one question
    /// (#406).
    /// </summary>
    public ToolArguments Arguments { get; init; } = ToolArguments.Empty;
}

/// <summary>A settings row a declared phrase asked for, and the value that phrase means.</summary>
public sealed record SettingCommandMatch(string CapabilityId, SettingRow Row, string? Value, string Phrase);

/// <summary>A phrase that is not in any descriptor, because whoever wrote it was the Commander.</summary>
public sealed record DynamicCommand(
    string Phrase,
    string CapabilityId,
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments);

/// <summary>A tool a declared phrase asked for, and the arguments that phrase means.</summary>
public sealed record ToolCommandMatch(
    string CapabilityId,
    string ToolName,
    ToolArguments Arguments,
    string Phrase);

/// <summary>The model-free command path (Phase 3, "Ship's AI Unsure").</summary>
public sealed class KeywordRouter(
    CapabilityRegistry registry,
    Func<IEnumerable<DynamicCommand>>? dynamicCommands = null)
{
    /// <summary>
    /// Matches the capability whose declared keyword phrase appears in the input as a whole phrase,
    /// preferring the longest so a more specific phrase wins over one it contains.
    /// </summary>
    private static IEnumerable<CapabilityKeyword> Vocabulary(CapabilityDescriptor descriptor, InputSource source) =>
        source == InputSource.Spoken
            ? descriptor.Keywords.Concat(descriptor.SpokenKeywords)
            : descriptor.Keywords;

    /// <summary>Every phrase this router accepts, over its registry and the dynamic commands live now.</summary>
    public PhraseBook Book => PhraseBook.From(registry, dynamicCommands?.Invoke() ?? []);

    public KeywordMatch? Match(string input, InputSource source = InputSource.Typed) =>
        Match(input, source, bounded: true);

    /// <summary>
    /// The same match, with the length bound made explicit so the interrupt path can turn it off.
    /// </summary>
    private KeywordMatch? Match(string input, InputSource source, bool bounded)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var folded = Folded(input);
        var words = bounded ? Words(folded).Length : (int?)null;

        var candidates =
            from capability in registry.All
            from keyword in Vocabulary(capability.Descriptor, source)
            where MatchesAsCommand(folded, keyword.Phrase, words)
            orderby keyword.Phrase.Length descending
            select (capability, keyword);

        foreach (var (capability, keyword) in candidates)
        {
            if (Answering(capability.Descriptor, keyword) is { } tool)
            {
                return new KeywordMatch(capability.Descriptor.Id, tool.Name)
                {
                    Arguments = new ToolArguments(keyword.Arguments),
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Which tool a matched keyword reaches, or null when the router would have to guess (#161).
    /// </summary>
    internal static ToolDefinition? Answering(CapabilityDescriptor descriptor, CapabilityKeyword keyword)
    {
        // No *required* parameters, rather than no parameters at all.
        var eligible = descriptor.Tools.Where(t => !t.Parameters.Any(p => p.Required)).ToList();

        if (keyword.ToolName is { Length: > 0 } named)
        {
            // A named tool that is not eligible is a declaration bug rather than a phrasing the Commander got
            // wrong, so it declines here and is caught by the test that walks every declared keyword.
            return eligible.FirstOrDefault(t => string.Equals(t.Name, named, StringComparison.Ordinal));
        }

        return eligible.Count == 1 ? eligible[0] : null;
    }

    /// <summary>Matches only the commands allowed to answer while a turn is already running.</summary>
    public KeywordMatch? MatchInterrupting(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var folded = Folded(input);

        // The interrupt-only vocabulary first: it exists precisely for phrases too broad to sit in the
        // general list, so it would never be reached if the general list were consulted first.
        var byInterruptPhrase =
            (from capability in registry.All
             from keyword in capability.Descriptor.InterruptKeywords
             where MatchesAsCommand(folded, keyword, words: null)
             orderby keyword.Length descending
             let tool = capability.Descriptor.Tools.FirstOrDefault(t => t.Interrupting && t.Parameters.Count == 0)
             where tool is not null
             select new KeywordMatch(capability.Descriptor.Id, tool!.Name))
            .FirstOrDefault();

        if (byInterruptPhrase is not null)
        {
            return byInterruptPhrase;
        }

        if (Match(input, InputSource.Typed, bounded: false) is not { } match)
        {
            return null;
        }

        var matched = registry.Find(match.CapabilityId)?.Descriptor.Tools
            .FirstOrDefault(t => t.Name == match.ToolName);

        return matched?.Interrupting == true ? match : null;
    }

    /// <summary>Matches a settings command phrase — the model-free way to reach a protected row.</summary>
    public SettingCommandMatch? MatchSetting(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var utterance = Utterance(input);

        // As said first: `PersonaCapability` declares "switch to Directive 47", which opens with an opener.
        return Readings(utterance).Select(Matching).FirstOrDefault(match => match is not null);

        SettingCommandMatch? Matching(string said)
        {
            var folded = Folded(said);

            return (
                from capability in registry.All
                from row in capability.Descriptor.Settings
                from command in row.Commands
                where string.Equals(folded, Folded(command.Phrase), StringComparison.OrdinalIgnoreCase)
                select new SettingCommandMatch(capability.Descriptor.Id, row, command.Value, command.Phrase))
                .FirstOrDefault();
        }
    }

    /// <summary>Matches a tool command phrase — the model-free way to act on the game (Phase 10).</summary>
    public ToolCommandMatch? MatchToolCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var utterance = Utterance(input);

        // As said first, then stripped; within a reading, the Commander's own phrases first.
        return Readings(utterance).Select(Matching).FirstOrDefault(match => match is not null);

        ToolCommandMatch? Matching(string said)
        {
            var folded = Folded(said);

            var dynamic = (
                from command in dynamicCommands?.Invoke() ?? []
                where string.Equals(folded, Folded(command.Phrase), StringComparison.OrdinalIgnoreCase)
                orderby command.Phrase.Length descending
                select new ToolCommandMatch(
                    command.CapabilityId,
                    command.ToolName,
                    new ToolArguments(command.Arguments),
                    command.Phrase))
                .FirstOrDefault();

            return dynamic ?? (
                from capability in registry.All
                from tool in capability.Descriptor.Tools
                from command in tool.Commands
                where string.Equals(folded, Folded(command.Phrase), StringComparison.OrdinalIgnoreCase)

                // A phrase that is only an answer while there is a question.
                where command.When?.Invoke() ?? true
                orderby command.Phrase.Length descending
                select new ToolCommandMatch(
                    capability.Descriptor.Id,
                    tool.Name,
                    new ToolArguments(command.Arguments),
                    command.Phrase))
                .FirstOrDefault();
        }
    }

    /// <summary>The ways an utterance is read, in order: as said, opener removed, tail removed, both removed.</summary>
    internal static IReadOnlyList<string> Readings(string said) =>
    [
        .. new[]
        {
            said,
            SpokenOpeners.Strip(said),
            SpokenTails.Strip(said),
            SpokenTails.Strip(SpokenOpeners.Strip(said)),
        }.Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// One utterance, reduced to what was said: no surrounding punctuation, no doubled spaces, and
    /// apostrophes normalised the same way <see cref="ContainsPhrase"/> normalises them.
    /// </summary>
    internal static string Utterance(string text) => string.Join(' ', Words(text));

    /// <summary>
    /// The words of an utterance, which is the one split <see cref="Utterance"/> and the length bound
    /// both work from, so punctuation and doubled spaces cannot make them disagree.
    /// </summary>
    internal static string[] Words(string text) =>
        Normalise(text).Split(
            [' ', '\t', '\r', '\n', '.', ',', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// How many words of surrounding sentence a declared keyword may carry and still be read as the
    /// command it names.
    /// </summary>
    private const int MaxWordsAroundAKeyword = 6;

    /// <summary>A keyword may not hijack a sentence.</summary>
    /// <param name="words">
    /// How many words the utterance holds, counted once by the caller rather than once per declared
    /// phrase — every capability's whole vocabulary is walked for every utterance.
    /// </param>
    /// <remarks>
    /// Both counts are taken after <see cref="Folded"/>, so a phrase carrying more than one "the" is not
    /// allowed extra surrounding words for it (#525).
    /// </remarks>
    private static bool MatchesAsCommand(string folded, string phrase, int? words)
    {
        var foldedPhrase = Folded(phrase);

        return ContainsPhrase(folded, foldedPhrase)
            && (words is not { } count || count <= Words(foldedPhrase).Length + MaxWordsAroundAKeyword);
    }

    /// <summary>
    /// True when the phrase appears in the text bounded by word edges, so "docked" does not match
    /// inside a longer word and "where am i" only matches those three words in that order. Both sides
    /// must already have passed through <see cref="Folded"/>.
    /// </summary>
    private static bool ContainsPhrase(string folded, string foldedPhrase)
    {
        if (string.IsNullOrWhiteSpace(foldedPhrase))
        {
            return false;
        }

        return Regex.IsMatch(
            folded,
            $@"\b{Regex.Escape(foldedPhrase)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Apostrophes vary by keyboard and by autocorrect; "what's" and "what’s" are the same word.</summary>
    private static string Normalise(string value) =>
        value.Replace('’', '\'').Replace('ʼ', '\'');

    /// <summary>
    /// An utterance or declared phrase with every "the" removed, so a Commander who says a declared
    /// phrase without its "the" (or adds one where none was declared) still reaches the same target
    /// (#525). <see cref="Utterance"/> leaves "the" in because most of its call sites are not phrase
    /// comparison.
    /// </summary>
    internal static string WithoutThe(string text) =>
        string.Join(' ', Words(text).Where(word => !string.Equals(word, "the", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Runs of words written more than one way, each with the single spelling they fold to. Longest
    /// first, so a longer run is replaced before a shorter one inside it.
    /// </summary>
    private static readonly IReadOnlyList<(string[] Run, string Spelling)> Spellings =
        [.. new (string Run, string Spelling)[]
            {
                ("per cent", "percent"),
                ("super cruise", "supercruise"),
                ("heatsink", "heat sink"),
                ("hard points", "hardpoints"),
                ("e c m", "ecm"),
            }
            .Select(entry => (Run: entry.Run.Split(' '), entry.Spelling))
            .OrderByDescending(entry => entry.Run.Length)];

    /// <summary>
    /// The form an utterance and a declared phrase are both reduced to before they are compared:
    /// <see cref="WithoutThe"/>, with hyphens as spaces, "%" as "percent", whole numbers 0 to 100 as
    /// words and the <see cref="Spellings"/> table applied. For comparison only; never shown or spoken.
    /// </summary>
    internal static string Folded(string text)
    {
        var words = WithoutThe(text.Replace("%", " percent ", StringComparison.Ordinal))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(NumberAsWords)
            .SelectMany(word => word.Split('-', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        var folded = new List<string>(words.Count);

        for (var i = 0; i < words.Count;)
        {
            var spelling = Spellings.FirstOrDefault(entry =>
                i + entry.Run.Length <= words.Count
                && entry.Run.Select((word, j) => string.Equals(word, words[i + j], StringComparison.OrdinalIgnoreCase)).All(same => same));

            if (spelling.Run is not null)
            {
                folded.Add(spelling.Spelling);
                i += spelling.Run.Length;
            }
            else
            {
                folded.Add(words[i]);
                i += 1;
            }
        }

        return string.Join(' ', folded);
    }

    /// <summary>A word of digits from 0 to 100 as the words it is said in; any other word unchanged.</summary>
    private static IEnumerable<string> NumberAsWords(string word) =>
        word.Length <= 3 && word.All(char.IsAsciiDigit) && int.Parse(word, CultureInfo.InvariantCulture) <= 100
            ? SpokenNumbers.Expand(word).Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)
            : [word];
}
