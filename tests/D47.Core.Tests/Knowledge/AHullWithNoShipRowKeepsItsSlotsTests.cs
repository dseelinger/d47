using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A hull's slot layout is reachable even where the table has no hull row for it (reported
/// 2026-08-20).
/// <para>
/// <b>The two sections of the table can disagree, and did.</b> The layout is EDSY's and the hull
/// figures are FDevIDs' joined to coriolis, so Frontier shipping a ship ahead of the id sources
/// leaves it with a full <c>[slots]</c> block and no <c>[ships]</c> row — which is the state four
/// hulls are in today, and which the corpus soak already reported under "raw symbol read out".
/// </para>
/// <para>
/// <see cref="EliteSpecifications.Slots"/> routed through <see cref="EliteSpecifications.Ship"/>
/// to resolve a display name to a symbol, so a missing hull row hid a layout sitting in the same
/// file. On the Loadout tab that surfaced as the no-layout fallback: raw journal slot names
/// (<c>TinyHardpoint1</c> rather than "Utility Mount 1"), a typed-out blueprint instead of the
/// chooser, and no empty slots listed at all.
/// </para>
/// </summary>
public class AHullWithNoShipRowKeepsItsSlotsTests
{
    /// <summary>The Mandalay. Its 35 slot rows are in the table; its hull row is not.</summary>
    private const string NoShipRow = "explorer_nx";

    [Fact]
    public void TheGapThisGuardsIsRealRatherThanHypothetical()
    {
        // If this ever starts failing, the table has caught up and the fallback below is no longer
        // load-bearing for this hull — which is a good day, not a broken test. Pick another from
        // the soak's list, or retire the guard.
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

        // The name the Commander reads, which is the half that was reported: the panel showed
        // "TinyHardpoint1" because it had no ShipSlot to ask.
        Assert.Equal("Utility Mount 1", slot.Describe());
    }

    [Fact]
    public void AHullWithBothStillResolvesEitherSpelling()
    {
        // The behaviour remediation 16 item 5 added, unchanged: StoredShips carries the localised
        // name, so both spellings have to find the same layout.
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
