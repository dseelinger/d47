using System.Reflection;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public partial class DocumentationGateTests
{
    [Trait("Category", "Gate")]
    [Fact]
    public void EveryToolRunStepNamesAToolTheRegistryHas()
    {
        var registered = Registry().ToolNames.ToHashSet(StringComparer.Ordinal);
        var tools = StepEventCatalogue.Default.ValuesOf("ToolRun", "Tool");

        Assert.NotEmpty(tools);

        var unknown = tools.Where(tool => !registered.Contains(tool)).ToList();

        Assert.True(
            unknown.Count == 0,
            $"StepFields.tsv has ToolRun values the capability registry does not have: {string.Join(", ", unknown)}. Rerun tools/gen-step-events.py.");
    }
}
