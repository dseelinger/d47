using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>A <c>ships.json</c> that carries the removed <c>settled</c> member loads with every build intact.</summary>
public class AShipsFileWithAStoredNoStillLoadsTests
{
    [Fact]
    public void ABuildWithASettledMemberLoadsWithItsSlots()
    {
        var files = new MemoryFileSystem();

        files.WriteText(
            "ships.json",
            """
            {"ships":[{"id":"ship-1","hull":"python","shipId":41,"settled":"a|b→c",
             "slots":[{"slot":"MainEngines","blueprint":"Dirty Drives","grade":5}]}]}
            """);

        var store = new ShipBuildStore("ships.json", files, NullLogger<ShipBuildStore>.Instance);

        Assert.True(store.Poll());

        var build = Assert.Single(store.Builds);

        Assert.Equal("python", build.Hull);
        Assert.Equal(5, Assert.Single(build.Slots).Grade);
    }
}
