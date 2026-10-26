using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Signal search leaves out sightings last seen before the cutoff, from both sources (#1770).</summary>
public class AReportAgeCutoffDropsOlderSignalSightingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly StarPosition Origin = new(0, 0, 0);

    private static SignalSighting Sighting(string system, double x, TimeSpan age) =>
        new(system.Length, system, new StarPosition(x, 0, 0), "USS", "Nav Beacon", SignalKind.NavBeacon, Now - age);

    private static IEnumerable<string> Systems(SignalSearchResult result) => result.Signals.Select(finding => finding.System);

    [Fact]
    public void OnlySightingsOnOrAfterTheCutoffAreKept()
    {
        var own = new[]
        {
            Sighting("Old", 1, TimeSpan.FromHours(30)),
            Sighting("Edge", 2, TimeSpan.FromHours(24)),
            Sighting("Fresh", 3, TimeSpan.FromHours(2)),
        };

        var result = SignalSearch.Find(Origin, null, _ => true, 100, 25, own, null, Now - TimeSpan.FromHours(24));

        Assert.Equal(["Edge", "Fresh"], Systems(result));
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public void WithNoCutoffEverySightingIsKept()
    {
        var own = new[] { Sighting("Old", 1, TimeSpan.FromHours(9000)), Sighting("Fresh", 3, TimeSpan.FromHours(2)) };

        var result = SignalSearch.Find(Origin, null, _ => true, 100, 25, own, null);

        Assert.Equal(["Old", "Fresh"], Systems(result));
    }
}
