using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>On the embedded tables every entry is reachable from the root of <see cref="CodexTree"/> exactly once, and every key is unique.</summary>
public class TheShippedCodexTreeReachesEveryEntryOnceTests
{
    private static List<CodexNode> Walk(CodexNode root)
    {
        var nodes = new List<CodexNode>();
        var pending = new Stack<CodexNode>([root]);

        while (pending.TryPop(out var node))
        {
            nodes.Add(node);

            foreach (var child in node.Children)
            {
                Assert.Same(node, child.Parent);
                pending.Push(child);
            }
        }

        return nodes;
    }

    [Fact]
    public void EveryEntryIsReachedExactlyOnce()
    {
        var reached = Walk(CodexCatalogue.Tree.Root).Where(node => node.Entry is not null).Select(node => node.Entry!.Key).ToList();

        Assert.Equal(reached.Count, reached.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            CodexCatalogue.Table.All.Select(entry => entry.Key).Order(StringComparer.Ordinal),
            reached.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryKeyIsUniqueAndEveryOtherNodeKeyFollowsTheKindGrammar()
    {
        var nodes = Walk(CodexCatalogue.Tree.Root);
        var keys = nodes.Select(node => node.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(nodes.Where(node => node.Entry is null), node => Assert.True(CodexKeys.IsKind(node.Key), node.Key));
        Assert.All(nodes, node => Assert.Same(node, CodexCatalogue.Tree.Node(node.Key)));
        Assert.All(nodes, node => Assert.False(string.IsNullOrWhiteSpace(node.ShortName), node.Key));
    }

    [Fact]
    public void EveryCanonnRowHasAGroupAndTheHeaderNamesIt()
    {
        var text = CodexCatalogue.Text(CodexCatalogue.ResourceName);
        var canonn = CodexTable.Parse(text).All;

        Assert.Contains("# Columns: EntryID, English name, category, subcategory, group", text, StringComparison.Ordinal);
        Assert.Contains("# Entries: 1060.", text, StringComparison.Ordinal);
        Assert.Equal(1060, canonn.Count);
        Assert.All(canonn, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Group), entry.Key));
    }

    [Fact]
    public void TheOrganicGroupsAreNamedForTheirGenus()
    {
        var tree = CodexCatalogue.Tree;

        Assert.Equal("Bacterium", tree.Node("organic_structures.bacterium")!.Name);
        Assert.Equal("Tussock", tree.Node("organic_structures.tussock")!.Name);
        Assert.Equal("Frutexa", tree.Node("organic_structures.frutexa")!.Name);
        Assert.Equal("Mollusc", tree.Node("organic_structures.mollusc")!.Name);
        Assert.Equal("organic_structures.bacterium", tree.Node("organic_structures.bacterium.bacterium_aurasus")!.Parent!.Key);
        Assert.Equal(6, tree.Node("guardian_objects")!.Children.Count(child => child.Entry is not null));
    }
}
