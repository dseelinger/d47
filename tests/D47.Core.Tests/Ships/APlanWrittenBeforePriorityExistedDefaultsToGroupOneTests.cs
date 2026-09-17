using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A <c>ships.json</c> written before #253 named no priority group at all, and has to load as group 1
/// rather than as none.
/// </summary>
public class APlanWrittenBeforePriorityExistedDefaultsToGroupOneTests
{
    [Fact]
    public void ASlotWithNoStoredPriorityLoadsAtGroupOne()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d47-builds-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                path,
                """
                {"ships":[{"id":"ship-1","hull":"python","shipId":41,
                 "slots":[{"slot":"MainEngines","blueprint":"Dirty Drives","grade":5}]}]}
                """);

            var store = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

            Assert.True(store.Poll());

            var plan = Assert.Single(Assert.Single(store.Builds).Slots);

            Assert.Equal(1, plan.Priority);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void APlannedPriorityRoundTripsThroughASave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d47-builds-{Guid.NewGuid():N}.json");

        try
        {
            var writing = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

            writing.Save([new ShipBuild("F1", "ship-1", "python", 41, "Ox",
                [new SlotPlan("MainEngines", "Dirty Drives", 5) { Priority = 3 }])]);

            var reading = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

            Assert.True(reading.Poll());

            var plan = Assert.Single(Assert.Single(reading.Builds).Slots);

            Assert.Equal(3, plan.Priority);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
