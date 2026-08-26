using D47.Core.Capabilities;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>
/// The routing table, checked without a model
/// (<a href="https://github.com/dseelinger/d47/issues/57">#57</a>).
/// <para>
/// <b>The measurement needs a model; the table does not.</b> Every row names two real tools and
/// asks a real question, and all of that is assertable offline — so a mistyped tool name is caught
/// on every push rather than on the next paid run, which might be weeks away.
/// </para>
/// <para>
/// The paid half is <c>LiveScenarioTests.TheRoutingTableAgainstAConfiguredEndpoint</c>, behind
/// <c>D47_SCENARIOS_LIVE=1</c> with the rest of the live tier. It never gates a release: a suite
/// whose result depends on a third party's model, a network and a non-deterministic sampler cannot
/// sit in the path that publishes a tag.
/// </para>
/// </summary>
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

    /// <summary>
    /// Every tool a row names is one d47 actually registers. A typo here would report as a routing
    /// failure on a paid run — the model blamed for not calling something that does not exist.
    /// </summary>
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

    /// <summary>
    /// <b>Every row says both halves.</b> A row asserting only that the right tool ran passes on a
    /// model that calls both, which is a different behaviour with a different bill and the same
    /// green tick — and "both" is exactly the failure mode an overlapping pair produces.
    /// </summary>
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

            // The safety kind is never how a routing row says it. See AssertionKind.ToolNotChosen.
            Assert.DoesNotContain(row.Assertions, a => a.Kind == AssertionKind.ToolDidNotRun);

            Assert.DoesNotContain(
                didNot,
                other => string.Equals(other.Target, ran[0].Target, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Routing is a quality property, so a rate is a real answer — but a rate needs a sample size
    /// or it is decoration, and an always-tolerance on a non-deterministic sampler is a suite that
    /// cries wolf until nobody reads it.
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

    /// <summary>
    /// No two rows ask the same thing. A duplicated utterance is a row that looks like coverage and
    /// measures the same switch twice.
    /// </summary>
    [Fact]
    public void NoTwoRowsAskTheSameQuestion()
    {
        var rows = Corpus.Routing();

        Assert.Equal(
            rows.Count,
            rows.Select(row => row.Utterance.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Equal(rows.Count, rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The relationship #58 and this issue were proposed with: a pair given a cross-check at its
    /// seam is a pair worth asserting the routing of. Both directions of the material family are
    /// covered, which is the family the original defect came from.
    /// </summary>
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
