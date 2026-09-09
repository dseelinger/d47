using D47.Core.Capabilities;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>The routing table, checked without a model.</summary>
public class RoutingTableTests
{
    /// <summary>
    /// Every tool d47 registers, from the real builtin registry the runner itself builds — so this
    /// cannot drift from what a live run would actually advertise.
    /// </summary>
    private static IReadOnlySet<string> ToolNames()
    {
        using var world = new ScenarioWorld();

        return world.Registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void TheTableIsNotEmpty()
    {
        Assert.NotEmpty(Corpus.Routing());
    }

    [Fact]
    public void EveryToolNamedIsAToolThatExists()
    {
        var known = ToolNames();

        var unknown = Corpus.Routing()
            .SelectMany(row => row.Assertions)
            .Where(assertion => assertion.Target is { Length: > 0 })
            .Select(assertion => assertion.Target!)
            .Where(target => !known.Contains(target))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unknown.Count == 0,
            $"The routing table names tools that are not registered: {string.Join(", ", unknown)}.");
    }

    [Fact]
    public void EveryRowNamesTheToolThatMustRunAndTheOneThatMustNot()
    {
        foreach (var row in Corpus.Routing())
        {
            var ran = row.Assertions.Where(a => a.Kind == AssertionKind.ToolRan).ToList();
            var didNot = row.Assertions.Where(a => a.Kind == AssertionKind.ToolNotChosen).ToList();

            Assert.True(ran.Count == 1, $"{row.Id}: a routing row names exactly one tool that must run.");
            Assert.True(
                didNot.Count >= 1,
                $"{row.Id}: a routing row must also name the neighbour that must not be chosen.");

            // The safety kind is never how a routing row says it.
            Assert.DoesNotContain(row.Assertions, a => a.Kind == AssertionKind.ToolDidNotRun);

            Assert.DoesNotContain(
                didNot,
                other => string.Equals(other.Target, ran[0].Target, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Routing is a quality property, so a rate is a real answer — but a rate needs a sample size or it
    /// is decoration, and an always-tolerance on a non-deterministic sampler is a suite that cries wolf
    /// until nobody reads it.
    /// </summary>
    [Fact]
    public void EveryRoutingAssertionCarriesATolerance()
    {
        foreach (var row in Corpus.Routing())
        {
            Assert.All(
                row.Assertions,
                assertion => Assert.False(
                    assertion.Tolerance.IsAlways,
                    $"{row.Id}: routing is measured, not demanded. Give it a rate out of a sample."));
        }
    }

    [Fact]
    public void NoTwoRowsAskTheSameQuestion()
    {
        var rows = Corpus.Routing();

        Assert.Equal(
            rows.Count,
            rows.Select(row => row.Utterance.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Equal(rows.Count, rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A pair given a cross-check at its seam is a pair worth asserting the routing of.</summary>
    [Fact]
    public void TheMaterialFamilyIsCoveredInBothDirections()
    {
        var targets = Corpus.Routing()
            .SelectMany(row => row.Assertions)
            .Where(assertion => assertion.Kind == AssertionKind.ToolRan)
            .Select(assertion => assertion.Target)
            .ToList();

        Assert.Contains(D47.Core.Knowledge.MaterialSeam.MaterialTool, targets);
        Assert.Contains(D47.Core.Knowledge.MaterialSeam.MarketTool, targets);
        Assert.Contains(D47.Core.Knowledge.MaterialSeam.MicroResourceTool, targets);
    }
}
