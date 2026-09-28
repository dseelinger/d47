using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The session's rate in the core's domain: credits an hour, the largest source (#613).</summary>
public class TheCoreRemarksOnItsDomainsRateTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException(json);

    private static JournalEvent LoadGame(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1000 }""");

    private static JournalEvent Bounty(DateTimeOffset at, long reward) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"Bounty", "Target":"eagle", "TotalReward":{{reward}}, "VictimFaction":"Somebody" }""");

    private static JournalEvent Bond(DateTimeOffset at, long reward) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"FactionKillBond", "Reward":{{reward}}, "AwardingFaction":"Lavigny's Legion", "VictimFaction":"Arakang Purple Drug Empire" }""");

    private static JournalEvent Voucher(DateTimeOffset at, long amount) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"RedeemVoucher", "Type":"bounty", "Amount":{{amount}} }""");

    /// <summary>Folds the events into the state, then hands them to the callout as one tick.</summary>
    private static List<Announcement> Tick(
        CommanderGameState state,
        DomainCallout callout,
        DateTimeOffset now,
        IReadOnlyList<JournalEvent> events,
        bool priming = false)
    {
        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        var context = new CalloutContext(now, priming, state, new GameStatus(), new NavRoute(), events);

        return [.. callout.Examine(context)];
    }

    private static CommanderGameState State() => new(new CommanderIdentity(Fid, "Doug"));

    private static DomainCallout Sentinel() => new(() => PersonaCatalog.Sentinel);

    [Fact]
    public void SentinelSaysTheCombatRateOnceTheSessionIsHalfAnHourOld()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        var said = Assert.Single(Tick(state, callout, Noon.AddMinutes(40), [Bounty(Noon.AddMinutes(40), 400_000)]));

        Assert.Equal("domain.combat", said.Key);
        Assert.Equal("600,000 credits an hour this session. Most of it from bounties.", said.Text);
    }

    [Fact]
    public void ASessionYoungerThanHalfAnHourSaysNothing()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        Assert.Empty(Tick(state, callout, Noon.AddMinutes(10), [Bounty(Noon.AddMinutes(10), 400_000)]));
    }

    [Fact]
    public void WardenHasNoDomainAndSaysNothing()
    {
        var state = State();
        var callout = new DomainCallout(() => PersonaCatalog.Warden);

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        Assert.Equal(PersonaDomain.None, PersonaCatalog.Warden.Domain);
        Assert.Empty(Tick(state, callout, Noon.AddMinutes(40), [Bounty(Noon.AddMinutes(40), 400_000)]));
    }

    [Fact]
    public void ACoreTheCommanderWroteHasNoDomainAndSaysNothing()
    {
        var own = new D47.Core.Persona.Persona(
            "mine", "Mine", "tag", "body", "intro", "return", new VoiceHint("a voice"));

        Assert.Equal(PersonaDomain.None, own.Domain);

        var state = State();
        var callout = new DomainCallout(() => own);

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        Assert.Empty(Tick(state, callout, Noon.AddMinutes(40), [Bounty(Noon.AddMinutes(40), 400_000)]));
    }

    [Fact]
    public void TheLargestSourceIsNamed()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        var at = Noon.AddMinutes(40);
        var said = Assert.Single(Tick(state, callout, at, [Bounty(at, 100_000), Bond(at, 300_000)]));

        Assert.Equal("600,000 credits an hour this session. Most of it from combat bonds.", said.Text);
    }

    [Fact]
    public void AVoucherPaysIntoTheCombatDomain()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        var at = Noon.AddMinutes(40);
        var said = Assert.Single(Tick(state, callout, at, [Voucher(at, 400_000)]));

        Assert.Equal("domain.combat", said.Key);
    }

    [Fact]
    public void AMoneyEventDoesNotWakeCombat()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        var at = Noon.AddMinutes(40);
        var sale = Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "Type":"palladium", "Count":1, "SellPrice":400000, "TotalSale":400000, "AvgPricePaid":0 }""");

        Assert.Empty(Tick(state, callout, at, [sale]));
    }

    [Fact]
    public void TheRateIsSaidAtMostOnceAnHour()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);

        Assert.Single(Tick(state, callout, Noon.AddMinutes(40), [Bounty(Noon.AddMinutes(40), 400_000)]));

        Assert.Empty(Tick(state, callout, Noon.AddMinutes(50), [Bounty(Noon.AddMinutes(50), 100_000)]));
    }

    [Fact]
    public void TheSecondRemarkCarriesTheRateLastSaid()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);
        Tick(state, callout, Noon.AddMinutes(40), [Bounty(Noon.AddMinutes(40), 400_000)]);

        var later = Noon.AddMinutes(105);
        var said = Assert.Single(Tick(state, callout, later, [Bounty(later, 200_000)]));

        Assert.Contains("Last time I said 600,000.", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingTheGameForgetsTheRateLastSaid()
    {
        var state = State();
        var callout = Sentinel();

        Tick(state, callout, Noon, [LoadGame(Noon)]);
        Tick(state, callout, Noon.AddMinutes(40), [Bounty(Noon.AddMinutes(40), 400_000)]);

        var fresh = Noon.AddHours(2);
        Tick(state, callout, fresh, [LoadGame(fresh)]);

        var at = fresh.AddMinutes(40);
        var said = Assert.Single(Tick(state, callout, at, [Bounty(at, 400_000)]));

        Assert.DoesNotContain("Last time", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidDuringPriming()
    {
        var state = State();
        var callout = Sentinel();

        Assert.Empty(Tick(state, callout, Noon, [LoadGame(Noon)], priming: true));
        Assert.Empty(Tick(
            state,
            callout,
            Noon.AddMinutes(40),
            [Bounty(Noon.AddMinutes(40), 400_000)],
            priming: true));

        // The first live remark still has no figure before it.
        var said = Assert.Single(Tick(state, callout, Noon.AddMinutes(41), [Bounty(Noon.AddMinutes(41), 1_000)]));

        Assert.DoesNotContain("Last time", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSettingOffSilencesItThroughTheEngine()
    {
        var state = State();
        var callout = Sentinel();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);

        state.Apply(LoadGame(Noon));

        var at = Noon.AddMinutes(40);
        var bounty = Bounty(at, 400_000);
        state.Apply(bounty);

        var context = new CalloutContext(at, false, state, new GameStatus(), new NavRoute(), [bounty]);

        engine.SetEnabled(callout.Id, false);
        engine.Tick(context);
        Assert.Empty(engine.Drain());

        engine.SetEnabled(callout.Id, true);
        engine.Tick(context);
        Assert.Single(engine.Drain());
    }

    [Fact]
    public void ARelogRestartsTheRemarkRhythmEvenInsideTheEngineCooldownWindow()
    {
        var state = State();
        var callout = Sentinel();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);

        state.Apply(LoadGame(Noon));
        var first = Noon.AddMinutes(40);
        var firstBounty = Bounty(first, 400_000);
        state.Apply(firstBounty);
        engine.Tick(new CalloutContext(first, false, state, new GameStatus(), new NavRoute(), [firstBounty]));
        Assert.Single(engine.Drain());

        // The relog lands 45 minutes after that remark: inside what would have been a one-hour engine
        // cooldown, but after the callout reset its own clock on LoadGame.
        var reload = Noon.AddMinutes(45);
        var load = LoadGame(reload);
        state.Apply(load);
        engine.Tick(new CalloutContext(reload, false, state, new GameStatus(), new NavRoute(), [load]));
        Assert.Empty(engine.Drain());

        var second = reload.AddMinutes(40);
        var secondBounty = Bounty(second, 400_000);
        state.Apply(secondBounty);
        engine.Tick(new CalloutContext(second, false, state, new GameStatus(), new NavRoute(), [secondBounty]));

        var said = Assert.Single(engine.Drain());
        Assert.DoesNotContain("Last time", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.DomainKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(row.Protected);
        Assert.True(new CalloutSettings().Domain);
        Assert.Equal("true", row.Binding!.Read(D47Settings.Defaults));

        var off = row.Binding!.Write!(D47Settings.Defaults, "false");

        Assert.False(off!.Callouts.Domain);
    }

    [Fact]
    public void TheDomainRemarkIsACoreBriefThatKeepsEveryFigure()
    {
        var brief = FlavourBriefs.For(
            new Announcement(
                "domain.combat",
                "600,000 credits an hour this session. Most of it from bounties."),
            personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.True(brief.NeedsPersona);
        Assert.Contains("600,000 credits an hour this session.", brief.Instruction, StringComparison.Ordinal);
    }
}
