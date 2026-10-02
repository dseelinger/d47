using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Help;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A narration with a stock core aboard carries one unsaid tip on using D47, once per Commander.</summary>
public sealed class TheNarratorTeachesD47OnceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(5);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"d47-tips-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    private NarratorTipStore Store() => new(_path, NullLogger<NarratorTipStore>.Instance);

    private static NarratorCallout Narrator(bool stock, Func<NarratorTip?> tip) =>
        new(new NearbyFight())
        {
            StandInInterval = Gap,
            StandInLongest = Gap,
            StockCoreAboard = () => stock,
            TakeTip = tip,
        };

    private static Announcement Narrated(NarratorCallout narrator)
    {
        var context = new CalloutContext(
            T0, false, null, GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip }, NavRoute.None, []);
        _ = narrator.Examine(context).ToArray();

        return Assert.Single(narrator.Examine(context with { Now = T0 + Gap + TimeSpan.FromSeconds(1) }));
    }

    private static NarratorTip Tip(Func<string, bool> said) =>
        NarratorTips.Next(said) ?? throw new InvalidOperationException("no help page offers a tip");

    [Fact]
    public void WithCovasAboardANarrationCarriesTheFirstUnsaidTipAndTheStoreRecordsIt()
    {
        var store = Store();
        var narrator = Narrator(true, () =>
        {
            var tip = NarratorTips.Next(id => store.Said("F1", id));

            if (tip is not null)
            {
                store.Record("F1", tip.CapabilityId);
            }

            return tip;
        });

        var said = Narrated(narrator);
        var tip = Assert.IsType<NarratorTip>(said.Tip);
        var brief = FlavourBriefs.For(said, true)!;

        Assert.Equal(HelpLibrary.Pages.First(id => HelpLibrary.For(id)?.Intro.Length > 0), tip.CapabilityId);
        Assert.Contains(tip.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("had not yet learned", brief.Instruction, StringComparison.Ordinal);
        Assert.True(Store().Said("F1", tip.CapabilityId));
    }

    [Fact]
    public void ATipSaidToACommanderIsNotOfferedAgainButAnotherCommanderStartsWithNone()
    {
        var store = Store();
        var first = Tip(id => store.Said("F1", id));
        store.Record("F1", first.CapabilityId);

        var reloaded = Store();
        var second = Tip(id => reloaded.Said("F1", id));

        Assert.NotEqual(first.CapabilityId, second.CapabilityId);
        Assert.Equal(first.CapabilityId, Tip(id => reloaded.Said("F2", id)).CapabilityId);
    }

    [Fact]
    public void WithAnotherCoreAboardNoNarrationCarriesATip()
    {
        var narrator = Narrator(false, () => Tip(_ => false));
        narrator.HasStory = () => true;
        narrator.Interval = Gap;
        narrator.Longest = Gap;

        Assert.Null(Narrated(narrator).Tip);
    }

    [Fact]
    public void ANarrationWithNoTipKeepsTheBaseBrief()
    {
        var said = Narrated(Narrator(true, () => null));

        Assert.Null(said.Tip);
        Assert.Same(FlavourBriefs.Narration, FlavourBriefs.For(said, true));
    }

    [Fact]
    public void ASettingsFileWithoutTheKeyLoadsWithTipsOn()
    {
        Assert.True(JsonSerializer.Deserialize<CalloutSettings>("{}")!.NarratorD47Tips);
        Assert.True(new D47Settings().Callouts.NarratorD47Tips);
    }
}
