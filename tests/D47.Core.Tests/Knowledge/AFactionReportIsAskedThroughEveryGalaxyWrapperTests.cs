using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AFactionReportIsAskedThroughEveryGalaxyWrapperTests
{
    private sealed class Reporting : StubGalaxyService, IGalaxyService
    {
        public List<string> Asked { get; } = [];

        public List<(bool Refresh, IProgress<FactionProgress>? Progress)> Passed { get; } = [];

        public override Task<FactionReport> FactionAsync(string faction, CancellationToken cancellationToken, bool refresh = false, IProgress<FactionProgress>? progress = null)
        {
            Asked.Add(faction);
            Passed.Add((refresh, progress));

            return Task.FromResult(new FactionReport(
                faction,
                "Anarchy",
                "Independent",
                [new FactionSystem { Name = "Nowhere Known", Controls = true }],
                []));
        }
    }

    [Fact]
    public async Task TheReportComesFromTheServiceInsideAndItsSystemsAreRemembered()
    {
        var inner = new Reporting();
        var names = new GalaxySearchNames(new StationPowerFilter(inner, () => null));

        var report = await names.FactionAsync("Some Faction", TestContext.Current.CancellationToken);

        Assert.Equal("Some Faction", report.Name);
        Assert.Equal(["Some Faction"], inner.Asked);
        Assert.Contains("Nowhere Known", names.Names);
    }

    [Fact]
    public async Task RefreshAndProgressReachTheServiceThroughBothWrappers()
    {
        var inner = new Reporting();
        var names = new GalaxySearchNames(new StationPowerFilter(inner, () => null));
        var progress = new Progress<FactionProgress>();

        await names.FactionAsync("Some Faction", TestContext.Current.CancellationToken, refresh: true, progress: progress);

        Assert.Equal((true, progress), (inner.Passed[0].Refresh, inner.Passed[0].Progress));
    }
}
