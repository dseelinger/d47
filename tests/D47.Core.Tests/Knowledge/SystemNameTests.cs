using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Decoding a procedural system name (Phase 18, "Read a system name").
/// <para>
/// The grammar was validated against 4,746 distinct real names from the corpus — 2,854 procedural,
/// 1,892 hand-named, and no near-miss — but the corpus is one Commander's travel history and stays
/// out of a public repository, so the cases here are the <em>shapes</em> that survey found rather
/// than the names themselves.
/// </para>
/// </summary>
public class SystemNameTests
{
    [Fact]
    public void TheWorkedExampleDecodesIntoItsParts()
    {
        var name = SystemName.Read("Dryafea PO-X d2-0");

        Assert.True(name.IsProcedural);
        Assert.Equal("Dryafea", name.Sector);
        Assert.Equal("PO-X", name.Letters);
        Assert.Equal('d', name.MassCode);
        Assert.Equal(2, name.BoxelNumber);
        Assert.Equal(0, name.SystemNumber);
        Assert.Equal(80, name.BoxLightYears);
        Assert.Equal("PO-X d2", name.Boxel);
    }

    /// <summary>
    /// A sector name is anything up to the letters, and plenty of real ones carry spaces and digits.
    /// A grammar that assumed one word would refuse a large slice of the galaxy.
    /// </summary>
    [Theory]
    [InlineData("Col 285 Sector AB-C d1-2", "Col 285 Sector")]
    [InlineData("Jellyfish Sector FB-X c1-5", "Jellyfish Sector")]
    [InlineData("NGC 3199 Sector JC-V b2-4", "NGC 3199 Sector")]
    [InlineData("Synuefe AA-A a0", "Synuefe")]

    // A multi-word sector *and* no boxel number at once, which is where a grammar that handled
    // either alone would come apart.
    [InlineData("Pleiades Sector IR-W d1", "Pleiades Sector")]
    public void SectorNamesWithSpacesAndDigitsStillParse(string full, string sector) =>
        Assert.Equal(sector, SystemName.Read(full).Sector);

    /// <summary>
    /// The boxel number is written only once a sector's letters have run out, so its absence means
    /// zero rather than unknown — and a parser that demanded it would reject a real name and report
    /// "no mass code here", which is a wrong answer wearing the shape of a right one.
    /// </summary>
    [Fact]
    public void AMissingBoxelNumberIsZeroRatherThanAFailureToParse()
    {
        var name = SystemName.Read("Synuefe AA-A a0");

        Assert.True(name.IsProcedural);
        Assert.Equal(0, name.BoxelNumber);
        Assert.Equal(0, name.SystemNumber);
        Assert.Equal("AA-A a", name.Boxel);
    }

    /// <summary>
    /// The ladder, derived by regressing the boxel index out of a name against real <c>StarPos</c>
    /// coordinates: 9.99, 20.02, 39.51, 78.23 and 165.32 measured for a through e, and the top three
    /// following the doubling to the sector's own 1,280.
    /// </summary>
    [Theory]
    [InlineData('a', 10)]
    [InlineData('b', 20)]
    [InlineData('c', 40)]
    [InlineData('d', 80)]
    [InlineData('e', 160)]
    [InlineData('f', 320)]
    [InlineData('g', 640)]
    [InlineData('h', 1280)]
    public void TheBoxLadderDoublesFromTenToTheSector(char mass, int lightYears) =>
        Assert.Equal(lightYears, SystemName.Read($"Fixture AA-A {mass}0").BoxLightYears);

    /// <summary>
    /// The internal consistency check that closes the ladder: eight rungs doubling from 10 land
    /// exactly on the sector, which is what mass code <c>h</c> is.
    /// </summary>
    [Fact]
    public void TheTopOfTheLadderIsTheSectorItself() =>
        Assert.Equal(SystemName.SectorLightYears, SystemName.Read("Fixture AA-A h0").BoxLightYears);

    /// <summary>
    /// Which rungs rest on a measurement and which on the doubling is surfaced rather than hidden,
    /// because a figure resting on an inference should say so where a Commander can see it.
    /// </summary>
    [Theory]
    [InlineData('a', true)]
    [InlineData('e', true)]
    [InlineData('f', false)]
    [InlineData('h', false)]
    public void WhetherABoxSizeWasMeasuredIsPartOfTheAnswer(char mass, bool measured) =>
        Assert.Equal(measured, SystemName.Read($"Fixture AA-A {mass}0").BoxSizeMeasured);

    [Theory]
    [InlineData('a', 1)]
    [InlineData('d', 4)]
    [InlineData('h', 8)]
    public void TheMassCodeIsRankedRatherThanLeftAsALetter(char mass, int rank) =>
        Assert.Equal(rank, SystemName.Read($"Fixture AA-A {mass}0").MassRank);

    /// <summary>
    /// A hand-named system is a real answer, not a parse failure. 1,892 of the 4,746 names surveyed
    /// were hand-named, so this is the common case rather than the edge one.
    /// </summary>
    [Theory]
    [InlineData("Sol")]
    [InlineData("Colonia")]
    [InlineData("HIP 12685")]
    [InlineData("Barnard's Star")]
    public void AHandNamedSystemIsReadAsHavingNoMassCode(string spoken)
    {
        var name = SystemName.Read(spoken);

        Assert.False(name.IsProcedural);
        Assert.Null(name.MassCode);
        Assert.Null(name.BoxLightYears);
        Assert.Equal(spoken, name.Value);
    }

    /// <summary>
    /// Things that look procedural and are not. The mass code is a single letter a through h, so a
    /// stray letter outside that range is not a boxel with an unknown size — it is not a boxel.
    /// </summary>
    [Theory]
    [InlineData("Fixture AA-A z0")]
    [InlineData("Fixture AA-A d")]
    [InlineData("Fixture A-A d1-2")]
    [InlineData("Fixture AA-A D1-2")]
    [InlineData("AA-A d1-2")]
    public void NamesThatMerelyResembleTheGrammarAreNotDecoded(string spoken) =>
        Assert.False(SystemName.Read(spoken).IsProcedural);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingAtAllIsAnEmptyReadRatherThanAThrow(string? spoken)
    {
        var name = SystemName.Read(spoken);

        Assert.False(name.IsProcedural);
        Assert.Equal(string.Empty, name.Value);
    }

    [Fact]
    public void SurroundingWhitespaceIsTrimmedRatherThanBreakingTheMatch() =>
        Assert.Equal('d', SystemName.Read("  Dryafea PO-X d2-0  ").MassCode);
}
