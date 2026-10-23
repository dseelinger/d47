using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class StatusRowsTests
{
    [Fact]
    public void NoIntroductionsNamesNoCoreAndHasNoSecondLine()
    {
        var text = StatusRows.Introductions(0);

        Assert.Equal("No core has introduced itself yet", text.Block);
        Assert.Null(text.Second);
    }

    [Theory]
    [InlineData(1, "1 has so far")]
    [InlineData(7, "7 have so far")]
    public void IntroductionsCountWhatHasIntroducedItself(int introduced, string second)
    {
        var text = StatusRows.Introductions(introduced);

        Assert.Equal("Every core introduces itself again", text.Block);
        Assert.Equal(second, text.Second);
    }

    [Theory]
    [InlineData(0, "None yet. A core needs a name and a paragraph saying what it is like.")]
    [InlineData(1, "")]
    [InlineData(3, "")]
    [InlineData(12, "")]
    public void OwnCoresStateOnlyThatThereAreNone(int count, string expected)
    {
        string[] first = ["Vesper", "Halcyon", "Old Tam"];
        var names = Enumerable.Range(0, count).Select(i => i < 3 ? first[i] : $"Core {i}").ToList();

        Assert.Equal(expected, StatusRows.OwnCores(names));
    }

    [Fact]
    public void NoBoundShipSaysChangingShipChangesNothing()
    {
        var text = StatusRows.ShipCores([], 12);

        Assert.Equal(
            "No ship has its own core. Until one does, changing ship changes nothing about who answers you.",
            text.Block);
        Assert.Null(text.Second);
    }

    [Fact]
    public void OneShipInAFleetOfOne()
    {
        Assert.Equal(
            "1 of 1 ship has a core: Cold Iron with Warden",
            StatusRows.ShipCores(["Cold Iron with Warden"], 1).Block);
    }

    [Fact]
    public void ThreeShipsAreAllNamed()
    {
        var text = StatusRows.ShipCores(
            ["Cold Iron with Warden", "Long Odds with Cora", "Brass Halo with Analyst Prime"], 12);

        Assert.Equal(
            "3 of 12 ships have a core: Cold Iron with Warden, Long Odds with Cora and Brass Halo with Analyst Prime",
            text.Block);
    }

    [Fact]
    public void NineShipsNameThreeAndCountSix()
    {
        string[] first = ["Cold Iron with Warden", "Long Odds with Cora", "Brass Halo with Analyst Prime"];
        var bound = first.Concat(Enumerable.Range(0, 6).Select(i => $"Ship {i} with Cora")).ToList();

        Assert.Equal(
            "9 of 23 ships have a core: Cold Iron with Warden, Long Odds with Cora, Brass Halo with Analyst Prime and 6 more",
            StatusRows.ShipCores(bound, 23).Block);
    }

    [Fact]
    public void AnUnreadFleetNamesTheBoundShipsAndSaysWhatIsMissing()
    {
        var text = StatusRows.ShipCores(["Cold Iron with Warden", "Long Odds with Cora"], null);

        Assert.Equal("2 ships have a core: Cold Iron with Warden and Long Odds with Cora", text.Block);
        Assert.Equal("Dock somewhere with a shipyard to read the rest.", text.Second);
    }
}
