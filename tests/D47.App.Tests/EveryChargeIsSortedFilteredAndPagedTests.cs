using System.Globalization;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Every charge list's order, filter and pages, and the words of its cells (#2563).</summary>
public class EveryChargeIsSortedFilteredAndPagedTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static string Money(decimal dollars) => dollars.ToString("C4", CultureInfo.CurrentCulture);

    private static SpendEntry Model(int minutesAgo, decimal dollars, bool priced = true, string purpose = SpendPurposes.Conversation) => new()
    {
        At = Noon.AddMinutes(-minutesAgo),
        Kind = SpendKind.Model,
        ProviderId = "anthropic",
        Model = "claude-sonnet-5-5",
        Purpose = purpose,
        Dollars = dollars,
        Priced = priced,
    };

    private static SpendEntry Voice(int minutesAgo, decimal dollars, string purpose = SpendPurposes.VoiceAboard, bool priced = true) => new()
    {
        At = Noon.AddMinutes(-minutesAgo),
        Kind = SpendKind.Voice,
        ProviderId = "elevenlabs",
        Model = "ElevenLabs",
        Purpose = purpose,
        Dollars = dollars,
        Priced = priced,
        Characters = 938,
    };

    /// <summary>A press on a column of the list's SortHeader.</summary>
    private static void Press(SpendChargeList list, int column) =>
        list.SortBy(ColumnSort.Next(SpendBreakdownModel.ChargeColumns, list.Order, column));

    private static List<SpendEntry> Many(int count) => [.. Enumerable.Range(0, count).Select(i => Model(i, 0.01m + (i * 0.0001m)))];

    [Fact]
    public void TheDefaultOrderIsCostMostFirstWithUnpricedRowsLast()
    {
        var cheap = Model(1, 0.01m);
        var dear = Model(2, 0.50m);
        var unpriced = Model(3, 0m, priced: false);
        var middle = Model(4, 0.20m);

        var page = new SpendChargeList().Of([cheap, unpriced, dear, middle]);

        Assert.Equal([dear, middle, cheap, unpriced], page.Rows);
    }

    [Fact]
    public void UnpricedRowsStayLastWhenCostIsReversed()
    {
        var cheap = Model(1, 0.01m);
        var dear = Model(2, 0.50m);
        var unpriced = Model(3, 0m, priced: false);

        var list = new SpendChargeList();
        Press(list, SpendBreakdownModel.ChargeCostColumn);
        Press(list, SpendBreakdownModel.ChargeCostColumn);

        Assert.Equal(new SortOrder(SpendBreakdownModel.ChargeCostColumn, SortDirection.Ascending), list.Order);
        Assert.Equal([cheap, dear, unpriced], list.Of([dear, unpriced, cheap]).Rows);
    }

    [Fact]
    public void TimesFirstPressIsNewestFirst()
    {
        var oldest = Model(30, 0.50m);
        var newest = Model(1, 0.01m);
        var between = Model(10, 0.20m);

        var list = new SpendChargeList();
        Press(list, SpendBreakdownModel.TimeColumn);

        Assert.Equal([newest, between, oldest], list.Of([oldest, newest, between]).Rows);
    }

    [Fact]
    public void SpokenLinesAreHiddenByDefaultAndCounted()
    {
        var model = Model(1, 0.40m);
        var spoken = Voice(2, 0.0610m);
        var unpricedSpoken = Voice(3, 0m, priced: false);

        var list = new SpendChargeList();
        var page = list.Of([model, spoken, unpricedSpoken]);

        Assert.Equal([model], page.Rows);
        Assert.Equal(1, page.Listed);
        Assert.Equal(2, page.Spoken.Charges);
        Assert.Equal($"Spoken lines are left out: 2 charges, ≥ {Money(0.0610m)}.", SpendBreakdownModel.SpokenText(page.Spoken, list.ShowSpoken));

        list.ShowSpoken = true;
        page = list.Of([model, spoken, unpricedSpoken]);

        Assert.Equal(3, page.Listed);
        Assert.Equal($"Spoken lines are shown: 2 charges, ≥ {Money(0.0610m)}.", SpendBreakdownModel.SpokenText(page.Spoken, list.ShowSpoken));
    }

    [Fact]
    public void AVoicePurposeShowsItsRowsWithSpokenLinesHidden()
    {
        var aboard = Voice(1, 0.06m);
        var aboardUnpriced = Voice(2, 0m, priced: false);
        var cast = Voice(3, 0.09m, SpendPurposes.VoiceStoryCast);
        var model = Model(4, 0.40m);

        var list = new SpendChargeList();
        list.FilterTo(SpendPurposes.VoiceAboard, SpendKind.Voice);

        Assert.False(list.ShowSpoken);

        var page = list.Of([aboard, aboardUnpriced, cast, model]);

        Assert.Equal([aboard, aboardUnpriced], page.Rows);
        Assert.Equal(new SpendPurposeShare(SpendPurposes.VoiceAboard, SpendKind.Voice, 2, 0.06m, 1), page.Purpose);
        Assert.Equal("2 charges for Aboard", SpendBreakdownModel.FilteredText(page.Purpose!));
    }

    [Fact]
    public void ARowWithNoPurposeIsFilteredAsNotRecorded()
    {
        var recorded = Model(1, 0.40m);
        var notRecorded = Model(2, 0.30m) with { Purpose = null };

        var list = new SpendChargeList();
        list.FilterTo(null, SpendKind.Model);

        Assert.Equal([notRecorded], list.Of([recorded, notRecorded]).Rows);
    }

    [Fact]
    public void EachChangeReturnsToTheFirstPage()
    {
        var rows = Many(250);
        var list = new SpendChargeList();

        void Turn(Action change)
        {
            list.NextPage();
            Assert.Equal(1, list.Of(rows).Page);
            change();
            Assert.Equal(0, list.Of(rows).Page);
        }

        Turn(() => Press(list, SpendBreakdownModel.TimeColumn));
        Turn(() => list.SortBy(null));
        Turn(() => list.FilterTo(SpendPurposes.Conversation, SpendKind.Model));
        Turn(list.ShowEveryPurpose);
        Turn(() => list.ShowSpoken = true);
        Turn(list.WindowChanged);
        Turn(() => list.Headset = true);
    }

    [Fact]
    public void APageIsAHundredRowsOnTheDesktopAndTwentyFiveInTheHeadset()
    {
        var rows = Many(250);
        var list = new SpendChargeList();

        var page = list.Of(rows);
        Assert.Equal(100, page.Rows.Count);
        Assert.Equal("1–100 of 250", page.Readout);
        Assert.False(page.HasPrevious);
        Assert.True(page.HasNext);

        list.NextPage();
        list.NextPage();
        page = list.Of(rows);
        Assert.Equal(50, page.Rows.Count);
        Assert.Equal("201–250 of 250", page.Readout);
        Assert.False(page.HasNext);

        list.Headset = true;
        page = list.Of(rows);
        Assert.Equal(25, page.Rows.Count);
        Assert.Equal(10, page.Pages);
        Assert.Equal("1–25 of 250", page.Readout);
    }

    [Fact]
    public void APageStepPastTheLastPageStaysOnIt()
    {
        var rows = Many(30);
        var list = new SpendChargeList { Headset = true };

        list.NextPage();
        list.NextPage();
        list.NextPage();

        Assert.Equal(1, list.Of(rows).Page);
    }

    [Fact]
    public void AModelCallReadsItsTokensAndCache()
    {
        var call = Model(1, 0.1m) with { InputTokens = 48_210, OutputTokens = 9_804, CacheReadTokens = 12_100, CacheWriteTokens = 2_480 };

        Assert.Equal($"{48_210:N0} in · {9_804:N0} out", SpendBreakdownModel.UsedText(call));
        Assert.Equal($"{12_100:N0} cache read · {2_480:N0} written", SpendBreakdownModel.UsedDetail(call));
        Assert.Equal($"{46_900:N0} cache written", SpendBreakdownModel.UsedDetail(call with { CacheReadTokens = 0, CacheWriteTokens = 46_900 }));
        Assert.Equal("3 web searches", SpendBreakdownModel.UsedDetail(call with { CacheReadTokens = 0, CacheWriteTokens = 0, WebSearchRequests = 3 }));
        Assert.Null(SpendBreakdownModel.UsedDetail(call with { CacheReadTokens = 0, CacheWriteTokens = 0 }));
    }

    [Fact]
    public void SpeechAndTranscriptionReadWhatTheySent()
    {
        var transcription = new SpendEntry { Kind = SpendKind.Transcription, ProviderId = "groq", Model = "whisper-large-v3", AudioSeconds = 3.4, Priced = true };

        Assert.Equal("938 characters", SpendBreakdownModel.UsedText(Voice(1, 0.06m)));
        Assert.Equal($"{3.4:0.0} s of audio", SpendBreakdownModel.UsedText(transcription));
        Assert.Null(SpendBreakdownModel.UsedDetail(transcription));
    }

    [Fact]
    public void AChargeReadsItsTimeStepModelAndCost()
    {
        var chapter = Model(0, 0.0123m, purpose: SpendPurposes.StoryChapter) with { Step = "fidelity" };

        Assert.Equal("9 Nov 12:00:00", SpendBreakdownModel.TimeText(chapter, TimeZoneInfo.Utc));
        Assert.Equal("FIDELITY", SpendBreakdownModel.StepWord(chapter));
        Assert.Null(SpendBreakdownModel.StepWord(Model(0, 0.1m)));
        Assert.Equal("VOICE", SpendBreakdownModel.StepWord(Voice(0, 0.1m)));
        Assert.Equal("claude-sonnet-5-5", SpendBreakdownModel.ModelText(chapter));
        Assert.NotNull(SpendBreakdownModel.ProviderLine(chapter));
        Assert.Null(SpendBreakdownModel.ProviderLine(Voice(0, 0.1m)));
        Assert.Equal(Money(0.0123m), SpendBreakdownModel.ChargeCostText(chapter));
        Assert.Equal("no rate", SpendBreakdownModel.ChargeCostText(Model(0, 0m, priced: false)));
    }
}
