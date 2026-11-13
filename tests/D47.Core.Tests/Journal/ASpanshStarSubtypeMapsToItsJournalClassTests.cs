using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ASpanshStarSubtypeMapsToItsJournalClassTests
{
    private static bool IsStar(string subtype) =>
        subtype.EndsWith(" Star", StringComparison.Ordinal) || subtype.Contains("Black Hole", StringComparison.Ordinal);

    [Theory]
    [InlineData("O (Blue-White) Star", "O")]
    [InlineData("B (Blue-White) Star", "B")]
    [InlineData("A (Blue-White) Star", "A")]
    [InlineData("F (White) Star", "F")]
    [InlineData("G (White-Yellow) Star", "G")]
    [InlineData("K (Yellow-Orange) Star", "K")]
    [InlineData("M (Red dwarf) Star", "M")]
    [InlineData("L (Brown dwarf) Star", "L")]
    [InlineData("T (Brown dwarf) Star", "T")]
    [InlineData("Y (Brown dwarf) Star", "Y")]
    [InlineData("A (Blue-White super giant) Star", "A_BlueWhiteSuperGiant")]
    [InlineData("K (Yellow-Orange giant) Star", "K_OrangeGiant")]
    [InlineData("M (Red giant) Star", "M_RedGiant")]
    [InlineData("Neutron Star", "N")]
    [InlineData("Black Hole", "H")]
    [InlineData("Herbig Ae/Be Star", "AeBe")]
    [InlineData("T Tauri Star", "TTS")]
    [InlineData("S-type Star", "S")]
    [InlineData("C Star", "C")]
    [InlineData("Wolf-Rayet Star", "W")]
    [InlineData("White Dwarf (D) Star", "D")]
    [InlineData("White Dwarf (DA) Star", "DA")]
    [InlineData("White Dwarf (DAB) Star", "DAB")]
    [InlineData("White Dwarf (DAZ) Star", "DAZ")]
    [InlineData("White Dwarf (DC) Star", "DC")]
    [InlineData("White Dwarf (DQ) Star", "DQ")]
    public void AConfirmedSubtypeGivesTheExactCode(string subtype, string code) =>
        Assert.Equal(code, StarClasses.FromSubtype(subtype));

    [Fact]
    public void EveryStarSubtypeMapsAndPlanetSubtypesDoNot()
    {
        foreach (var subtype in BodyCatalogue.Subtypes)
        {
            if (IsStar(subtype))
            {
                Assert.NotNull(StarClasses.FromSubtype(subtype));
            }
            else
            {
                Assert.Null(StarClasses.FromSubtype(subtype));
            }
        }
    }

    [Fact]
    public void OnlyTheMainSequenceGiantAndSuperGiantStarsAreScoopable()
    {
        foreach (var subtype in BodyCatalogue.Subtypes.Where(IsStar))
        {
            var scoopable = StarClasses.IsScoopable(StarClasses.FromSubtype(subtype));
            Assert.NotNull(scoopable);

            var expected = "OBAFGKM".Contains(subtype[0]) && subtype.Contains('(') &&
                !subtype.StartsWith("White", StringComparison.Ordinal);

            Assert.True(expected == scoopable, subtype);
        }
    }

    [Theory]
    [InlineData("MS-type Star")]
    [InlineData("Herbig Ae/Be Star")]
    [InlineData("White Dwarf (DAV) Star")]
    [InlineData("Supermassive Black Hole")]
    public void ASubtypeThatMerelyStartsWithAScoopableLetterIsNotScoopable(string subtype) =>
        Assert.False(StarClasses.IsScoopable(StarClasses.FromSubtype(subtype)));

    [Theory]
    [InlineData("Q (Imaginary) Star")]
    [InlineData("")]
    [InlineData(null)]
    public void AnUnknownSubtypeMapsToNull(string? subtype) =>
        Assert.Null(StarClasses.FromSubtype(subtype));
}
