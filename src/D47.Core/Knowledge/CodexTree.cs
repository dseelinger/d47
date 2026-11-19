namespace D47.Core.Knowledge;

/// <summary>A place in the <see cref="CodexTree"/>: the root, a family, a group, a species, or an entry.</summary>
public sealed class CodexNode
{
    private readonly List<CodexNode> _children = [];

    internal CodexNode(string key, string name, CodexNode? parent, CodexEntry? entry)
    {
        Key = key;
        Name = name;
        Parent = parent;
        Entry = entry;
        ShortName = parent is null || parent.Parent is null ? name : CodexTree.ShortNameOf(name, parent.Name);
    }

    public string Key { get; }

    public string Name { get; }

    /// <summary>The part of <see cref="Name"/> its parent's name does not already say: "Aurasus" under "Bacterium".</summary>
    public string ShortName { get; }

    /// <summary>Null for the root.</summary>
    public CodexNode? Parent { get; }

    public IReadOnlyList<CodexNode> Children => _children;

    /// <summary>The table row this node stands for: a Canonn entry, or a star or planet row; null for the root, a family, a group or a species.</summary>
    public CodexEntry? Entry { get; }

    internal List<CodexNode> ChildList => _children;
}

/// <summary>
/// The codex tables as one tree: the root, then the families, then star and planet rows under their parent rows, and
/// Canonn entries under their group (<see cref="CodexEntry.Group"/>) and, where a group's entries are named
/// "Species - Colour", under their species. A family with no star or planet rows and one group lists its entries directly.
/// </summary>
public sealed class CodexTree
{
    public const string RootKey = "codex";

    /// <summary>The families in their order under the root; any other subcategory follows by name.</summary>
    public static readonly IReadOnlyList<string> FamilyOrder =
    [
        "Stars",
        "Gas giant planets",
        "Terrestrial planets",
        "Organic structures",
        "Geology and anomalies",
        "Guardian objects",
    ];

    private static int FamilyRank(string family)
    {
        for (var at = 0; at < FamilyOrder.Count; at++)
        {
            if (string.Equals(FamilyOrder[at], family, StringComparison.Ordinal))
            {
                return at;
            }
        }

        return FamilyOrder.Count;
    }

    /// <summary>The families whose Canonn entries are one group of the given name, whatever Canonn's group says.</summary>
    private static readonly Dictionary<string, string> OneGroupFamilies = new(StringComparer.Ordinal)
    {
        ["Gas giant planets"] = "Green gas giants",
    };

    private readonly Dictionary<string, CodexNode> _nodes;

    private CodexTree(CodexNode root, Dictionary<string, CodexNode> nodes)
    {
        Root = root;
        _nodes = nodes;
    }

    public CodexNode Root { get; }

    /// <summary>The node of a key: <see cref="RootKey"/>, a family, group or species key, or an entry's key.</summary>
    public CodexNode? Node(string key) => _nodes.GetValueOrDefault(key);

    public static CodexTree Of(CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var nodes = new Dictionary<string, CodexNode>(StringComparer.Ordinal);
        var root = new CodexNode(RootKey, "Codex", null, null);
        nodes[RootKey] = root;

        var families = table.All
            .GroupBy(entry => entry.SubCategory, StringComparer.Ordinal)
            .OrderBy(family => FamilyRank(family.Key))
            .ThenBy(family => family.Key, StringComparer.Ordinal);

        foreach (var family in families)
        {
            var familyNode = Child(nodes, root, CodexKinds.Slug(family.Key), family.Key, null);
            var kinds = family.Where(entry => entry.Kind is not null).ToList();
            var canonn = family.Where(entry => entry.Kind is null).ToList();

            AddKinds(nodes, familyNode, kinds);
            AddCanonn(nodes, familyNode, canonn, kinds.Count > 0);
        }

        foreach (var node in nodes.Values)
        {
            if (node != root)
            {
                node.ChildList.Sort(static (a, b) =>
                    StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name) is var byName and not 0
                        ? byName
                        : StringComparer.Ordinal.Compare(a.Key, b.Key));
            }
        }

        return new CodexTree(root, nodes);
    }

    /// <summary>
    /// The name less its parent's: less a leading parent name ("Bacterium Aurasus - Teal" under "Bacterium Aurasus" is
    /// "Teal"), a trailing one ("Roseum Brain Tree" under "Brain Tree" is "Roseum"), or the leading comma-separated parts
    /// it shares ("Icy body, not terraformable, Argon" under "Icy body, not terraformable, with atmosphere" is "Argon");
    /// otherwise the whole name.
    /// </summary>
    public static string ShortNameOf(string name, string parentName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parentName);

        string? rest = null;

        if (parentName.Length > 0 && name.Length > parentName.Length && name.StartsWith(parentName, StringComparison.Ordinal)
            && name[parentName.Length] is ' ' or ',')
        {
            rest = name[parentName.Length..].TrimStart(' ', ',', '-');
        }
        else if (parentName.Length > 0 && name.EndsWith(" " + parentName, StringComparison.Ordinal))
        {
            rest = name[..^(parentName.Length + 1)].TrimEnd(' ', ',', '-');
        }
        else
        {
            var parts = name.Split(", ");
            var parentParts = parentName.Split(", ");
            var shared = 0;

            while (shared < parts.Length - 1 && shared < parentParts.Length && string.Equals(parts[shared], parentParts[shared], StringComparison.Ordinal))
            {
                shared++;
            }

            if (shared > 0)
            {
                rest = string.Join(", ", parts[shared..]);
            }
        }

        return string.IsNullOrWhiteSpace(rest) ? name : rest;
    }

    private static void AddKinds(Dictionary<string, CodexNode> nodes, CodexNode familyNode, List<CodexEntry> kinds)
    {
        var keys = kinds.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var byParent = kinds.ToLookup(entry => entry.Kind!.ParentKey is { } parent && keys.Contains(parent) ? parent : null, StringComparer.Ordinal);
        var placed = new HashSet<string>(StringComparer.Ordinal);

        void Place(CodexNode parent, IEnumerable<CodexEntry> rows)
        {
            foreach (var row in rows)
            {
                if (placed.Add(row.Key))
                {
                    var node = Child(nodes, parent, row.Key, row.Name, row);
                    Place(node, byParent[row.Key]);
                }
            }
        }

        Place(familyNode, byParent[null]);
        Place(familyNode, kinds.Where(entry => !placed.Contains(entry.Key)).ToList());
    }

    private static void AddCanonn(Dictionary<string, CodexNode> nodes, CodexNode familyNode, List<CodexEntry> entries, bool familyHasKinds)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var family = familyNode.Name;

        if (OneGroupFamilies.TryGetValue(family, out var oneGroup))
        {
            AddGroup(nodes, Child(nodes, familyNode, familyNode.Key + "." + CodexKinds.Slug(oneGroup), oneGroup, null), entries);
            return;
        }

        var groups = entries.GroupBy(entry => entry.Group, StringComparer.Ordinal).ToList();

        if (!familyHasKinds && groups.Count == 1)
        {
            AddGroup(nodes, familyNode, entries);
            return;
        }

        foreach (var group in groups)
        {
            var members = group.ToList();

            if (group.Key is null)
            {
                AddGroup(nodes, familyNode, members);
                continue;
            }

            var name = GroupName(group.Key, members);
            AddGroup(nodes, Child(nodes, familyNode, familyNode.Key + "." + CodexKinds.Slug(name), name, null), members);
        }
    }

    private static void AddGroup(Dictionary<string, CodexNode> nodes, CodexNode groupNode, List<CodexEntry> entries)
    {
        if (!HasSpecies(entries))
        {
            foreach (var entry in entries)
            {
                Child(nodes, groupNode, entry.Key, entry.Name, entry);
            }

            return;
        }

        foreach (var entry in entries)
        {
            var species = CodexSpecies.Of(entry.Name)!;
            var speciesNode = Child(nodes, groupNode, groupNode.Key + "." + CodexKinds.Slug(species), species, null);
            Child(nodes, speciesNode, entry.Key, entry.Name, entry);
        }
    }

    /// <summary>The shared first word of a group's names when they name species, otherwise Canonn's group.</summary>
    private static string GroupName(string group, List<CodexEntry> entries)
    {
        if (!HasSpecies(entries))
        {
            return group;
        }

        var words = entries.Select(entry => FirstWord(entry.Name)).Distinct(StringComparer.Ordinal).ToList();

        return words.Count == 1 && words[0].Length > 0 ? words[0] : group;
    }

    private static bool HasSpecies(List<CodexEntry> entries) =>
        entries.Count > 0 && entries.All(entry => !string.Equals(CodexSpecies.Of(entry.Name), entry.Name.Trim(), StringComparison.Ordinal));

    private static string FirstWord(string name)
    {
        var trimmed = name.Trim();
        var at = trimmed.IndexOf(' ', StringComparison.Ordinal);

        return at > 0 ? trimmed[..at] : trimmed;
    }

    /// <summary>The node of the key under the parent, made if there is none.</summary>
    private static CodexNode Child(Dictionary<string, CodexNode> nodes, CodexNode parent, string key, string name, CodexEntry? entry)
    {
        if (nodes.TryGetValue(key, out var existing) && existing.Parent == parent)
        {
            return existing;
        }

        var node = new CodexNode(key, name, parent, entry);
        nodes[key] = node;
        parent.ChildList.Add(node);

        return node;
    }
}
