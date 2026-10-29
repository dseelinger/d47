using D47.Core.Capabilities;
using D47.Core.Coverage;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every tool and every settings row on the Features page has steps for exercising it by hand.</summary>
[Trait("Category", "Gate")]
public sealed class EveryFeaturesLineHasTestStepsTests
{
    [Fact]
    public void EveryFeaturesLineHasSteps()
    {
        var (_, _, _, registry, _) = TestSurface.CreateFull();
        var items = CoverageInventory.Of(registry);

        var tools = registry.All
            .SelectMany(capability => capability.Descriptor.Tools.Select(tool => (capability.Descriptor.Id, tool)))
            .ToDictionary(pair => (pair.Id, pair.tool.Name));
        var rows = registry.All
            .SelectMany(capability => capability.Descriptor.Settings.Select(row => (capability.Descriptor.Id, row)))
            .ToLookup(pair => (pair.Id, pair.row.Key));

        bool HasSteps(CoverageItem item) => item.Kind == CoverageKind.Tool
            ? !string.IsNullOrWhiteSpace(tools[(item.CapabilityId, item.Id)].tool.Exercise)
            : rows[(item.CapabilityId, item.Id)].Any(pair => !string.IsNullOrWhiteSpace(pair.row.Exercise));

        var toolsWithout = items.Where(item => item.Kind == CoverageKind.Tool && !HasSteps(item)).Select(item => item.Id).ToList();
        var settings = items.Where(item => item.Kind == CoverageKind.Setting).ToList();
        var missing = settings.Where(item => !HasSteps(item)).Select(item => item.Id).ToList();

        Assert.True(toolsWithout.Count == 0, "Tools with no Exercise: " + string.Join(", ", toolsWithout));
        Assert.True(missing.Count == 0, "Settings with no Exercise: " + string.Join(", ", missing));
    }
}
