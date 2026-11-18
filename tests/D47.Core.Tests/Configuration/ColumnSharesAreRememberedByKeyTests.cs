using D47.Core.Configuration;
using System.Text.Json;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Dragged column shares are kept by table id and column key (#1612).</summary>
public class ColumnSharesAreRememberedByKeyTests
{
    [Fact]
    public void AShareIsKeptUnderItsTableAndColumn()
    {
        var state = new ViewState().With("fleet", "plan", 0.3).With("fleet", "current", 0.2).With("crew", "name", 0.15);

        Assert.Equal(0.3, state.ColumnShares["fleet"]["plan"]);
        Assert.Equal(0.2, state.ColumnShares["fleet"]["current"]);
        Assert.Equal(0.15, state.ColumnShares["crew"]["name"]);
    }

    [Fact]
    public void ForgettingTheLastShareOfATableDropsTheTable()
    {
        var state = new ViewState().With("fleet", "plan", 0.3).Without("fleet", "plan");

        Assert.Empty(state.ColumnShares);
    }

    [Fact]
    public void ColumnSharesSurviveSavingAndLoading()
    {
        var state = new ViewState().With("fleet", "plan", 0.305);

        var round = JsonSerializer.Deserialize<ViewState>(JsonSerializer.Serialize(state));

        Assert.Equal(0.305, round!.ColumnShares["fleet"]["plan"]);
    }

    [Fact]
    public void AFileWithoutTheKeyLoadsAsEmpty()
    {
        var state = JsonSerializer.Deserialize<ViewState>("{}");

        Assert.Empty(state!.ColumnShares);
    }

    [Fact]
    public void WidthsStoredInPixelsByAnOlderBuildAreDropped()
    {
        var state = JsonSerializer.Deserialize<ViewState>("""{"ColumnWidths":{"fleet":{"plan":300}}}""");

        Assert.Empty(state!.ColumnShares);
    }
}
