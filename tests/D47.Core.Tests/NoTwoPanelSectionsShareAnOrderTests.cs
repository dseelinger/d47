using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests;

/// <summary>No two sections of the settings panel claim the same position.</summary>
public class NoTwoPanelSectionsShareAnOrderTests
{
    [Fact]
    public void EveryPanelSectionHasItsOwnPosition()
    {
        var shared = OnThePanel()
            .GroupBy(capability => capability.Display.Order)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key}: {string.Join(" / ", group.Select(c => c.Display.PanelTitle))}")
            .ToList();

        Assert.True(
            shared.Count == 0,
            "these sections claim the same position, so which comes first is decided by "
            + "registration order in BuiltinCapabilities.All rather than by anybody — "
            + string.Join("; ", shared));
    }

    /// <summary>
    /// And the sections that do not draw are left alone, which is why this counts only the ones that
    /// do.
    /// </summary>
    [Fact]
    public void AndTheGateIsAboutWhatIsDrawnRatherThanWhatIsRegistered()
    {
        var drawn = OnThePanel();
        var all = Registered();

        Assert.NotEmpty(drawn);
        Assert.True(drawn.Count < all.Count, "every capability draws, so this gate is not narrowing anything");
    }

    /// <summary>
    /// The action cards' primary card is drawn and carries its rows, and neither fact depends on its
 /// position in the nav.
    /// </summary>
    [Fact]
    public void TheActionsCardIsDrawnAndKeepsItsRowsWhereverItSitsInTheNav()
    {
        var flight = Registered().SingleOrDefault(c => c.Id == "flight-controls");

        Assert.True(flight is not null, "the flight-controls capability is not registered at all");

        Assert.True(
            flight!.Display.ShowOnPanel,
            "the actions card is not drawn. Its visibility used to be `order == 50`, so a "
            + "renumbering switches it off without touching anything that looks like visibility.");

        Assert.True(
            flight.Settings.Count > 0,
            "the actions card has no rows. The keyboard-actions row and the ship commands used to "
            + "hang off `order == 50` too, so they leave with the same edit.");

        // The other three action cards stay off the panel, which is the other half of what the sentinel meant
        // and is just as easy to invert by accident.
        var siblings = Registered()
            .Where(c => c.Id is "ship-systems" or "panels" or "srv")
            .ToList();

        Assert.Equal(3, siblings.Count);
        Assert.All(siblings, c => Assert.False(c.Display.ShowOnPanel, $"{c.Id} should not draw a card"));

        // Their rows live on the primary card, so they carry none of their own.
        Assert.All(siblings, c => Assert.Empty(c.Settings));
    }

    private static IReadOnlyList<CapabilityDescriptor> OnThePanel() =>
        [.. Registered().Where(capability => capability.Display.ShowOnPanel)];

    /// <summary>
    /// The built-in set as the app registers it, so this sees the same list and the same order the
    /// panel does rather than a hand-written copy of either.
    /// </summary>
    private static IReadOnlyList<CapabilityDescriptor> Registered()
    {
        using var install = new TempInstall();

        return [.. TestSurface.For(install).Registry.All.Select(registered => registered.Descriptor)];
    }
}
