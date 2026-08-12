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
    /// Matches the capability whose declared keyword appears in the input, preferring the
    /// longest keyword so a more specific phrase wins over a word it contains.
    /// <para>
    /// Only zero-argument tools are reachable for now: filling arguments from free text without
    /// a closed grammar is how a router starts guessing, and guessing is what this path exists
    /// to avoid. Argument-taking tools arrive with the closed vocabularies in Phase 6.
    /// </para>
    /// </summary>
    public KeywordMatch? Match(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var text = input.ToLowerInvariant();

        var candidates =
            from capability in registry.All
            from keyword in capability.Descriptor.Keywords
            where text.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal)
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
}
