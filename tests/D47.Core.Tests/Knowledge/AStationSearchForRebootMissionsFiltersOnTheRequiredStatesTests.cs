using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>MissionStationQuery.States: the required states replace the seen-under ones (#2139).</summary>
public class AStationSearchForRebootMissionsFiltersOnTheRequiredStatesTests
{
    private static MissionStationQuery Parse(IReadOnlyCollection<MissionName> names)
    {
        Assert.True(MissionStationQuery.TryParse("Sol", names, null, out var query, out _));

        return query;
    }

    [Fact]
    public void ARebootQueryFiltersOnTheTenRequiredStates()
    {
        var names = MissionCatalogue.All.Where(name => name.Family == "Mission_OnFoot_RebootRestore").ToList();

        Assert.Equal(names[0].RequiredStates.Order(StringComparer.OrdinalIgnoreCase), Parse(names).States.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(10, Parse(names).States.Count);
    }

    [Fact]
    public void AMassacreQueryStillFiltersOnTheStatesItWasSeenUnder()
    {
        var names = MissionCatalogue.All.Where(name => name.Family == "Mission_Massacre").ToList();
        var expected = names.Select(name => name.State).Where(state => state.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected, Parse(names).States);
    }
}
