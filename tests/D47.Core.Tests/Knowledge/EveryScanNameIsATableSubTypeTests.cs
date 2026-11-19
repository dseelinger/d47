using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Every Spansh name <see cref="CodexKinds.OfScan(D47.Core.Journal.JournalEvent)"/> maps a journal value to is one the
/// shipped <c>CodexBodies.tsv</c> uses: each star and planet subType, each white dwarf class the table has, each gas at
/// the end of some atmosphere, and each ring type.
/// </summary>
public class EveryScanNameIsATableSubTypeTests
{
    private static readonly List<CodexKind> Kinds = [.. CodexCatalogue.Table.All.Select(entry => entry.Kind).OfType<CodexKind>()];

    [Fact]
    public void EveryStarAndPlanetSubTypeIsInTheTable()
    {
        var subTypes = Kinds.Select(kind => kind.SubType).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(CodexKinds.StarTypes.Values.Concat(CodexKinds.PlanetClasses.Values), name => !subTypes.Contains(name));
    }

    [Theory]
    [InlineData("D")]
    [InlineData("DA")]
    [InlineData("DAB")]
    [InlineData("DAV")]
    [InlineData("DAZ")]
    [InlineData("DB")]
    [InlineData("DBV")]
    [InlineData("DBZ")]
    [InlineData("DC")]
    [InlineData("DCV")]
    [InlineData("DQ")]
    public void AWhiteDwarfClassIsATableSubType(string code) =>
        Assert.Contains(Kinds, kind => kind.SubType == "White Dwarf (" + code + ") Star");

    [Fact]
    public void EveryGasEndsAnAtmosphereInTheTable()
    {
        var atmospheres = Kinds.Select(kind => kind.Atmosphere).OfType<string>().ToList();

        Assert.DoesNotContain(CodexKinds.AtmosphereTypes.Values, gas =>
            !atmospheres.Any(atmosphere => atmosphere == gas || atmosphere.EndsWith(" " + gas, StringComparison.Ordinal)));
    }

    [Fact]
    public void EveryRingTypeIsInTheTable()
    {
        var rings = Kinds.Select(kind => kind.Rings).OfType<string>().SelectMany(set => set.Split(',')).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(CodexKinds.RingClasses.Values, type => !rings.Contains(type));
    }
}
