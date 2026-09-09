using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary> A hull's slot layout is reachable even where the table has no hull row for it. </summary>
public class AHullWithNoShipRowKeepsItsSlotsTests
{
    /// <summary>The Mandalay.</summary>
    private const string NoShipRow = "explorer_nx";

    [Fact]
    public void TheGapThisGuardsIsRealRatherThanHypothetical()
    {
        // If this ever starts failing, the table has caught up and the fallback below is no longer
        // needed for this hull — which means the table improved, not that the test broke.
        Assert.Null(EliteSpecifications.Ship(NoShipRow));
    }

    [Fact]
    public void TheLayoutIsFoundAnyway()
    {
        var slots = EliteSpecifications.Slots(NoShipRow);

        Assert.NotEmpty(slots);
        Assert.Equal(35, slots.Count);
    }

    [Fact]
    public void AndSoIsOneSlotOfIt()
    {
        var slot = EliteSpecifications.Slot(NoShipRow, "TinyHardpoint1");

        Assert.NotNull(slot);
        Assert.Equal(ShipSlotKind.Utility, slot.Kind);

        // The name the Commander reads: the panel showed "TinyHardpoint1" because it had no ShipSlot to ask.
        Assert.Equal("Utility Mount 1", slot.Describe());
    }

    [Fact]
    public void AHullWithBothStillResolvesEitherSpelling()
    {
        // The behaviour remediation 16 item 5 added, unchanged: StoredShips carries the localised name, so
        // both spellings have to find the same layout.
        Assert.NotEmpty(EliteSpecifications.Slots("anaconda"));
        Assert.Equal(
            EliteSpecifications.Slots("anaconda").Count,
            EliteSpecifications.Slots("Anaconda").Count);
    }

    [Fact]
    public void AHullNothingKnowsAtAllStillAnswersEmpty()
    {
        Assert.Empty(EliteSpecifications.Slots("thargoid_interceptor"));
        Assert.Empty(EliteSpecifications.Slots(""));
        Assert.Empty(EliteSpecifications.Slots(null));
    }
}
