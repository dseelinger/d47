using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public sealed class ASearchShowsWhatItNamesTests
{
    private static SettingRow Row(string key, string label, string help = "Plain help.") =>
        new() { Key = key, Label = label, Help = help, Kind = SettingKind.Toggle };

    private static readonly IReadOnlyList<string> Areas = ["Speech", "Ship"];

    // Place 0 and 1 in area 0; place 2 in area 1.
    private static readonly IReadOnlyList<SievePlace> Places =
    [
        new("Microphone", ["ptt"], 0),
        new("Voices", [], 0),
        new("Cargo", [], 1),
    ];

    private static readonly IReadOnlyList<SieveGroup> Groups =
    [
        new("Input", "Where [the mic](voiceid) listens."),
    ];

    private static readonly IReadOnlyList<SieveRow> Rows =
    [
        new(Row("a", "Alpha"), 0, 0, null),
        new(Row("b", "Bravo"), 1, -1, null),
        new(Row("c", "Charlie"), 2, -1, null),
    ];

    private static SettingsSift Sift(string query) =>
        SettingsSieve.Sift(
            query, Rows, Places, Areas, Groups, D47Settings.Defaults, _ => false, true, new HashSet<int>());

    [Fact]
    public void ATermOfAPlaceShowsEveryRowOfThatPlace()
    {
        var sift = Sift("ptt");

        Assert.Equal(1, sift.Places[0].Showing);
        Assert.Equal(0, sift.Places[1].Showing);
        Assert.Equal(0, sift.Places[2].Showing);
    }

    [Fact]
    public void AnAreaTitleShowsTheRowsOfEachOfItsPlaces()
    {
        var sift = Sift("speech");

        Assert.Equal(1, sift.Places[0].Showing);
        Assert.Equal(1, sift.Places[1].Showing);
        Assert.Equal(0, sift.Places[2].Showing);
    }

    [Fact]
    public void AGroupsPlainHelpShowsItsRowsButItsMarkupDoesNot()
    {
        var plain = Sift("listens");

        Assert.True(plain.Groups[0].Showing);
        Assert.Equal(1, plain.Places[0].Showing);

        var markup = Sift("voiceid");

        Assert.False(markup.Groups[0].Showing);
        Assert.Equal(0, markup.Places[0].Showing);
    }

    [Fact]
    public void AQueryThatNamesNothingLeavesNothingShowingAndEveryPlaceExisting()
    {
        var sift = Sift("zzzz");

        Assert.All(sift.Places, place => Assert.Equal(0, place.Showing));
        Assert.All(sift.Places, place => Assert.True(place.Exists));
        Assert.True(sift.Filtering);
    }
}
