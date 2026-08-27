using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// About is the last section in the panel, and it is last because it says so
/// (<a href="https://github.com/dseelinger/d47/issues/83">#83</a>).
/// <para>
/// <b>Reported as "About should be the last left nav item. Bottom of the page, not second to the
/// bottom."</b> It shared <c>Order = 95</c> with Privacy, and <c>SettingsService</c> sorts with
/// LINQ's <c>OrderBy</c>, which is <em>stable</em> — so the tie fell through to registration order
/// in <c>BuiltinCapabilities.All</c>. The bottom of the nav was decided by which of two lines came
/// first in a list, and nothing on either capability said so.
/// </para>
/// <para>
/// <b>It could not be fixed by moving it in that list.</b> About is pinned to the end there by
/// <c>DocumentationGateTests</c>, which derives every page's <c>nav_order</c> from its index — so
/// its nav position has to be settled on <c>Display.Order</c>, which is what this asserts.
/// </para>
/// <para>
/// <b>Two different reasons for "last", which is what made this easy to get wrong twice.</b> Both
/// are asserted here so neither can be quietly satisfied by the other.
/// </para>
/// </summary>
public class AboutIsTheBottomOfThePageTests
{
    private static IReadOnlyList<CapabilityDescriptor> OnThePanel() =>
        [.. Registered().Where(c => c.Display.ShowOnPanel)];

    /// <summary>
    /// The page's own order: what the panel would draw, sorted the way
    /// <c>SettingsService</c> sorts it.
    /// </summary>
    [Fact]
    public void AboutSortsLastOfEverythingOnThePanel()
    {
        var drawn = OnThePanel().OrderBy(c => c.Display.Order).ToList();

        Assert.NotEmpty(drawn);
        Assert.Equal("About", drawn[^1].Display.PanelTitle);
    }

    /// <summary>
    /// And strictly below Privacy rather than tied with it, so the answer does not depend on the
    /// sort being stable or on a list's order. A tie here is the defect itself.
    /// </summary>
    [Fact]
    public void AndStrictlyBelowPrivacyRatherThanTiedWithIt()
    {
        var about = OnThePanel().Single(c => c.Display.PanelTitle == "About");
        var privacy = OnThePanel().Single(c => c.Display.PanelTitle == "Privacy and egress");

        Assert.True(
            about.Display.Order > privacy.Display.Order,
            $"About is {about.Display.Order} and Privacy is {privacy.Display.Order}. Equal orders "
            + "leave the bottom of the nav to registration order, which is what #83 was.");
    }

    /// <summary>
    /// Nothing else sits at or below About, which is the property "last" actually means — a
    /// capability added later with a high order would take the bottom of the page without anyone
    /// deciding it should.
    /// </summary>
    [Fact]
    public void NothingElseClaimsAboutsPositionOrBelowIt()
    {
        var about = OnThePanel().Single(c => c.Display.PanelTitle == "About");

        var atOrBelow = OnThePanel()
            .Where(c => c.Display.PanelTitle != "About")
            .Where(c => c.Display.Order >= about.Display.Order)
            .Select(c => $"{c.Display.PanelTitle} ({c.Display.Order})")
            .ToList();

        Assert.True(
            atOrBelow.Count == 0,
            $"these sit at or past About's {about.Display.Order}: {string.Join(", ", atOrBelow)}. "
            + "About is the bottom of the page; something below it is a nav order nobody chose.");
    }

    /// <summary>
    /// The other half of "last", and it is a different requirement about a different list:
    /// <c>DocumentationGateTests</c> derives each page's <c>nav_order</c> from the index here, so
    /// anything registered after About renumbers every documentation page.
    /// </summary>
    [Fact]
    public void AndAboutIsStillLastInTheRegistrationListForTheDocsGate()
    {
        var all = Registered();

        Assert.Equal("About", all[^1].Display.PanelTitle);
    }

    /// <summary>
    /// The built-in set as the app registers it, so this sees the same list and the same order
    /// the panel does rather than a hand-written copy of either.
    /// </summary>
    private static IReadOnlyList<CapabilityDescriptor> Registered()
    {
        using var install = new TempInstall();

        return [.. TestSurface.For(install).Registry.All.Select(registered => registered.Descriptor)];
    }
}
