using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Tests;
using Xunit;

namespace D47.Core.Tests.Coverage;

/// <summary>
/// Coverage recording is a workbench aid for whoever builds d47, not a feature. A Commander who
/// downloads a release should find no trace of it — no row, no tool, nothing to ask about.
/// </summary>
public class CoverageIsOutOfTheWayTests
{
    [Fact]
    public void ANormalRunHasNoCoverageRowAnywhereOnTheSurface()
    {
        using var install = new TempInstall();

        var rows = TestSurface.For(install).Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Select(row => row.Key)
            .ToList();

        Assert.DoesNotContain("diagnostics.coverage", rows);
        Assert.DoesNotContain(rows, key => key.Contains("coverage", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// And when it is asked for, it is one row on the diagnostics card — decided before
    /// registration, so descriptors are still registered once and never mutated either way.
    /// </summary>
    [Fact]
    public void AskingForItAddsExactlyOneRow()
    {
        using var install = new TempInstall();

        var settings = TestSurface.For(install).Settings;

        var without = DiagnosticsCapability
            .Create(install.Paths, new FakeVerbosityControl(), settings, "1.0.0-test")
            .Settings;

        var with = DiagnosticsCapability
            .Create(install.Paths, new FakeVerbosityControl(), settings, "1.0.0-test", () => "3 of 4 exercised.")
            .Settings;

        Assert.Equal(without.Count + 1, with.Count);

        var added = with.Single(row => row.Key == "diagnostics.coverage");

        Assert.Equal(SettingKind.Info, added.Kind);

        // Info rows are read-only and computed at render time, so the row reports rather than
        // configures — there is nothing here for the model or the router to set.
        Assert.Null(added.Binding!.Write);
        Assert.Empty(added.Commands);
    }

    /// <summary>
    /// It never becomes something the model can call. The registry is what the prompt is built
    /// from, so a tool here would ship the aid to every Commander's language model.
    /// </summary>
    [Fact]
    public void ItIsNeverATool()
    {
        using var install = new TempInstall();

        Assert.DoesNotContain(
            TestSurface.For(install).Registry.ToolNames,
            name => name.Contains("coverage", StringComparison.OrdinalIgnoreCase));
    }
}
