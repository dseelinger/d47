using System.Text;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// "What can you do" (list.md Phase 6), answered from the capability registry.
/// <para>
/// <b>The model is never asked what d47 can do.</b> That is the whole point of the item, and it
/// is not a stylistic preference: a model asked to describe its own capabilities produces a
/// fluent, confident, partly invented list, and the Commander cannot tell which half is which.
/// The registry already holds every capability's identity, summary and example phrasings
/// (architecture.md D5), so the honest answer is a projection of it — one that cannot name a
/// capability that does not exist, because it is built from the ones that do.
/// </para>
/// <para>
/// Groups first, then capabilities, then detail and examples, ranked by real usage — which is
/// the shape a spoken answer needs. Reading 40 capabilities aloud is not help; naming the
/// groups and offering to go deeper is.
/// </para>
/// </summary>
public static class HelpCapability
{
    public const string Id = "help";

    /// <summary>
    /// Deliberately late-bound: the registry cannot be handed to a descriptor that is being
    /// built to go into it. The app supplies the accessor once both exist.
    /// </summary>
    public static CapabilityDescriptor Create(Func<CapabilityRegistry> registry) => new()
    {
        Id = Id,
        Group = "Foundation",
        Name = "Help",
        Summary = "Say what D47 can actually do, projected from the capability registry rather than invented.",
        Examples =
        [
            "what can you do",
            "what can you help with",
            "tell me about the voice capabilities",
        ],

        // Phrases that can only be a request for help. "help" alone is refused for the reason
        // JournalCapability documents — a bare word hijacks any sentence containing it, and
        // "help me plot a route" is not a request for a capability list.
        Keywords =
        [
            "what can you do",
            "what can you help with",
            "what are you capable of",
            "what are your capabilities",
            "list your capabilities",
            "what do you do",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Help", Order = 10 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_capabilities",
                Description =
                    "List what D47 can do, from its own capability registry. Use this instead of describing "
                    + "D47's abilities from memory — this is the only accurate source, and anything not "
                    + "listed here does not exist.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "group",
                        Type = ToolParameterType.String,
                        // Plain ASCII, no quotes. The schema serializer escapes both, and this
                        // string is quoted verbatim in the capability page by the docs gate.
                        Description =
                            "Optional group name to expand, such as Voice. Omit for the overview.",
                    },
                ],
                Handler = (arguments, _) =>
                {
                    arguments.TryGetString("group", out var group);
                    return Task.FromResult(ToolResult.Ok(Describe(registry(), group)));
                },
            },
        ],
    };

    /// <summary>
    /// The overview, or one group in detail. Both are projections of the registry, so a
    /// capability added in a later phase appears here with no change to this file.
    /// </summary>
    public static string Describe(CapabilityRegistry registry, string? group = null)
    {
        var visible = registry.All
            .Where(capability => capability.Descriptor.Id != Id)
            .ToArray();

        if (visible.Length == 0)
        {
            return "I have no capabilities registered, which should not be possible.";
        }

        return group is { Length: > 0 }
            ? DescribeGroup(registry, visible, group)
            : DescribeOverview(registry, visible);
    }

    private static string DescribeOverview(CapabilityRegistry registry, RegisteredCapability[] visible)
    {
        var report = new StringBuilder($"I have {visible.Length} capabilities, in these groups:");

        foreach (var group in Grouped(registry, visible))
        {
            report.AppendLine();
            report.Append($"  {group.Key} — {string.Join(", ", group.Select(c => c.Descriptor.Name))}");
        }

        report.AppendLine();
        report.Append("Ask about a group by name for the detail.");

        return report.ToString();
    }

    private static string DescribeGroup(
        CapabilityRegistry registry,
        RegisteredCapability[] visible,
        string group)
    {
        var matching = Ranked(
                registry,
                visible.Where(c => c.Descriptor.Group.Contains(group, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (matching.Length == 0)
        {
            // Names the real groups rather than saying "no". A Commander who asked for the wrong
            // one wants the right one, and this is the moment d47 knows both.
            var groups = Grouped(registry, visible).Select(g => g.Key);
            return $"I have no group called \"{group}\". I have: {string.Join(", ", groups)}.";
        }

        var report = new StringBuilder();

        foreach (var capability in matching)
        {
            var descriptor = capability.Descriptor;

            report.AppendLine($"{descriptor.Name}: {descriptor.Summary}");

            if (descriptor.Examples.Count > 0)
            {
                // The Commander's own words, not the tool names. Example phrasings are what
                // makes a capability list actionable by voice.
                report.AppendLine($"  Try: {string.Join("; ", descriptor.Examples.Select(e => $"\"{e}\""))}");
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Groups in declaration order, with the capabilities inside each one ranked by use. The
    /// group order is stable on purpose — a spoken list whose headings move between askings is
    /// harder to follow than one that does not.
    /// </summary>
    private static IEnumerable<IGrouping<string, RegisteredCapability>> Grouped(
        CapabilityRegistry registry,
        IEnumerable<RegisteredCapability> capabilities) =>
        Ranked(registry, capabilities)
            .GroupBy(capability => capability.Descriptor.Group, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<RegisteredCapability> Ranked(
        CapabilityRegistry registry,
        IEnumerable<RegisteredCapability> capabilities)
    {
        // Registration order is the tiebreak, so an unused set comes out in the order the app
        // declared it rather than in dictionary order.
        var order = registry.All
            .Select((capability, index) => (capability.Descriptor.Id, index))
            .ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);

        return capabilities
            .OrderByDescending(capability => registry.UseCountOf(capability.Descriptor.Id))
            .ThenBy(capability => order.GetValueOrDefault(capability.Descriptor.Id, int.MaxValue));
    }
}
