using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A narration with a stock core aboard carries one unsaid Elite tip while Time_Played is under 50 hours.</summary>
public sealed class TheNarratorTeachesNewPlayersEliteTests
{
    private static readonly DateTimeOffset T0 = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(5);

    private const double Hour = 3600;

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException(json);

    private static CommanderGameState Played(double? seconds)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        if (seconds is { } played)
        {
            state.Apply(Event(
                $$$"""{"timestamp":"3312-05-01T11:00:00Z","event":"Statistics","Exploration":{"Time_Played":{{{played}}}}}"""));
        }

        return state;
    }

    private static NarratorCallout Narrator(HashSet<string> said, Func<NarratorTip?>? d47 = null) =>
        new(new NearbyFight())
        {
            StandInInterval = Gap,
            StandInLongest = Gap,
            StockCoreAboard = () => true,
            TakeTip = d47 ?? (() => null),
            TakeEliteTip = seen =>
            {
                var tip = EliteTips.Next(seen, said.Contains);

                if (tip is not null)
                {
                    said.Add(tip.CapabilityId);
                }

                return tip;
            },
        };

    private static Announcement Narrated(NarratorCallout narrator, CommanderGameState state, params string[] events)
    {
        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip };
        var context = new CalloutContext(T0, false, state, docked, NavRoute.None, []);
        _ = narrator.Examine(context).ToArray();

        var happened = events.Select(kind => Event($$"""{"timestamp":"3312-05-01T12:01:00Z","event":"{{kind}}"}""")).ToArray();
        _ = narrator.Examine(context with { Now = T0 + TimeSpan.FromSeconds(1), Events = happened }).ToArray();

        return Assert.Single(narrator.Examine(context with { Now = T0 + Gap + TimeSpan.FromSeconds(1) }));
    }

    [Fact]
    public void TenHoursInAfterAJumpTheNarrationCarriesTheFuelScoopTip()
    {
        var said = Narrated(Narrator([]), Played(10 * Hour), "FSDJump");
        var tip = Assert.IsType<NarratorTip>(said.Tip);
        var brief = FlavourBriefs.For(said, true)!;

        Assert.Equal("elite:fuel-scooping", tip.CapabilityId);
        Assert.Contains("fuel scoop", tip.Text, StringComparison.Ordinal);
        Assert.Contains(tip.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("playing Elite Dangerous", brief.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void SixtyHoursInNoEliteTipIsOffered() =>
        Assert.Null(Narrated(Narrator([]), Played(60 * Hour), "FSDJump").Tip);

    [Fact]
    public void BeforeAnyStatisticsEventNoEliteTipIsOffered() =>
        Assert.Null(Narrated(Narrator([]), Played(null), "FSDJump").Tip);

    [Fact]
    public void ATipAlreadySaidIsNotOfferedAgain()
    {
        var said = new HashSet<string> { "elite:fuel-scooping" };

        Assert.Equal("elite:discovery-scanner", Narrated(Narrator(said), Played(10 * Hour), "FSDJump").Tip?.CapabilityId);
    }

    [Fact]
    public void ATipWhoseEventHasNotHappenedIsNotOffered() =>
        Assert.Null(Narrated(Narrator([]), Played(10 * Hour)).Tip);

    [Fact]
    public void WithAD47TipAndAnEliteTipBothDueTheNarrationCarriesOnlyTheD47Tip()
    {
        var d47 = new NarratorTip("route", "Routes", "The ship plots a route if asked.");
        var said = new HashSet<string>();
        var narration = Narrated(Narrator(said, () => d47), Played(10 * Hour), "FSDJump");

        Assert.Same(d47, narration.Tip);
        Assert.Empty(said);
    }

    [Fact]
    public void EveryTipInTheListIsOneLineWithAKnownShape()
    {
        var seen = new HashSet<string>(["FSDJump", "FuelScoop", "FSDTarget", "SupercruiseEntry", "Undocked", "DockingGranted",
            "Docked", "HullDamage", "Resurrect", "Scan", "Interdicted", "CommitCrime", "MarketSell", "MissionAccepted",
            "MaterialCollected", "ShieldState", "CollectCargo", "JetConeBoost", "ModuleBuy"]);
        var offered = new List<string>();

        while (EliteTips.Next(seen, offered.Contains) is { } tip)
        {
            Assert.DoesNotContain('\t', tip.Text);
            offered.Add(tip.CapabilityId);
        }

        Assert.Equal(21, offered.Count);
    }

    [Fact]
    public void ASettingsFileWithoutTheKeyLoadsWithEliteTipsOn()
    {
        Assert.True(JsonSerializer.Deserialize<CalloutSettings>("{}")!.NarratorEliteTips);
        Assert.True(new D47Settings().Callouts.NarratorEliteTips);
    }
}
