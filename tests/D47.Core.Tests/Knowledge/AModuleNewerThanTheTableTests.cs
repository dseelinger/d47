using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary> Modules Frontier ships ahead of the id sources. </summary>
public class AModuleNewerThanTheTableTests
{
    [Fact]
    public void AModuleFrontierNamesSurvivesWithoutFigures()
    {
        // The Mk II cabins are in outfitting.csv and in no coriolis-data file.
        var cabin = EliteSpecifications.Module("int_mkii_passengercabin_size5_class1");

        Assert.NotNull(cabin);
        // Qualified from its own symbol by the existing disambiguator, because outfitting.csv files the size
        // 5 and size 6 cabins under one name and one class.
        Assert.Equal("MkII Economy Class Passenger Cabin (size5)", cabin.Name);
        Assert.Equal(5, cabin.Class);
        Assert.Null(cabin.Mass);

        // And it still knows what kind of thing it is, so a slot can offer it.
        Assert.Equal("ipc", cabin.Type);
    }

    [Theory]
    [InlineData("int_hyperdrive_size8_class5", "Frame Shift Drive")]
    [InlineData("int_stellarbodydiscoveryscanner_advanced", "Advanced Discovery Scanner")]
    [InlineData("int_engine_size2_class1_free", "Thrusters (free)")]
    public void SoDoTheOtherTwentySeven(string symbol, string named) =>
        Assert.Equal(named, EliteSpecifications.Module(symbol)?.Name);

    [Fact]
    public void AModuleNobodyNamesIsSaidByItsGroup()
    {
        // The reported one.
        Assert.Null(EliteSpecifications.Module("int_fighterbaymk2_size5_class1_free"));

        Assert.Equal(
            "Fighter Hangar (newer than my table)",
            EliteSpecifications.ModuleName("int_fighterbaymk2_size5_class1_free"));
    }

    [Fact]
    public void AGroupWithMoreThanOneNameIsNotGuessedAt()
    {
        // Five unnamed modules are passenger cabins, whose group holds Economy, Business, First and Luxury.
        var said = EliteSpecifications.ModuleName("int_passengercabin_size4_class0");

        Assert.NotNull(said);
        Assert.DoesNotContain("newer than my table", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AModuleNobodyHasEverHeardOfIsStillNotInvented()
    {
        // The floor: a symbol in no source at all gets the readable fallback and no claim.
        var said = EliteSpecifications.ModuleName("int_notathing_size9_class9");

        Assert.DoesNotContain("newer than my table", said ?? string.Empty, StringComparison.Ordinal);
    }
}
