using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// No two sections of the settings panel claim the same position
/// (<a href="https://github.com/dseelinger/d47/issues/83">#83</a>).
/// <para>
/// <b>A tie is not a tidiness problem, it is an undecided question rendered as though it were
/// decided.</b> <c>SettingsService</c> sorts with LINQ's <c>OrderBy</c>, which is stable, so two
/// capabilities at the same <c>Order</c> fall back to their positions in
/// <c>BuiltinCapabilities.All</c> — a list whose order exists for an entirely different reason
/// (<c>DocumentationGateTests</c> derives each docs page's <c>nav_order</c> from the index there).
/// So the left nav ends up arranged by a list nobody was arranging the nav with, and the next
/// person to touch that list moves the nav without knowing they did.
/// </para>
/// <para>
/// #83 fixed one such tie, between About and Privacy. Three more survived it — at 54, 55 and 58 —
/// and were resolved on 2026-08-27 by giving each its own number <b>in the sequence it already
/// rendered in</b>, so nothing a Commander sees moved. This is the gate that stops a fourth.
/// </para>
/// <para>
/// <b>The forgotten-order case needs nothing here.</b> <c>CapabilityDisplay.Order</c> defaults to
/// 100, which is past About's 99, so a capability that declares none lands below the page's
/// deliberate ending and trips
/// <see cref="AboutIsTheBottomOfThePageTests.NothingElseClaimsAboutsPositionOrBelowIt"/> instead.
/// Two tests, two distinct faults, and neither needs the default changed to catch its own.
/// </para>
/// </summary>
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
    /// And the sections that do not draw are left alone, which is why this counts only the ones
    /// that do. A hidden capability sharing a number with a visible one decides nothing, and
    /// widening the gate to cover them would be asking for numbers to be chosen for rows that are
    /// never rendered.
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
    /// The action cards' primary card is drawn and carries its rows, and neither fact depends on
    /// its position in the nav (#83).
    /// <para>
    /// <b>This is the regression the renumbering could have caused and nothing would have
    /// reported.</b> <c>ActionCapabilities.Create</c> decided both of those with <c>order == 50</c>
    /// — so the display order was quietly load-bearing for whether the card exists at all and
    /// whether the keyboard-actions row and the ship commands appear on it. Moving the card one
    /// place to break a tie with Engineers would have switched both off, silently, and the only
    /// symptom would have been a settings card that stopped being there.
    /// </para>
    /// <para>
    /// Asserted against what the card <em>is</em> rather than what number it holds, so the next
    /// person to move it in the nav is free to.
    /// </para>
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

        // The other three action cards stay off the panel, which is the other half of what the
        // sentinel meant and is just as easy to invert by accident. Named rather than taken from
        // the group, which holds several unrelated capabilities that draw quite legitimately.
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
