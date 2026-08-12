using System.Text.RegularExpressions;
using D47.Core.Capabilities;

namespace D47.Core.Conversation;

public sealed record KeywordMatch(string CapabilityId, string ToolName);

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
            var tool = capability.Descriptor.Tools.FirstOrDefault(t => t.Parameters.Count == 0);
            if (tool is not null)
            {
                return new KeywordMatch(capability.Descriptor.Id, tool.Name);
            }
        }

        return null;
    }

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
