using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The shipped table holds no Canonn row for an ordinary star or planet type, only d47's kinds, so logging a Class I
/// gas giant is never judged a region first.
/// </summary>
public class AnOrdinaryGasGiantIsNeverARegionFirstTests
{
    private const long ClassIGasGiant = 1200501;

    [Fact]
    public void AClassIGasGiantIsNoFirstInAnyRegion()
    {
        var regions = GalacticRegions.Names.Select(name => GalacticRegions.IdOf(name)!.Value).ToList();

        Assert.NotEmpty(regions);
        Assert.Null(CodexCatalogue.Table.Entry(ClassIGasGiant));
        Assert.All(regions, region =>
            Assert.Equal(CodexFirst.None, CodexFirsts.Judge(CodexCatalogue.Table, ClassIGasGiant, region, "$Codex_SubCategory_Gas_Giants;")));
    }
}
