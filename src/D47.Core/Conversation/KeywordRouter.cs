using System.Text.RegularExpressions;
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

        var words = bounded ? Words(input).Length : (int?)null;

        var candidates =
            from capability in registry.All
            from keyword in Vocabulary(capability.Descriptor, source)
            where MatchesAsCommand(input, keyword.Phrase, words)
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
    private static ToolDefinition? Answering(CapabilityDescriptor descriptor, CapabilityKeyword keyword)
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

        // The interrupt-only vocabulary first: it exists precisely for phrases too broad to sit in the
        // general list, so it would never be reached if the general list were consulted first.
        var byInterruptPhrase =
            (from capability in registry.All
             from keyword in capability.Descriptor.InterruptKeywords
             where MatchesAsCommand(input, keyword, words: null)
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

        // As said, before as stripped — and that order is the whole of the safety here. `PersonaCapability`
        // declares "switch to Directive 47", which *opens with an opener*, so stripping first would have left
        // "directive 47" matching nothing and silently taken persona switching by voice away.
        return Matching(utterance)
            ?? Matching(SpokenOpeners.Strip(utterance))
            ?? Matching(SpokenTails.Strip(utterance))
            ?? Matching(SpokenTails.Strip(SpokenOpeners.Strip(utterance)));

        SettingCommandMatch? Matching(string said) => (
            from capability in registry.All
            from row in capability.Descriptor.Settings
            from command in row.Commands
            where string.Equals(said, Utterance(command.Phrase), StringComparison.OrdinalIgnoreCase)
            select new SettingCommandMatch(capability.Descriptor.Id, row, command.Value, command.Phrase))
            .FirstOrDefault();
    }

    /// <summary>Matches a tool command phrase — the model-free way to act on the game (Phase 10).</summary>
    public ToolCommandMatch? MatchToolCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var utterance = Utterance(input);

        // The Commander's own phrases first.
        var dynamic = (
            from command in dynamicCommands?.Invoke() ?? []
            where string.Equals(utterance, Utterance(command.Phrase), StringComparison.OrdinalIgnoreCase)
            orderby command.Phrase.Length descending
            select new ToolCommandMatch(
                command.CapabilityId,
                command.ToolName,
                new ToolArguments(command.Arguments),
                command.Phrase))
            .FirstOrDefault();

        if (dynamic is not null)
        {
            return dynamic;
        }

        return (
            from capability in registry.All
            from tool in capability.Descriptor.Tools
            from command in tool.Commands
            where string.Equals(utterance, Utterance(command.Phrase), StringComparison.OrdinalIgnoreCase)

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

    /// <summary>
    /// One utterance, reduced to what was said: no surrounding punctuation, no doubled spaces, and
    /// apostrophes normalised the same way <see cref="ContainsPhrase"/> normalises them.
    /// </summary>
    private static string Utterance(string text) => string.Join(' ', Words(text));

    /// <summary>
    /// The words of an utterance, which is the one split <see cref="Utterance"/> and the length bound
    /// both work from, so punctuation and doubled spaces cannot make them disagree.
    /// </summary>
    private static string[] Words(string text) =>
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
    private static bool MatchesAsCommand(string text, string phrase, int? words) =>
        ContainsPhrase(text, phrase)
        && (words is not { } count || count <= Words(phrase).Length + MaxWordsAroundAKeyword);

    /// <summary>
    /// True when the phrase appears in the text bounded by word edges, so "docked" does not match
    /// inside a longer word and "where am i" only matches those three words in that order.
    /// </summary>
    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        // Apostrophes vary by keyboard and by autocorrect; "what's" and "what’s" must behave the same, and
        // neither should be the reason a command does not route.
        var normalisedText = Normalise(text);
        var normalisedPhrase = Normalise(phrase);

        return Regex.IsMatch(
            normalisedText,
            $@"\b{Regex.Escape(normalisedPhrase)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalise(string value) =>
        value.Replace('’', '\'').Replace('ʼ', '\'');
}
