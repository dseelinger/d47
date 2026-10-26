using D47.Core.Configuration;
using System.Text.Json;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Dragged column widths are kept by table id and column key (#1612).</summary>
public class ColumnWidthsAreRememberedByKeyTests
{
    [Fact]
    public void AWidthIsKeptUnderItsTableAndColumn()
    {
        var state = new ViewState().With("fleet", "plan", 120).With("fleet", "current", 80).With("crew", "name", 60);

        Assert.Equal(120, state.ColumnWidths["fleet"]["plan"]);
        Assert.Equal(80, state.ColumnWidths["fleet"]["current"]);
        Assert.Equal(60, state.ColumnWidths["crew"]["name"]);
    }

    [Fact]
    public void ForgettingTheLastWidthOfATableDropsTheTable()
    {
        var state = new ViewState().With("fleet", "plan", 120).Without("fleet", "plan");

        Assert.Empty(state.ColumnWidths);
    }

    [Fact]
    public void ColumnWidthsSurviveSavingAndLoading()
    {
        var state = new ViewState().With("fleet", "plan", 120.5);

        var round = JsonSerializer.Deserialize<ViewState>(JsonSerializer.Serialize(state));

        Assert.Equal(120.5, round!.ColumnWidths["fleet"]["plan"]);
    }

    [Fact]
    public void AFileWithoutTheKeyLoadsAsEmpty()
    {
        var state = JsonSerializer.Deserialize<ViewState>("{}");

        Assert.Empty(state!.ColumnWidths);
    }
}
