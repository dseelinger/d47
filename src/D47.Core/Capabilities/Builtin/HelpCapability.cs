using System.Text;
using D47.Core.Conversation;
using D47.Core.Help;
using D47.Core.Input;

namespace D47.Core.Capabilities.Builtin;

/// <summary>"What can you do" (Phase 6), answered from the capability registry.</summary>
public static class HelpCapability
{
    public const string Id = "help";

    public const string DrillToolName = "drill_capabilities";

    public const string FindPhraseToolName = "find_phrase";

    /// <summary>How many phrases a leaf says before it stops, in order of how well they are known.</summary>
    private const int PhrasesPerLeaf = 2;

    /// <summary>How many phrases one match says before it stops.</summary>
    private const int PhrasesPerMatch = 3;

    /// <summary>
    /// Deliberately late-bound: the registry cannot be handed to a descriptor that is being built to go
    /// into it, and the phrase book is assembled from the router that is itself built after the registry
    /// (#229).
    /// </summary>
    public static CapabilityDescriptor Create(
        Func<CapabilityRegistry> registry, OfferWindow offers, Func<PhraseBook> phraseBook) => new()
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
        // JournalCapability documents — a bare word hijacks any sentence containing it, and "help me plot a
        // route" is not a request for a capability list.
        Keywords =
        [
            new CapabilityKeyword("what can you do", DrillToolName),
            new CapabilityKeyword("what can you help with", DrillToolName),
            new CapabilityKeyword("what are you capable of", DrillToolName),
            new CapabilityKeyword("what are your capabilities", DrillToolName),
            new CapabilityKeyword("list your capabilities", DrillToolName),
            new CapabilityKeyword("what do you do", DrillToolName),
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
                    + "listed here does not exist. Asked what to say, use find_phrase instead of this: an "
                    + "area listing names features, not the phrases that actually route.",
                AlwaysLoaded = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "group",
                        Type = ToolParameterType.String,
                        // Plain ASCII, no quotes.
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
            new ToolDefinition
            {
                Name = FindPhraseToolName,
                Description =
                    "Find the phrase or phrases that actually reach a goal, from the model-free router's own "
                    + "phrase book — never from memory. Use this before telling the Commander there is no set "
                    + "phrase for something, or that it cannot be done; the phrase book may hold one.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "goal",
                        Type = ToolParameterType.String,
                        Description = "What the Commander wants to do, in a few plain words.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) =>
                {
                    arguments.TryGetString("goal", out var goal);
                    return Task.FromResult(ToolResult.Ok(FindPhrase(phraseBook(), registry(), goal ?? string.Empty)));
                },
            },
            new ToolDefinition
            {
                Name = DrillToolName,
                Description = "Walk the spoken map of what D47 can do, one level at a time.",

                // Reached only by the Commander: a model asking "what can you do" gets get_capabilities,
                // never an offer that would then capture whatever the Commander says next.
                Protected = true,
                Handler = (_, _) =>
                {
                    var (text, offer) = LevelAnswer(HelpTaxonomy.Top, registry());
                    offers.Open(offer);
                    return Task.FromResult(ToolResult.Ok(text));
                },
            },
        ],
    };

    /// <summary>What a level says: the count, the names, and a question.</summary>
    internal static (string Text, Offer Offer) LevelAnswer(IReadOnlyList<HelpNode> children, CapabilityRegistry registry)
    {
        var names = children.Select(child => child.Name).ToArray();
        var text = $"{names.Length} areas: {Listed(names)}. Which one?";

        var offer = new Offer(
        [
            .. children.Select(child => new OfferChoice(
                child.Name, new OfferTarget.Answer(() => Answer(child, registry)))),
        ]);

        return (text, offer);
    }

    /// <summary>What picking one choice says, and what it opens next.</summary>
    internal static OfferAnswer Answer(HelpNode node, CapabilityRegistry registry)
    {
        if (node.CapabilityId is not { } capabilityId)
        {
            var (text, offer) = LevelAnswer(node.Children, registry);
            return new OfferAnswer(text, offer);
        }

        return new OfferAnswer(LeafAnswer(node, registry.Find(capabilityId)?.Descriptor));
    }

    /// <summary>A leaf: its sentence, one or two phrases to say, and its panel page.</summary>
    private static string LeafAnswer(HelpNode node, CapabilityDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return node.Sentence;
        }

        var parts = new List<string> { node.Sentence };

        var phrases = Phrases(descriptor).Take(PhrasesPerLeaf).ToArray();

        if (phrases.Length > 0)
        {
            parts.Add($"Say {Listed(phrases.Select(phrase => $"'{phrase}'").ToArray(), conjunction: "or")}.");
        }

        if (descriptor.Display is { ShowOnPanel: true, PanelTitle: { Length: > 0 } title })
        {
            parts.Add($"It has a page on the panel called {title}.");
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// What the Commander could say for this capability: its tool command phrases first, then its
    /// keywords, then its examples — the order a phrase is most likely to actually work.
    /// </summary>
    internal static IEnumerable<string> Phrases(CapabilityDescriptor descriptor) =>
        descriptor.Tools.SelectMany(tool => tool.Commands.Select(command => command.Phrase))
            .Concat(descriptor.Keywords.Select(keyword => keyword.Phrase))
            .Concat(descriptor.Examples)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>"a", "a, and b", "a, b, and c" — the join <see cref="Conversation.TurnLoop"/>'s "did you mean" uses.</summary>
    private static string Listed(IReadOnlyList<string> items, string conjunction = "and") => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        2 => $"{items[0]} {conjunction} {items[1]}",
        _ => $"{string.Join(", ", items.SkipLast(1))}, {conjunction} {items[^1]}",
    };

    /// <summary>
    /// Every phrase-book entry whose words reach the goal, grouped by what they reach so two phrases for
    /// the same thing are said together — matched per phrase rather than per capability, on the same
    /// content-word rule <see cref="HowDoI"/> matches a "how do I" goal with (#229).
    /// </summary>
    internal static string FindPhrase(PhraseBook book, CapabilityRegistry registry, string goal)
    {
        var goalWords = HowDoI.ContentWords(goal);

        if (goalWords.Count == 0)
        {
            return $"No phrase matched \"{goal}\".";
        }

        var threshold = Math.Min(goalWords.Count, HowDoI.MinSharedWords);

        var matches = book.Entries
            .Select(entry => (
                entry,
                shared: HowDoI.ContentWords(entry.Phrase).Intersect(goalWords, StringComparer.OrdinalIgnoreCase).Count()))
            .Where(scored => scored.shared >= threshold)
            .GroupBy(scored => PhraseBook.Target(scored.entry), StringComparer.Ordinal)
            .Select(group => (
                Description: WhatItDoes(group.First().entry, registry),
                Phrases: group.Select(scored => scored.entry.Phrase)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(PhrasesPerMatch)
                    .ToArray(),
                Shared: group.Max(scored => scored.shared)))
            .OrderByDescending(match => match.Shared)
            .ToArray();

        return matches.Length == 0
            ? $"No phrase matched \"{goal}\"."
            : string.Join(
                " ",
                matches.Select(match =>
                    $"{match.Description} Say {Listed(match.Phrases.Select(phrase => $"'{phrase}'").ToArray(), conjunction: "or")}."));
    }

    /// <summary>What a matched entry reaches, in plain words — never a tool name or an action id.</summary>
    private static string WhatItDoes(PhraseEntry entry, CapabilityRegistry registry)
    {
        if (entry.Row is { } row)
        {
            return entry.Value is { Length: > 0 } value ? $"Sets {row.Label} to {value}." : $"Reports {row.Label}.";
        }

        if (entry.Arguments.TryGetValue("action", out var actionId)
            && GameActions.All.FirstOrDefault(action => action.Id == actionId) is { } gameAction)
        {
            return $"Reaches {gameAction.Label}.";
        }

        var tool = entry.ToolName is { } toolName
            ? registry.Find(entry.CapabilityId)?.Descriptor.Tools
                .FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal))
            : null;

        return tool?.Description ?? registry.Find(entry.CapabilityId)?.Descriptor.Summary ?? "Something D47 can do.";
    }

    /// <summary>The overview, or one area in detail.</summary>
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
            ? DescribeArea(registry, group)
            : DescribeOverview(visible);
    }

    private static string DescribeOverview(RegisteredCapability[] visible)
    {
        var report = new StringBuilder($"I have {visible.Length} capabilities, in these areas:");

        foreach (var category in HelpTaxonomy.Top)
        {
            report.AppendLine();
            report.Append($"  {category.Name} — {category.Sentence}");
        }

        report.AppendLine();
        report.Append("Ask about an area by name for the detail.");

        return report.ToString();
    }

    private static string DescribeArea(CapabilityRegistry registry, string group)
    {
        if (Find(HelpTaxonomy.Top, group) is not { } node)
        {
            var areas = HelpTaxonomy.Top.Select(category => category.Name);
            return $"I have no area called \"{group}\". I have: {string.Join(", ", areas)}.";
        }

        return node.CapabilityId is { } capabilityId
            ? DescribeCapability(registry, capabilityId)
            : DescribeChildren(registry, node);
    }

    /// <summary>The node whose name contains <paramref name="name"/>, checked level by level.</summary>
    private static HelpNode? Find(IReadOnlyList<HelpNode> level, string name)
    {
        foreach (var node in level)
        {
            if (node.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        foreach (var node in level)
        {
            if (Find(node.Children, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string DescribeChildren(CapabilityRegistry registry, HelpNode node)
    {
        var report = new StringBuilder();

        foreach (var child in node.Children)
        {
            report.AppendLine(
                child.CapabilityId is { } capabilityId
                    ? DescribeCapability(registry, capabilityId)
                    : $"{child.Name}: {child.Sentence}");
        }

        return report.ToString().TrimEnd();
    }

    private static string DescribeCapability(CapabilityRegistry registry, string capabilityId)
    {
        var descriptor = registry.Find(capabilityId)!.Descriptor;
        var report = new StringBuilder($"{descriptor.Name}: {descriptor.Summary}");

        if (descriptor.Examples.Count > 0)
        {
            // The Commander's own words, not the tool names.
            report.AppendLine();
            report.Append($"  Try: {string.Join("; ", descriptor.Examples.Select(e => $"\"{e}\""))}");
        }

        return report.ToString();
    }
}
