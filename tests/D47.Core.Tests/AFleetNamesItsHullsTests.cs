using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A stored ship names its hull rather than spelling its symbol
/// (https://github.com/dseelinger/d47/issues/105).
/// <para>
/// <b>The third time a raw symbol reached the Commander, and the first two produced the fix this
/// one was missing.</b> <c>StoredShip.Describe()</c> was the one <c>Describe</c> that never climbed
/// <see cref="EliteSpecifications.HullSaid"/>, so the same fleet had two spellings depending on
/// which event a row came from — <em>Tulimiekka (Kestrel Mk II)</em> off a Loadout beside
/// <em>Kofu (corsair)</em> off StoredShips.
/// </para>
/// <para>
/// <b>The hulls here are chosen against the trap that hid it twice.</b> <c>StoredShips</c> carries
/// no <c>ShipType_Localised</c> at all, and every hull anybody would reach for while writing a test
/// — Anaconda, Krait — has a measured row, so it comes out right whether or not the ladder is
/// climbed. Only a hull named from its <em>armour</em> tells the two apart.
/// </para>
/// </summary>
public class AFleetNamesItsHullsTests
{
    private static StoredShip Stored(string type, string? name = null) =>
        new(12, type, name, "Shinrarta Dezhra");

    /// <summary>
    /// The three hulls with no measured row, which is the whole point: these are named from their
    /// armour, so a caller that skips the ladder shows the symbol and one that climbs it does not.
    /// </summary>
    [Theory]
    [InlineData("corsair", "Corsair")]
    [InlineData("explorer_nx", "Caspian Explorer")]
    [InlineData("smallcombat01_nx", "Kestrel Mk II")]
    public void AHullWithNoMeasuredRowIsStillNamed(string symbol, string expected)
    {
        // These three have no measured row in the ships table — only slots and armour — so their
        // names come from NamesFromArmour. That is what makes them the right hulls to test with:
        // every hull anybody would reach for instead (Anaconda, Krait) is named correctly whether
        // or not the ladder is climbed, which is how this defect survived two fixes.
        Assert.Equal(expected, EliteSpecifications.HullSaid(symbol));
        Assert.Equal($"Kofu ({expected})", Stored(symbol, "Kofu").Describe());
        Assert.Equal(expected, Stored(symbol).Describe());
    }

    /// <summary>
    /// And the reported string exactly: this is what was sitting in <c>data/ship-cores.json</c> on
    /// the installed build.
    /// </summary>
    [Fact]
    public void TheReportedStringsReadAsNames()
    {
        Assert.Equal("Kofu (Corsair)", Stored("corsair", "Kofu").Describe());
        Assert.Equal("Flamebrand (Anaconda)", Stored("anaconda", "Flamebrand").Describe());
        Assert.Equal("Type-10 Defender", Stored("type9_military").Describe());
    }

    /// <summary>
    /// A hull nothing knows is no worse than before. The ladder hands back what it was given when
    /// the measured row, the armour prefix and the spoken match all miss, so a hull Frontier adds
    /// tomorrow still says something rather than nothing.
    /// </summary>
    [Fact]
    public void AHullNothingKnowsIsHandedBackUnchanged()
    {
        Assert.Equal("Sidewinder II (hull_nobody_has_measured)", Stored("hull_nobody_has_measured", "Sidewinder II").Describe());
    }

    /// <summary>
    /// Casing does not matter, which is what makes this safe against Frontier's own spelling: the
    /// journal writes <c>SmallCombat01_NX</c> in a live event and <c>smallcombat01_nx</c> in a
    /// stored one, and both are the same ship.
    /// </summary>
    [Fact]
    public void FrontiersOwnCasingIsNamedToo()
    {
        Assert.Equal("Kestrel Mk II", Stored("SmallCombat01_NX").Describe());
    }
}
