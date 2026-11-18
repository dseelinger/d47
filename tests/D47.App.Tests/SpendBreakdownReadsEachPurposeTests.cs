using System.Globalization;
using D47.App.Controls;
using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

/// <summary>The breakdown page's figures and words, from the ledger's purpose shares (#2562).</summary>
public class SpendBreakdownReadsEachPurposeTests
{
    private static string Money(decimal dollars) => dollars.ToString("C4", CultureInfo.CurrentCulture);

    [Fact]
    public void APartlyPricedPurposeReadsAsAFloorWithItsUnpricedCount()
    {
        var share = new SpendPurposeShare(SpendPurposes.Conversation, SpendKind.Model, 5, 4.2057m, 3);

        Assert.Equal("≥ " + Money(4.2057m), SpendBreakdownModel.CostText(share));
        Assert.Equal("3 with no rate", SpendBreakdownModel.UnpricedText(share));
    }

    [Fact]
    public void APurposeWithNoPricedChargeReadsNoRateAlone()
    {
        var share = new SpendPurposeShare(SpendPurposes.WebLookup, SpendKind.Model, 2, 0m, 2);

        Assert.Equal("no rate", SpendBreakdownModel.CostText(share));
        Assert.Null(SpendBreakdownModel.UnpricedText(share));
        Assert.Equal("—", SpendBreakdownModel.ShareText(share, 10m));
    }

    [Fact]
    public void ShareIsThePurposesCostOverTheWindowsToOnePlace()
    {
        var share = new SpendPurposeShare(SpendPurposes.Conversation, SpendKind.Model, 412, 1.32m, 0);

        Assert.Equal("13.2%", SpendBreakdownModel.ShareText(share, 10m));
        Assert.Equal("—", SpendBreakdownModel.ShareText(share, 0m));
    }

    [Fact]
    public void ARowWrittenBeforePurposesWereRecordedIsNotRecorded()
    {
        var share = new SpendPurposeShare(null, SpendKind.Voice, 1, 0.1m, 0);

        Assert.True(SpendBreakdownModel.NotRecorded(share));
        Assert.Equal("Not recorded", SpendBreakdownModel.NameOf(share));
        Assert.Equal("VOICE", SpendBreakdownModel.KindWord(share.Kind));
    }

    [Theory]
    [InlineData("Today", "Nothing has been charged today.")]
    [InlineData("This week", "Nothing has been charged this week.")]
    [InlineData("Last 7 days", "Nothing has been charged in the last 7 days.")]
    [InlineData("This month", "Nothing has been charged this month.")]
    [InlineData("Last 30 days", "Nothing has been charged in the last 30 days.")]
    public void NothingChargedNamesTheWindow(string window, string said) =>
        Assert.Equal(said, SpendBreakdownModel.NothingText(window));

    [Fact]
    public void TheDefaultOrderIsCostMostFirstWithNoRateLast()
    {
        IReadOnlyList<SpendPurposeShare> shares =
        [
            new(SpendPurposes.WebLookup, SpendKind.Model, 1, 0m, 1),
            new(SpendPurposes.Debrief, SpendKind.Model, 1, 0.2m, 0),
            new(SpendPurposes.StoryChapter, SpendKind.Model, 1, 3m, 0),
        ];

        Assert.Equal(
            [SpendPurposes.StoryChapter, SpendPurposes.Debrief, SpendPurposes.WebLookup],
            SpendBreakdownModel.InDefaultOrder(shares).Select(share => share.Purpose));
    }

    [Fact]
    public void AWindowWithAnyUnpricedChargeTotalsAsAFloor()
    {
        IReadOnlyList<SpendPurposeShare> shares =
        [
            new(SpendPurposes.Conversation, SpendKind.Model, 3, 1.5m, 0),
            new(SpendPurposes.WebLookup, SpendKind.Model, 2, 0m, 2),
        ];

        var figures = SpendBreakdownModel.WindowFigures.Of(shares);

        Assert.Equal(5, figures.Charges);
        Assert.Equal("≥ " + Money(1.5m), SpendBreakdownModel.TotalText(figures));
        Assert.Equal("1 charge", SpendBreakdownModel.ChargesText(1));
        Assert.Equal("2 purposes", SpendBreakdownModel.PurposesText(2));
    }
}
