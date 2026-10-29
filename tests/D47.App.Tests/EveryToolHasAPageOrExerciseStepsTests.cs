using D47.App.Coverage;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every registered tool has a page on the panel or steps for exercising it by hand.</summary>
[Trait("Category", "Gate")]
public sealed class EveryToolHasAPageOrExerciseStepsTests
{
    private static readonly HashSet<string> WithNeither = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void EveryToolHasAPageOrExerciseStepsOrIsOnTheList()
    {
        var (_, _, _, registry, _) = TestSurface.CreateFull();
        var tools = registry.All.SelectMany(capability => capability.Descriptor.Tools).ToList();
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        bool Covered(ToolDefinition tool) => ToolPlaces.For(tool.Name) is not null || !string.IsNullOrWhiteSpace(tool.Exercise);

        var missing = tools.Where(tool => !Covered(tool) && !WithNeither.Contains(tool.Name)).Select(tool => tool.Name).ToList();
        var stale = tools.Where(tool => Covered(tool) && WithNeither.Contains(tool.Name)).Select(tool => tool.Name).ToList();
        var unknown = WithNeither.Where(name => !names.Contains(name)).ToList();

        Assert.True(missing.Count == 0, "No page and no Exercise, and not on the list: " + string.Join(", ", missing));
        Assert.True(stale.Count == 0, "Now covered, remove from the list: " + string.Join(", ", stale));
        Assert.True(unknown.Count == 0, "On the list but not registered: " + string.Join(", ", unknown));
    }
}
