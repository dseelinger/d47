using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// One quantity, one spelling, across every tool that takes it
/// (<a href="https://github.com/dseelinger/d47/issues/178">#178</a>).
/// <para>
/// The route planner said <c>max_price_age</c> and the commodity search, following #157's spec,
/// said <c>max_price_age_hours</c> — the same bound on the same kind of data, one seam apart. A
/// model switching between the two tools has to remember which spelling belongs where, and that
/// is the sort of thing it gets right in the quiet and wrong under pressure.
/// </para>
/// <para>
/// The name carrying the unit won, because it is the one that reads correctly in the sentence a
/// refused widening has to say: <i>"max_price_age_hours stops at 8,760 hours"</i>.
/// </para>
/// </summary>
public class ThePriceAgeKnobIsSpelledOnceTests
{
    private static IReadOnlyList<ToolDefinition> Tools(TempInstall install)
    {
        var settings = TestSurface.For(install).Settings;

        return
        [
            .. GalaxyCapability.Create(new Nothing(), () => "Eurybia", settings, new NoPlan()).Tools,
            .. RouteCapability.Create(null, new NoPlan(), () => null, settings).Tools,
        ];
    }

    /// <summary>
    /// The assertion the rename exists for, and it is written over every tool rather than over the
    /// two that happen to have the argument today — a third tool spelling it a third way would be
    /// the same defect again.
    /// </summary>
    [Fact]
    public void NoToolSpellsThePriceAgeWithoutItsUnit()
    {
        using var install = new TempInstall();

        var wrong = Tools(install)
            .SelectMany(tool => tool.Parameters.Select(parameter => (Tool: tool.Name, Knob: parameter.Name)))
            .Where(p => p.Knob.Contains("price_age", StringComparison.Ordinal)
                        && p.Knob != "max_price_age_hours")
            .Select(p => $"{p.Knob} on {p.Tool}")
            .ToArray();

        Assert.True(
            wrong.Length == 0,
            $"These spell the price-age bound some other way: {string.Join(", ", wrong)}.");
    }

    /// <summary>Both tools that take it, taking it under the one name.</summary>
    [Fact]
    public void BothPlannersTakeTheSameName()
    {
        using var install = new TempInstall();

        var taking = Tools(install)
            .Where(tool => tool.Parameters.Any(p => p.Name == "max_price_age_hours"))
            .Select(tool => tool.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["find_nearest_station", "plot_trade_route"], taking);
    }

    /// <summary>Plans nothing; here only so the descriptors have something composed to build from.</summary>
    private sealed class NoPlan : ITradePlanService
    {
        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CommodityAnswer([], 0, 0, true));

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);
    }

    /// <summary>The same, for the galaxy half.</summary>
    private sealed class Nothing : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Eurybia", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("Eurybia", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Eurybia", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Eurybia", 0, []));
    }
}
