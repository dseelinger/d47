using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

public sealed class TheMinimumPopulationFieldSendsOneArgumentTests
{
    [Theory]
    [InlineData(true, "", "0-0")]
    [InlineData(true, "500", "0-0")]
    [InlineData(false, "1", "1-")]
    [InlineData(false, "1,000,000", "1000000-")]
    [InlineData(false, "250000", "250000-")]
    [InlineData(false, "0", null)]
    [InlineData(false, "", null)]
    public void TheArgumentFollowsTheTileOrTheBox(bool unpopulated, string text, string? expected) =>
        Assert.Equal(expected, MinimumPopulation.Argument(unpopulated, text));

    [Theory]
    [InlineData("", "", null)]
    [InlineData("", "50", "50")]
    [InlineData("10", "", "10-")]
    [InlineData("10", "50", "10-50")]
    public void ARangeIsWrittenAsTheToolReadsIt(string from, string to, string? expected) =>
        Assert.Equal(expected, SystemSearchPage.Range(from, to));

    [AvaloniaFact]
    public void EachTileFillsTheBoxAndSendsItsArgument()
    {
        var box = SystemSearchPage.Field("minimum population", mono: true);
        var field = new MinimumPopulation(box);
        var tiles = field.GetLogicalDescendants().OfType<CheckBox>().ToDictionary(tile => (string)tile.Content!);

        tiles["High"].IsChecked = true;
        Assert.Equal("1,000,000", box.Text);
        Assert.Equal("1000000-", field.Asked);

        tiles["Populated"].IsChecked = true;
        Assert.Equal("1", box.Text);
        Assert.False(tiles["High"].IsChecked);
        Assert.Equal("1-", field.Asked);

        tiles["Unpopulated"].IsChecked = true;
        Assert.Equal(string.Empty, box.Text);
        Assert.False(tiles["Populated"].IsChecked);
        Assert.Equal("0-0", field.Asked);

        box.Text = "250000";
        Assert.False(tiles["Unpopulated"].IsChecked);
        Assert.Equal("250000-", field.Asked);

        box.Text = "0";
        Assert.Null(field.Asked);

        box.Text = "1000000";
        Assert.True(tiles["High"].IsChecked);
    }

    [AvaloniaFact]
    public void ASavedPopulationSelectsItsTile()
    {
        var box = SystemSearchPage.Field("minimum population", mono: true);
        var field = new MinimumPopulation(box);
        var tiles = field.GetLogicalDescendants().OfType<CheckBox>().ToDictionary(tile => (string)tile.Content!);

        field.Set("0");
        Assert.True(tiles["Unpopulated"].IsChecked);
        Assert.Equal("0-0", field.Asked);

        field.Set("0-0");
        Assert.True(tiles["Unpopulated"].IsChecked);

        field.Set("1000000-");
        Assert.True(tiles["High"].IsChecked);
        Assert.False(tiles["Unpopulated"].IsChecked);
        Assert.Equal("1,000,000", box.Text);

        field.Set("300-");
        Assert.False(tiles["High"].IsChecked);
        Assert.Equal("300-", field.Asked);
    }
}
