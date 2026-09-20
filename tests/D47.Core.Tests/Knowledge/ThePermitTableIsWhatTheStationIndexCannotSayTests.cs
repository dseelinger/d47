using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The shipped permit table. The station index carries no permit field at all — measured against a
/// station search anchored on Alioth, 2026-09-20 — so a trade plan has nothing else to answer from
/// (#310).
/// </summary>
public class ThePermitTableIsWhatTheStationIndexCannotSayTests
{
    [Theory]
    [InlineData("Sol")]
    [InlineData("Shinrarta Dezhra")]
    [InlineData("Alioth")]
    [InlineData("Achenar")]
    [InlineData("Sirius")]
    [InlineData("Beta Hydri")]
    public void TheSystemsEveryCommanderKnowsAreLockedAreInIt(string system) =>
        Assert.True(PermitSystemTable.Locked(system), $"{system} needs a permit and the table does not say so.");

    [Theory]
    [InlineData("RR Caeli")]
    [InlineData("Ega")]
    [InlineData("Alpha Centauri")]
    public void AnOpenSystemIsNotInIt(string system) => Assert.False(PermitSystemTable.Locked(system));

    /// <summary>
    /// The name is matched against whatever spelling a market snapshot carries, which comes from a
    /// third-party index rather than from this table.
    /// </summary>
    [Fact]
    public void TheSpellingIsMatchedWithoutRegardToCase() =>
        Assert.True(PermitSystemTable.Locked("shinrarta dezhra"));

    [Fact]
    public void NothingIsLockedByAnAbsentName()
    {
        Assert.False(PermitSystemTable.Locked(null));
        Assert.False(PermitSystemTable.Locked(string.Empty));
    }

    /// <summary>
    /// Most of the table is procedurally-named systems inside permit-locked regions, which is the
    /// half a Commander cannot recognise by eye and the reason the table is worth shipping.
    /// </summary>
    [Fact]
    public void ItCarriesTheWholeGalaxysWorthRatherThanTheFamousFew() =>
        Assert.True(
            PermitSystemTable.Names.Count > 1_000,
            $"The permit table holds {PermitSystemTable.Names.Count} systems; the generator measured 2,702.");
}
