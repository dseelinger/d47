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
    private sealed class Placing : StubGalaxyService, IGalaxyService
    {
        public List<string> Asked { get; } = [];

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken)
        {
            Asked.Add(system);

            return Task.FromResult<StarPosition?>(new StarPosition(1, 2, 3));
        }
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
