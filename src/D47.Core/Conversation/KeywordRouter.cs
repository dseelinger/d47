using System.Text.RegularExpressions;
using D47.Core.Capabilities;

namespace D47.Core.Conversation;

public sealed record KeywordMatch(string CapabilityId, string ToolName);

/// <summary>A settings row a declared phrase asked for, and the value that phrase means.</summary>
public sealed record SettingCommandMatch(string CapabilityId, SettingRow Row, string? Value, string Phrase);

/// <summary>
/// The model-free command path (list.md Phase 3, "Ship's AI Unsure"). It exists for three
/// separate reasons, and it would be worth building for any one of them:
/// <list type="bullet">
/// <item>every input path must be answerable with no capabilities at all;</item>
/// <item>the whole turn path stays exercisable in tests with no provider;</item>
/// <item>safety-critical settings are reachable by voice <em>only</em> through here, never
/// through the LLM, because the model consumes untrusted text (architecture.md §7).</item>
/// </list>
/// <para>
/// Its vocabulary is a projection of the capability registry — the same
/// <see cref="CapabilityDescriptor.Keywords"/> the descriptor already declares — so there is no
/// second list to keep in step.
/// </para>
/// </summary>
public sealed class KeywordRouter(CapabilityRegistry registry)
{
    /// <summary>
    /// Matches the capability whose declared keyword phrase appears in the input as a whole
    /// phrase, preferring the longest so a more specific phrase wins over one it contains.
    /// <para>
    /// Matching is whole-word and phrase-level rather than substring, and this is load-bearing.
    /// A bare word match reads "Where is Iran?" as a request for the Commander's own position and
    /// answers it with journal data — confidently, and about something nobody asked. The router is
    /// the one answer path with no guardrail block in front of it, so precision here is the only
    /// thing standing between it and an invented answer. A miss costs a fall-through to the model;
    /// a false positive costs a wrong answer delivered with certainty.
    /// </para>
    /// <para>
    /// Only zero-argument tools are reachable for now: filling arguments from free text without a
    /// closed grammar is how a router starts guessing. Argument-taking tools arrive with the
    /// closed vocabularies in Phase 6.
    /// </para>
    /// </summary>
    public KeywordMatch? Match(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var candidates =
            from capability in registry.All
            from keyword in capability.Descriptor.Keywords
            where ContainsPhrase(input, keyword)
            orderby keyword.Length descending
            select capability;

        foreach (var capability in candidates)
        {
            // No *required* parameters, rather than no parameters at all. The router invokes
            // with empty arguments, so an optional parameter is no obstacle — and the stricter
            // test made a capability unreachable by voice purely for offering a refinement it
            // does not need. Spoken help was exactly that: "what can you do" matched nothing,
            // because get_capabilities takes an optional group to expand.
            //
            // Required parameters still disqualify a tool. The router deliberately does not
            // extract values from free text — a router that guesses at arguments is a router
            // that calls the right tool with the wrong ones.
            var tool = capability.Descriptor.Tools.FirstOrDefault(t => !t.Parameters.Any(p => p.Required));
            if (tool is not null)
            {
                return new KeywordMatch(capability.Descriptor.Id, tool.Name);
            }
        }

        return null;
    }

    /// <summary>
    /// Matches only the commands allowed to answer while a turn is already running.
    /// <para>
    /// Kept separate from <see cref="Match"/> because a caller asking this question is asking
    /// something narrower: not "can anything answer this" but "may this be answered *now*,
    /// ahead of the turn in flight". A surface that gates input on a turn being in progress —
    /// which every surface must, or a second question would trample the first — has to ask this
    /// before it applies that gate, or instant silence is gated behind the very thing it exists
    /// to interrupt (list.md Phase 5).
    /// </para>
    /// </summary>
    public KeywordMatch? MatchInterrupting(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // The interrupt-only vocabulary first: it exists precisely for phrases too broad to sit
        // in the general list, so it would never be reached if the general list were consulted
        // first.
        var byInterruptPhrase =
            (from capability in registry.All
             from keyword in capability.Descriptor.InterruptKeywords
             where ContainsPhrase(input, keyword)
             orderby keyword.Length descending
             let tool = capability.Descriptor.Tools.FirstOrDefault(t => t.Interrupting && t.Parameters.Count == 0)
             where tool is not null
             select new KeywordMatch(capability.Descriptor.Id, tool!.Name))
            .FirstOrDefault();

        if (byInterruptPhrase is not null)
        {
            return byInterruptPhrase;
        }

        if (Match(input) is not { } match)
        {
            return null;
        }

        var matched = registry.Find(match.CapabilityId)?.Descriptor.Tools
            .FirstOrDefault(t => t.Name == match.ToolName);

        return matched?.Interrupting == true ? match : null;
    }

    /// <summary>
    /// Matches a settings command phrase — the model-free way to reach a protected row.
    /// <para>
    /// Tried before <see cref="Match"/> because it is the more specific claim: a phrase declared
    /// against a row means one row and one value, with nothing inferred.
    /// </para>
    /// <para>
    /// The whole utterance has to be the phrase, not merely contain it, and that is one notch
    /// stricter than <see cref="Match"/> on purpose. Asking "what does personality off actually
    /// change" is a question about a setting, not an instruction to change it, and this is the
    /// one router path that writes. A miss costs a fall-through to the model; a false positive
    /// silently changes something nobody asked about. Variants are declared per row rather than
    /// inferred here, because stripping politeness words is guessing by another name.
    /// </para>
    /// </summary>
    public SettingCommandMatch? MatchSetting(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var utterance = Utterance(input);

        return (
            from capability in registry.All
            from row in capability.Descriptor.Settings
            from command in row.Commands
            where string.Equals(utterance, Utterance(command.Phrase), StringComparison.OrdinalIgnoreCase)
            select new SettingCommandMatch(capability.Descriptor.Id, row, command.Value, command.Phrase))
            .FirstOrDefault();
    }

    /// <summary>
    /// One utterance, reduced to what was said: no surrounding punctuation, no doubled spaces,
    /// and apostrophes normalised the same way <see cref="ContainsPhrase"/> normalises them.
    /// </summary>
    private static string Utterance(string text) =>
        string.Join(' ', Normalise(text).Split(
            [' ', '\t', '\r', '\n', '.', ',', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

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

        // Apostrophes vary by keyboard and by autocorrect; "what's" and "what’s" must behave
        // the same, and neither should be the reason a command does not route.
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
