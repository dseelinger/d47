using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The domain remark: a figure in the subject the core aboard pays attention to (#611).</summary>
public class ACoreRemarksOnItsOwnSubjectTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException(json);

    private static JournalEvent LoadGame(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1000 }""");

    private static JournalEvent Sell(DateTimeOffset at, long total) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":2, "Type":"gold", "Count":10, "SellPrice":{{total / 10}}, "TotalSale":{{total}}, "AvgPricePaid":1 }""");

    private static JournalEvent Mission(DateTimeOffset at, long reward) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MissionCompleted", "MissionID":7, "Name":"Mission_Courier", "Reward":{{reward}} }""");

    private static JournalEvent Bounty(DateTimeOffset at, long reward) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"Bounty", "Target":"cobramkiii", "TotalReward":{{reward}}, "VictimFaction":"Pirates" }""");

    private static JournalEvent Jump(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"FSDJump", "StarSystem":"Sol", "JumpDist":8.5 }""");

    private static JournalEvent ExplorationSale(DateTimeOffset at, long total) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"SellExplorationData", "TotalEarnings":{{total}}, "BaseValue":{{total}}, "Bonus":0 }""");

    private static JournalEvent Repair(DateTimeOffset at, long cost) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"RepairAll", "Cost":{{cost}} }""");

    private static JournalEvent ArrivalStar(DateTimeOffset at, long system, bool discovered = false) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"Scan", "ScanType":"AutoScan", "BodyName":"S{{system}}", "BodyID":0, "StarSystem":"S{{system}}", "SystemAddress":{{system}}, "StarType":"G", "DistanceFromArrivalLS":0.0, "WasDiscovered":{{(discovered ? "true" : "false")}}, "WasMapped":false }""");

    private static JournalEvent PlanetScan(DateTimeOffset at, long system, int body, bool mapped) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"Scan", "ScanType":"Detailed", "BodyName":"P{{body}}", "BodyID":{{body}}, "SystemAddress":{{system}}, "PlanetClass":"Icy body", "DistanceFromArrivalLS":100.0, "WasDiscovered":true, "WasMapped":{{(mapped ? "true" : "false")}}, "WasFootfalled":false }""");

    private static JournalEvent Mapped(DateTimeOffset at, long system, int body) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"SAAScanComplete", "BodyName":"P{{body}}", "BodyID":{{body}}, "SystemAddress":{{system}}, "ProbesUsed":5, "EfficiencyTarget":6 }""");

    private static JournalEvent Disembark(DateTimeOffset at, long system, int body) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"Disembark", "SRV":false, "Taxi":false, "Multicrew":false, "StarSystem":"S{{system}}", "SystemAddress":{{system}}, "Body":"P{{body}}", "BodyID":{{body}}, "OnStation":false, "OnPlanet":true }""");

    /// <summary>A session under way, and a callout that ticks against it the way the host runs it.</summary>
    private sealed class Session(PersonaDomain domain, bool enabled = true)
    {
        private readonly CommanderGameState _state = new(new CommanderIdentity(Fid, "Doug"));

        public DomainCallout Callout { get; } = new()
        {
            Domain = () => domain,
            Enabled = () => enabled,
        };

        public IReadOnlyList<Announcement> Tick(DateTimeOffset now, params JournalEvent[] events) =>
            Tick(now, priming: false, events);

        public IReadOnlyList<Announcement> Tick(DateTimeOffset now, bool priming, params JournalEvent[] events)
        {
            foreach (var journalEvent in events)
            {
                _state.Apply(journalEvent);
            }

            return [.. Callout.Examine(new CalloutContext(now, priming, _state, new GameStatus(), new NavRoute(), events))];
        }
    }

    private static Session Started(PersonaDomain domain, bool enabled = true)
    {
        var session = new Session(domain, enabled);
        session.Tick(Noon, LoadGame(Noon));
        return session;
    }

    [Fact]
    public void QuartermasterSaysTheRateAndItsLargestSourceAfterASale()
    {
        var session = Started(PersonaCatalog.Quartermaster.Domain);
        var at = Noon.AddMinutes(30);

        session.Tick(at.AddMinutes(-10), Mission(at.AddMinutes(-10), 500_000));
        var said = Assert.Single(session.Tick(at, Sell(at, 1_500_000)));

        Assert.Equal(DomainCallout.EarningsKey, said.Key);
        Assert.Equal("4 million credits an hour this session, the largest share from trade.", said.Text);
    }

    [Fact]
    public void SentinelSaysTheCombatRateAndItsLargestSource()
    {
        Assert.Equal(PersonaDomain.Combat, PersonaCatalog.Sentinel.Domain);

        var session = Started(PersonaCatalog.Sentinel.Domain);
        var at = Noon.AddMinutes(30);

        session.Tick(at.AddMinutes(-10), Sell(at.AddMinutes(-10), 5_000_000));
        var said = Assert.Single(session.Tick(at, Bounty(at, 800_000)));

        Assert.Equal(DomainCallout.CombatKey, said.Key);
        Assert.Equal("1.6 million credits an hour in combat this session, the largest share from bounties.", said.Text);
    }

    [Fact]
    public void ASaleDoesNotPromptSentinelAndABountyDoesNotPromptQuartermaster()
    {
        var sentinel = Started(PersonaDomain.Combat);
        var quartermaster = Started(PersonaDomain.Earnings);
        var at = Noon.AddHours(1);

        sentinel.Tick(at.AddMinutes(-5), Bounty(at.AddMinutes(-5), 800_000));

        Assert.Empty(sentinel.Tick(at, Sell(at, 2_000_000)));
        Assert.Empty(quartermaster.Tick(at, Bounty(at, 800_000)));
    }

    [Fact]
    public void ChartSaysTheExplorationRateAfterASale()
    {
        Assert.Equal(PersonaDomain.Exploration, PersonaCatalog.Cartographer.Domain);

        var session = Started(PersonaCatalog.Cartographer.Domain);
        var at = Noon.AddMinutes(30);

        var said = Assert.Single(session.Tick(at, ExplorationSale(at, 2_000_000)));

        Assert.Equal(DomainCallout.ExplorationKey, said.Key);
        Assert.Equal("4 million credits an hour from exploration data this session.", said.Text);
    }

    [Fact]
    public void ASaleOfCommoditiesDoesNotPromptChart()
    {
        var session = Started(PersonaDomain.Exploration);
        var at = Noon.AddHours(1);

        Assert.Empty(session.Tick(at, Sell(at, 2_000_000)));
    }

    [Fact]
    public void MenderSaysWhatRepairsCostOncePastTheThreshold()
    {
        Assert.Equal(PersonaDomain.Repairs, PersonaCatalog.Mender.Domain);

        var session = Started(PersonaCatalog.Mender.Domain);

        Assert.Empty(session.Tick(Noon.AddMinutes(5), Repair(Noon.AddMinutes(5), 60_000)));

        var at = Noon.AddMinutes(10);
        var said = Assert.Single(session.Tick(at, Repair(at, 90_000)));

        Assert.Equal(DomainCallout.RepairsKey, said.Key);
        Assert.Equal("150,000 credits on repairs this session.", said.Text);

        var soon = at.AddMinutes(30);
        Assert.Empty(session.Tick(soon, Repair(soon, 90_000)));
    }

    [Fact]
    public void ArchivistSaysTheCountOnTheThirdFirst()
    {
        var session = Started(PersonaDomain.Firsts);

        session.Tick(Noon.AddMinutes(1), ArrivalStar(Noon.AddMinutes(1), 1));
        session.Tick(Noon.AddMinutes(2), ArrivalStar(Noon.AddMinutes(2), 2));
        session.Tick(Noon.AddMinutes(3), PlanetScan(Noon.AddMinutes(3), 2, 5, mapped: false));

        var said = Assert.Single(session.Tick(Noon.AddMinutes(4), Mapped(Noon.AddMinutes(4), 2, 5)));

        Assert.Equal(DomainCallout.FirstsKey, said.Key);
        Assert.Equal("3 firsts this session: 2 undiscovered stars, 1 body mapped first.", said.Text);

        Assert.Empty(session.Tick(Noon.AddMinutes(20), ArrivalStar(Noon.AddMinutes(20), 4)));
    }

    [Fact]
    public void ArchivistCountsAFirstFootfallButNotAStarOrBodyThatWasAlreadyKnown()
    {
        var session = Started(PersonaDomain.Firsts);

        session.Tick(Noon.AddMinutes(1), ArrivalStar(Noon.AddMinutes(1), 1, discovered: true));
        session.Tick(Noon.AddMinutes(2), PlanetScan(Noon.AddMinutes(2), 2, 5, mapped: true));
        session.Tick(Noon.AddMinutes(3), Mapped(Noon.AddMinutes(3), 2, 5));
        session.Tick(Noon.AddMinutes(4), PlanetScan(Noon.AddMinutes(4), 2, 6, mapped: true));
        session.Tick(Noon.AddMinutes(5), ArrivalStar(Noon.AddMinutes(5), 3));
        session.Tick(Noon.AddMinutes(6), ArrivalStar(Noon.AddMinutes(6), 4));

        var said = Assert.Single(session.Tick(Noon.AddMinutes(8), Disembark(Noon.AddMinutes(8), 2, 6)));

        Assert.Equal("3 firsts this session: 2 undiscovered stars, 1 first footfall.", said.Text);
    }

    [Fact]
    public void ANewSessionRestartsTheCountOfFirsts()
    {
        var session = Started(PersonaDomain.Firsts);

        session.Tick(Noon.AddMinutes(1), ArrivalStar(Noon.AddMinutes(1), 1));
        session.Tick(Noon.AddMinutes(2), ArrivalStar(Noon.AddMinutes(2), 2));

        var later = Noon.AddHours(2);
        session.Tick(later, LoadGame(later));

        Assert.Empty(session.Tick(later.AddMinutes(1), ArrivalStar(later.AddMinutes(1), 3)));
    }

    [Fact]
    public void WardenSaysNothing()
    {
        Assert.Equal(PersonaDomain.None, PersonaCatalog.Warden.Domain);

        var session = Started(PersonaCatalog.Warden.Domain);
        var at = Noon.AddHours(2);

        Assert.Empty(session.Tick(at, Sell(at, 9_000_000)));
    }

    [Fact]
    public void ACoreTheCommanderWroteSaysNothing()
    {
        var own = new OwnPersona("mine", "Mine", "A core of my own.").AsPersona();

        Assert.Equal(PersonaDomain.None, own.Domain);

        var session = Started(own.Domain);
        var at = Noon.AddHours(2);

        Assert.Empty(session.Tick(at, Sell(at, 9_000_000)));
    }

    [Fact]
    public void ASessionUnderHalfAnHourSaysNothing()
    {
        var session = Started(PersonaDomain.Earnings);
        var at = Noon.AddMinutes(29);

        Assert.Empty(session.Tick(at, Sell(at, 9_000_000)));
    }

    [Fact]
    public void OnlyAnEarningEventPromptsIt()
    {
        var session = Started(PersonaDomain.Earnings);

        session.Tick(Noon.AddMinutes(10), Sell(Noon.AddMinutes(10), 1_000_000));

        Assert.Empty(session.Tick(Noon.AddMinutes(40), Jump(Noon.AddMinutes(40))));
    }

    [Fact]
    public void ItSpeaksAtMostOnceAnHourAndRecallsTheLastRate()
    {
        var session = Started(PersonaDomain.Earnings);

        var first = Noon.AddHours(1);
        Assert.Single(session.Tick(first, Sell(first, 2_000_000)));

        var soon = first.AddMinutes(59);
        Assert.Empty(session.Tick(soon, Sell(soon, 2_000_000)));

        var later = first.AddHours(1);
        var said = Assert.Single(session.Tick(later, Sell(later, 2_000_000)));

        Assert.Equal(
            "3 million credits an hour this session, the largest share from trade. "
            + "At the last count it was 2 million an hour.",
            said.Text);
    }

    [Fact]
    public void ANewSessionForgetsTheLastRate()
    {
        var session = Started(PersonaDomain.Earnings);

        var first = Noon.AddHours(1);
        Assert.Single(session.Tick(first, Sell(first, 2_000_000)));

        var restart = first.AddMinutes(5);
        session.Tick(restart, LoadGame(restart));

        var at = restart.AddHours(1);
        var said = Assert.Single(session.Tick(at, Sell(at, 1_000_000)));

        Assert.DoesNotContain("last count", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRemarkCarriesAFigure()
    {
        var session = Started(PersonaDomain.Earnings);
        var at = Noon.AddMinutes(45);

        var said = Assert.Single(session.Tick(at, Mission(at, 120_000)));

        Assert.Contains(said.Text, char.IsDigit);
    }

    [Fact]
    public void ThePrimingReplaySaysNothing()
    {
        var session = Started(PersonaDomain.Earnings);
        var at = Noon.AddHours(2);

        Assert.Empty(session.Tick(at, priming: true, Sell(at, 9_000_000)));
    }

    [Fact]
    public void TheSettingOffSilencesIt()
    {
        var session = Started(PersonaDomain.Earnings, enabled: false);
        var at = Noon.AddHours(2);

        Assert.Empty(session.Tick(at, Sell(at, 9_000_000)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ChoosingACoreNeverSwitchesItOn()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var rows = surface.Registry.All.SelectMany(capability => capability.Descriptor.Settings).ToList();

        var persona = rows.Single(row => row.Key == PersonaCapability.PersonaKey);
        var domain = rows.Single(row => row.Key == CalloutCapability.DomainKey);

        var off = domain.Binding!.Write!(D47Settings.Defaults, "false")!;
        var chosen = persona.Binding!.Write!(off, PersonaCatalog.Quartermaster.Id)!;

        Assert.Equal(PersonaCatalog.Quartermaster.Id, chosen.Persona.Id);
        Assert.False(chosen.Callouts.Domain);
    }

    [Trait("Category", "Integration")]
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
        Assert.Equal("true", row.Binding!.Read(D47Settings.Defaults));
    }

    [Fact]
    public void TheRemarkIsAlwaysSaidInTheCoresVoiceKeepingItsFigures()
    {
        var remark = new Announcement(DomainCallout.EarningsKey, "4 million credits an hour this session, the largest share from trade.");

        Assert.True(new RewordChance(new Random(1)).ShouldReword(remark, rewordPercent: 0));

        var brief = FlavourBriefs.For(remark, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.True(brief.NeedsPersona);
        Assert.Contains(remark.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("Keep every figure exactly", brief.Instruction, StringComparison.Ordinal);
        Assert.Null(FlavourBriefs.For(remark, personalityEnabled: false));
    }
}
