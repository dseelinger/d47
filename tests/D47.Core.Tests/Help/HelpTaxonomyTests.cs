using D47.Core.Capabilities;
using D47.Core.Help;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>The spoken map of six categories (#166), checked against the live registry.</summary>
public class HelpTaxonomyTests
{
    [Fact]
    public void EveryRegisteredCapabilityIsPlacedOrDeliberatelyUnspokenTests()
    {
        var ids = RegisteredIds();
        var leafIds = HelpTaxonomy.Leaves().Select(node => node.CapabilityId!).ToArray();

        var duplicates = leafIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.True(duplicates.Length == 0, $"Named more than once: {string.Join(", ", duplicates)}");

        var overlap = leafIds.Intersect(HelpTaxonomy.Unspoken, StringComparer.Ordinal).ToArray();
        Assert.True(overlap.Length == 0, $"Both a leaf and unspoken: {string.Join(", ", overlap)}");

        var placed = leafIds.Concat(HelpTaxonomy.Unspoken).ToHashSet(StringComparer.Ordinal);
        var missing = ids.Where(id => !placed.Contains(id)).ToArray();
        Assert.True(missing.Length == 0, $"Registered but neither a leaf nor unspoken: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryNamedCapabilityIsActuallyRegisteredTests()
    {
        var ids = RegisteredIds().ToHashSet(StringComparer.Ordinal);

        var namedButMissing = HelpTaxonomy.Leaves()
            .Select(node => node.CapabilityId!)
            .Concat(HelpTaxonomy.Unspoken)
            .Where(id => !ids.Contains(id))
            .ToArray();

        Assert.True(
            namedButMissing.Length == 0,
            $"Named but not registered: {string.Join(", ", namedButMissing)}");
    }

    [Fact]
    public void NoLevelSaysMoreThanSixThingsTests()
    {
        AssertBounded(HelpTaxonomy.Top);

        static void AssertBounded(IReadOnlyList<HelpNode> level)
        {
            Assert.True(
                level.Count <= HelpTaxonomy.MostAtOnce,
                $"A level holds {level.Count} things: {string.Join(", ", level.Select(n => n.Name))}");

            foreach (var node in level.Where(n => n.Children.Count > 0))
            {
                AssertBounded(node.Children);
            }
        }
    }

    [Fact]
    public void EveryRegisteredToolReachesATopLevelCategoryOrIsUnspokenTests()
    {
        var registry = TestSurface.For(new TempInstall()).Registry;

        var categoryOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var category in HelpTaxonomy.Top)
        {
            foreach (var leaf in HelpTaxonomy.Leaves([category]))
            {
                categoryOf[leaf.CapabilityId!] = category.Name;
            }
        }

        var orphaned = registry.All
            .SelectMany(capability => capability.Descriptor.Tools.Select(tool => (capability.Descriptor.Id, tool.Name)))
            .Where(entry => !categoryOf.ContainsKey(entry.Id) && !HelpTaxonomy.Unspoken.Contains(entry.Id, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            orphaned.Length == 0,
            $"Tools with no category: {string.Join(", ", orphaned.Select(entry => $"{entry.Name} ({entry.Id})"))}");
    }

    [Fact]
    public void EveryLeafSpeaksOneSentenceWithNoUnderscoreTests()
    {
        var broken = new List<string>();

        foreach (var leaf in HelpTaxonomy.Leaves())
        {
            if (!leaf.Sentence.EndsWith('.') || leaf.Sentence.Contains('_')
                || leaf.Sentence.Count(c => c is '.' or '!' or '?') != 1)
            {
                broken.Add($"{leaf.Name}: {leaf.Sentence}");
            }
        }

        Assert.True(broken.Count == 0, string.Join(Environment.NewLine, broken));
    }

    private static IReadOnlyList<string> RegisteredIds() =>
        TestSurface.For(new TempInstall()).Registry.All.Select(c => c.Descriptor.Id).ToArray();
}
