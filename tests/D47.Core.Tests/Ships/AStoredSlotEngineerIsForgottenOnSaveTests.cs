using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A ship slot plan names no engineer (#465). A <c>ships.json</c> that still carries one loads, and the
/// next save writes the slot without it.
/// </summary>
public class AStoredSlotEngineerIsForgottenOnSaveTests
{
    [Fact]
    public void ASlotWithAStoredEngineerLoadsAndLosesItOnSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d47-builds-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                path,
                """
                {"ships":[{"id":"ship-1","hull":"python","shipId":41,
                 "slots":[{"slot":"MainEngines","blueprint":"Dirty Drive Tuning","grade":5,
                           "engineer":"Felicity Farseer"}]}]}
                """);

            var store = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

            Assert.True(store.Poll());
            Assert.Empty(store.Problems);

            var build = Assert.Single(store.Builds);
            var plan = Assert.Single(build.Slots);

            Assert.Equal("Dirty Drive Tuning", plan.Blueprint);
            Assert.Equal("grade 5 Dirty Drive Tuning", plan.Describe());

            store.Save(store.Builds);

            Assert.DoesNotContain("engineer", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
