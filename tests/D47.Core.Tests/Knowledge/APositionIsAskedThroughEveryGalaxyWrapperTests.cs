using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The app reaches the galaxy service through <see cref="GalaxySearchNames"/> and <see cref="StationPowerFilter"/>; a
/// position asked of the outer one reaches the service rather than the interface's empty answer (#1409).
/// </summary>
public class APositionIsAskedThroughEveryGalaxyWrapperTests
{
    private sealed class Placing : IGalaxyService
    {
        public List<string> Asked { get; } = [];

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken)
        {
            Asked.Add(system);

            return Task.FromResult<StarPosition?>(new StarPosition(1, 2, 3));
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ThePositionComesFromTheServiceInside()
    {
        var inner = new Placing();
        IGalaxyService outer = new GalaxySearchNames(new StationPowerFilter(inner, () => null));

        var at = await outer.PositionAsync("Nowhere Known", TestContext.Current.CancellationToken);

        Assert.Equal(new StarPosition(1, 2, 3), at);
        Assert.Equal(["Nowhere Known"], inner.Asked);
    }
}
