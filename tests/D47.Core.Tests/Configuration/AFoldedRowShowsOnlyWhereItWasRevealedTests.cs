using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public sealed class AFoldedRowShowsOnlyWhereItWasRevealedTests
{
    private static SettingRow Row(string key, bool advanced = false, bool applies = true) =>
        new()
        {
            Key = key,
            Label = key,
            Help = "Help.",
            Kind = SettingKind.Toggle,
            Advanced = advanced,
            AppliesWhen = _ => applies,
        };

    private static readonly IReadOnlyList<SievePlace> Places = [new("One", [], 0), new("Two", [], 0)];

    private static SettingsSift Sift(IReadOnlyList<SieveRow> rows, params int[] revealed) =>
        SettingsSieve.Sift(
            string.Empty, rows, Places, ["Area"], [], D47Settings.Defaults, _ => false, false, new HashSet<int>(revealed));

    [Fact]
    public void AFoldedRowIsHiddenUnlessItsOwnPlaceWasRevealed()
    {
        SieveRow[] rows = [new(Row("a", advanced: true), 0, -1, null)];

        Assert.False(Sift(rows).Rows[0].Shown);
        Assert.False(Sift(rows, 1).Rows[0].Shown);
        Assert.True(Sift(rows, 0).Rows[0].Shown);
    }

    [Fact]
    public void AFoldedRowCountsAsFoldedWhetherOrNotItIsRevealed()
    {
        SieveRow[] rows = [new(Row("a", advanced: true), 0, -1, null)];

        Assert.Equal(1, Sift(rows).Places[0].Folded);
        Assert.Equal(1, Sift(rows, 0).Places[0].Folded);
    }

    [Fact]
    public void ARowThatDoesNotApplyCountsInNeitherFoldedNorExists()
    {
        SieveRow[] rows = [new(Row("a", advanced: true, applies: false), 0, -1, null)];

        var place = Sift(rows, 0).Places[0];

        Assert.False(place.Exists);
        Assert.Equal(0, place.Folded);
        Assert.False(Sift(rows, 0).Rows[0].Applies);
    }
}
