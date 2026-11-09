using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Meeting a Commander and <see cref="GameStateStore.RestoreLate"/> read each restore hook as the table in
/// the store says: which hooks Admit reads, which RestoreLate reads, what makes a value usable and which
/// side wins a merge.
/// </summary>
public class TheRestoreTableDrivesAdmitAndRestoreLateTests
{
    private const string Fid = "F1";

    private static readonly CommanderIdentity Commander = new(Fid, "Cmdr");
    private static readonly DateTimeOffset T1 = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(10);
    private static readonly DateTimeOffset T3 = T1.AddMinutes(20);

    // OrganicSampling: Admit only

    [Fact]
    public void AnOrganicSamplingRestoreIsTakenWhenACommanderIsMet()
    {
        var sampling = new OrganicSampling();
        string? asked = null;
        var store = new GameStateStore { Restore = fid => { asked = fid; return sampling; } };

        var state = Met(store);

        Assert.Equal(Fid, asked);
        Assert.Same(sampling, state.Sampling);
    }

    [Fact]
    public void NoOrganicSamplingRestoreLeavesTheDefault()
    {
        var store = new GameStateStore { Restore = _ => null };

        Assert.Same(OrganicSampling.Empty, Met(store).Sampling);
    }

    [Fact]
    public void RestoreLateNeverReadsTheOrganicSamplingHook()
    {
        var reads = 0;
        var store = new GameStateStore { Restore = _ => { reads++; return new OrganicSampling(); } };
        _ = Met(store);
        reads = 0;

        store.RestoreLate();

        Assert.Equal(0, reads);
    }

    // FleetRegistry

    [Fact]
    public void AKnownFleetIsTakenWhenACommanderIsMet()
    {
        var fleet = KnownFleet("Sol");
        var store = new GameStateStore { RestoreFleet = _ => fleet };

        Assert.Same(fleet, Met(store).Fleet);
    }

    [Fact]
    public void AnUnknownFleetIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreFleet = _ => FleetRegistry.Empty with { SnapshotSystem = "Sol" } };

        Assert.Same(FleetRegistry.Empty, Met(store).Fleet);
    }

    [Fact]
    public void AKnownFleetFillsAnUnknownLiveFleetOnRestoreLate()
    {
        FleetRegistry? restored = null;
        var store = new GameStateStore { RestoreFleet = _ => restored };
        var state = Met(store);
        restored = KnownFleet("Sol");

        store.RestoreLate();

        Assert.Same(restored, state.Fleet);
    }

    [Fact]
    public void AKnownLiveFleetIsKeptOnRestoreLate()
    {
        FleetRegistry? restored = null;
        var store = new GameStateStore { RestoreFleet = _ => restored };
        var state = Met(store);
        var live = KnownFleet("Colonia");
        state.Fleet = live;
        restored = KnownFleet("Sol");

        store.RestoreLate();

        Assert.Same(live, state.Fleet);
    }

    [Fact]
    public void AnUnknownFleetIsIgnoredOnRestoreLate()
    {
        FleetRegistry? restored = null;
        var store = new GameStateStore { RestoreFleet = _ => restored };
        var state = Met(store);
        restored = FleetRegistry.Empty with { SnapshotSystem = "Sol" };

        store.RestoreLate();

        Assert.Same(FleetRegistry.Empty, state.Fleet);
    }

    // JournalLocation: RestoreLate only

    [Fact]
    public void ALocationRestoreIsNotReadWhenACommanderIsMet()
    {
        var reads = 0;
        var store = new GameStateStore { RestoreLocation = _ => { reads++; return Here("Sol"); } };

        var state = Met(store);

        Assert.Equal(0, reads);
        Assert.Null(state.Location.StarSystem);
    }

    [Fact]
    public void ARestoredLocationFillsAnEmptyLiveLocationOnRestoreLate()
    {
        JournalLocation? restored = null;
        var store = new GameStateStore { RestoreLocation = _ => restored };
        var state = Met(store);
        restored = Here("Sol");

        store.RestoreLate();

        Assert.Same(restored, state.Location);
    }

    [Fact]
    public void ALiveLocationIsKeptOnRestoreLate()
    {
        JournalLocation? restored = null;
        var store = new GameStateStore { RestoreLocation = _ => restored };
        var state = Met(store);
        var live = Here("Colonia");
        state.Location = live;
        restored = Here("Sol");

        store.RestoreLate();

        Assert.Same(live, state.Location);
    }

    [Fact]
    public void ARestoredLocationWithNoSystemIsIgnoredOnRestoreLate()
    {
        JournalLocation? restored = null;
        var store = new GameStateStore { RestoreLocation = _ => restored };
        var state = Met(store);
        restored = new JournalLocation(null, "Body", false, null);

        store.RestoreLate();

        Assert.Same(JournalLocation.Unknown, state.Location);
    }

    // ShipLoadouts

    [Fact]
    public void KnownLoadoutsAreTakenWhenACommanderIsMet()
    {
        var loadouts = Ships(7, "Python");
        var store = new GameStateStore { RestoreLoadouts = _ => loadouts };

        Assert.Same(loadouts, Met(store).Loadouts);
    }

    [Fact]
    public void UnknownLoadoutsAreIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreLoadouts = _ => ShipLoadouts.NoShips };

        Assert.Same(ShipLoadouts.Empty, Met(store).Loadouts);
    }

    [Fact]
    public void RestoredLoadoutsMergeUnderLiveOnesOnRestoreLate()
    {
        ShipLoadouts? restored = null;
        var store = new GameStateStore { RestoreLoadouts = _ => restored };
        var state = Met(store);
        state.Loadouts = Ships(9, "Anaconda");
        restored = Ships(7, "Python");

        store.RestoreLate();

        Assert.Equal("Python", state.Loadouts.Ships[7].Loadout.Type);
        Assert.Equal("Anaconda", state.Loadouts.Ships[9].Loadout.Type);
    }

    [Fact]
    public void UnknownLoadoutsAreIgnoredOnRestoreLate()
    {
        ShipLoadouts? restored = null;
        var store = new GameStateStore { RestoreLoadouts = _ => restored };
        var state = Met(store);
        var live = Ships(9, "Anaconda");
        state.Loadouts = live;
        restored = ShipLoadouts.NoShips;

        store.RestoreLate();

        Assert.Same(live, state.Loadouts);
    }

    // OwnedKit

    [Fact]
    public void AKnownKitIsTakenWhenACommanderIsMet()
    {
        var kit = Suit(1);
        var store = new GameStateStore { RestoreKit = _ => kit };

        Assert.Same(kit, Met(store).Kit);
    }

    [Fact]
    public void AnUnknownKitIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreKit = _ => new OwnedKit() };

        Assert.Same(OwnedKit.Empty, Met(store).Kit);
    }

    [Fact]
    public void ARestoredKitMergesWithTheLiveOneOnRestoreLate()
    {
        OwnedKit? restored = null;
        var store = new GameStateStore { RestoreKit = _ => restored };
        var state = Met(store);
        state.Kit = Suit(2);
        restored = Suit(1);

        store.RestoreLate();

        Assert.Equal([1L, 2L], state.Kit.Suits.Keys.Order());
    }

    [Fact]
    public void AnUnknownKitIsIgnoredOnRestoreLate()
    {
        OwnedKit? restored = null;
        var store = new GameStateStore { RestoreKit = _ => restored };
        var state = Met(store);
        var live = Suit(2);
        state.Kit = live;
        restored = new OwnedKit();

        store.RestoreLate();

        Assert.Same(live, state.Kit);
    }

    // CarrierState

    [Fact]
    public void AKnownCarrierIsTakenWhenACommanderIsMet()
    {
        var carrier = OwnedCarrier("Meene");
        var store = new GameStateStore { RestoreCarrier = _ => carrier };

        Assert.Same(carrier, Met(store).Carrier);
    }

    [Fact]
    public void AnUnknownCarrierIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreCarrier = _ => CarrierState.None with { Name = "Nameless" } };

        Assert.Same(CarrierState.None, Met(store).Carrier);
    }

    [Fact]
    public void ARestoredCarrierKeepsTheLiveSystemOnRestoreLate()
    {
        CarrierState? restored = null;
        var store = new GameStateStore { RestoreCarrier = _ => restored };
        var state = Met(store);
        state.Carrier = CarrierState.None with { StarSystem = "Giryak" };
        restored = OwnedCarrier("Meene");

        store.RestoreLate();

        Assert.Equal("BNH-T2F", state.Carrier.CallSign);
        Assert.Equal("Sacred Fire", state.Carrier.Name);
        Assert.Equal("Giryak", state.Carrier.StarSystem);
    }

    [Fact]
    public void AnUnknownCarrierIsIgnoredOnRestoreLate()
    {
        CarrierState? restored = null;
        var store = new GameStateStore { RestoreCarrier = _ => restored };
        var state = Met(store);
        restored = CarrierState.None with { Name = "Nameless" };

        store.RestoreLate();

        Assert.Same(CarrierState.None, state.Carrier);
    }

    // MissionBoard

    [Fact]
    public void AKnownMissionBoardIsTakenWhenACommanderIsMet()
    {
        var board = Board(T1, new Mission(1, "Mission_A"));
        var store = new GameStateStore { RestoreMissions = _ => board };

        Assert.Same(board, Met(store).Missions);
    }

    [Fact]
    public void AnUnknownMissionBoardIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreMissions = _ => MissionBoard.Empty with { Missions = [new Mission(1, "Mission_A")] } };

        Assert.Same(MissionBoard.Empty, Met(store).Missions);
    }

    [Fact]
    public void ARestoredMissionBoardMergesWithTheLiveOneOnRestoreLate()
    {
        MissionBoard? restored = null;
        var store = new GameStateStore { RestoreMissions = _ => restored };
        var state = Met(store);
        state.Missions = Board(T2, new Mission(2, "Mission_B"));
        restored = Board(T1, new Mission(1, "Mission_A"));

        store.RestoreLate();

        Assert.Equal([1L, 2L], state.Missions.Missions.Select(mission => mission.Id).Order());
        Assert.Equal(T2, state.Missions.SeenAt);
    }

    [Fact]
    public void AnUnknownMissionBoardIsIgnoredOnRestoreLate()
    {
        MissionBoard? restored = null;
        var store = new GameStateStore { RestoreMissions = _ => restored };
        var state = Met(store);
        restored = MissionBoard.Empty with { Missions = [new Mission(1, "Mission_A")] };

        store.RestoreLate();

        Assert.Same(MissionBoard.Empty, state.Missions);
    }

    // ColonisationSites

    [Fact]
    public void KnownColonisationIsTakenWhenACommanderIsMet()
    {
        var sites = Claimed("Sol", T1);
        var store = new GameStateStore { RestoreColonisation = _ => sites };

        Assert.Same(sites, Met(store).Colonisation);
    }

    [Fact]
    public void UnknownColonisationIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreColonisation = _ => new ColonisationSites() };

        Assert.Same(ColonisationSites.Empty, Met(store).Colonisation);
    }

    [Fact]
    public void RestoredColonisationMergesWithTheLiveOneOnRestoreLate()
    {
        ColonisationSites? restored = null;
        var store = new GameStateStore { RestoreColonisation = _ => restored };
        var state = Met(store);
        state.Colonisation = Claimed("Colonia", T2);
        restored = Claimed("Sol", T1);

        store.RestoreLate();

        Assert.Equal(["Colonia", "Sol"], state.Colonisation.Claims.Select(claim => claim.StarSystem).Order());
    }

    [Fact]
    public void UnknownColonisationIsIgnoredOnRestoreLate()
    {
        ColonisationSites? restored = null;
        var store = new GameStateStore { RestoreColonisation = _ => restored };
        var state = Met(store);
        restored = new ColonisationSites();

        store.RestoreLate();

        Assert.Same(ColonisationSites.Empty, state.Colonisation);
    }

    // SpokenNames

    [Fact]
    public void KnownNamesAreTakenWhenACommanderIsMet()
    {
        var names = SpokenNames.Empty.With(["Sol"]);
        var store = new GameStateStore { RestoreNames = _ => names };

        Assert.Same(names, Met(store).Names);
    }

    [Fact]
    public void UnknownNamesAreIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreNames = _ => new SpokenNames() };

        Assert.Same(SpokenNames.Empty, Met(store).Names);
    }

    [Fact]
    public void RestoredNamesComeFirstAndKeepTheLiveOnesOnRestoreLate()
    {
        SpokenNames? restored = null;
        var store = new GameStateStore { RestoreNames = _ => restored };
        var state = Met(store);
        state.Names = SpokenNames.Empty.With(["Deciat"]);
        restored = SpokenNames.Empty.With(["Sol"]);

        store.RestoreLate();

        Assert.Equal(["Sol", "Deciat"], state.Names.Names);
    }

    [Fact]
    public void UnknownNamesAreIgnoredOnRestoreLate()
    {
        SpokenNames? restored = null;
        var store = new GameStateStore { RestoreNames = _ => restored };
        var state = Met(store);
        var live = SpokenNames.Empty.With(["Deciat"]);
        state.Names = live;
        restored = new SpokenNames();

        store.RestoreLate();

        Assert.Same(live, state.Names);
    }

    // PowerplayCycleMerits

    [Fact]
    public void KnownCycleMeritsAreTakenWhenACommanderIsMet()
    {
        var merits = Merits(new MeritGain(T1, 5));
        var store = new GameStateStore { RestoreCycleMerits = _ => merits };

        Assert.Same(merits, Met(store).CycleMerits);
    }

    [Fact]
    public void UnknownCycleMeritsAreIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreCycleMerits = _ => PowerplayCycleMerits.None with { Gains = [new MeritGain(T1, 5)] } };

        Assert.Same(PowerplayCycleMerits.None, Met(store).CycleMerits);
    }

    [Fact]
    public void RestoredCycleMeritsTakeTheLiveGainsOnTopOnRestoreLate()
    {
        PowerplayCycleMerits? restored = null;
        var store = new GameStateStore { RestoreCycleMerits = _ => restored };
        var state = Met(store);
        state.CycleMerits = Merits(new MeritGain(T2, 20), new MeritGain(T3, 30));
        restored = Merits(new MeritGain(T1, 10), new MeritGain(T2, 20));

        store.RestoreLate();

        Assert.Equal([T1, T2, T3], state.CycleMerits.Gains.Select(gain => gain.At));
    }

    [Fact]
    public void UnknownCycleMeritsAreIgnoredOnRestoreLate()
    {
        PowerplayCycleMerits? restored = null;
        var store = new GameStateStore { RestoreCycleMerits = _ => restored };
        var state = Met(store);
        restored = PowerplayCycleMerits.None with { Gains = [new MeritGain(T1, 5)] };

        store.RestoreLate();

        Assert.Same(PowerplayCycleMerits.None, state.CycleMerits);
    }

    // BgsWork

    [Fact]
    public void KnownBgsWorkIsTakenWhenACommanderIsMet()
    {
        var work = Work(Entry(T1));
        var store = new GameStateStore { RestoreBgsWork = _ => work };

        Assert.Same(work, Met(store).BgsWork);
    }

    [Fact]
    public void UnknownBgsWorkIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreBgsWork = _ => BgsWork.None with { Station = new BgsStation(1, 2, null) } };

        Assert.Same(BgsWork.None, Met(store).BgsWork);
    }

    [Fact]
    public void RestoredBgsWorkTakesTheLiveEntriesOnTopOnRestoreLate()
    {
        BgsWork? restored = null;
        var store = new GameStateStore { RestoreBgsWork = _ => restored };
        var state = Met(store);
        state.BgsWork = Work(Entry(T2), Entry(T3));
        restored = Work(Entry(T1), Entry(T2));

        store.RestoreLate();

        Assert.Equal([T1, T2, T3], state.BgsWork.Entries.Select(entry => entry.At));
    }

    [Fact]
    public void UnknownBgsWorkIsIgnoredOnRestoreLate()
    {
        BgsWork? restored = null;
        var store = new GameStateStore { RestoreBgsWork = _ => restored };
        var state = Met(store);
        restored = BgsWork.None with { Station = new BgsStation(1, 2, null) };

        store.RestoreLate();

        Assert.Same(BgsWork.None, state.BgsWork);
    }

    // SystemStandings

    private static SystemStandings Standing(string time, double influence) =>
        SystemStandings.Empty.Apply(Parsed(
            "{\"timestamp\":\"" + time + "Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":10477373803,"
            + "\"Factions\":[{\"Name\":\"Mother Gaia\",\"Influence\":" + influence.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}"));

    private static JournalEvent Parsed(string line)
    {
        Assert.True(JournalEvent.TryParse(line, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void KnownStandingsAreTakenWhenACommanderIsMet()
    {
        var standings = Standing("2026-09-05T10:00:00", 0.5);
        var store = new GameStateStore { RestoreStandings = _ => standings };

        Assert.Same(standings, Met(store).Standings);
    }

    [Fact]
    public void EmptyStandingsAreIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreStandings = _ => SystemStandings.Empty };

        Assert.Same(SystemStandings.Empty, Met(store).Standings);
    }

    [Fact]
    public void RestoredStandingsTakeTheLiveReadingOnTopOnRestoreLate()
    {
        SystemStandings? restored = null;
        var store = new GameStateStore { RestoreStandings = _ => restored };
        var state = Met(store);
        state.Standings = Standing("2026-09-06T10:00:00", 0.6);
        restored = Standing("2026-09-05T10:00:00", 0.5);

        store.RestoreLate();

        Assert.Equal(0.6, state.Standings.Latest("Sol")!.InfluenceOf("Mother Gaia"));
        Assert.Equal(0.5, state.Standings.Earlier("Sol")!.InfluenceOf("Mother Gaia"));
    }

    // UnlockEvidence

    [Fact]
    public void KnownEvidenceSetsReputationContributionsAndTalliesWhenACommanderIsMet()
    {
        var evidence = Evidence("Empire", 1, TallyEngineers[0]);
        var store = new GameStateStore { RestoreEvidence = _ => evidence };

        var state = Met(store);

        Assert.Same(evidence.Reputation, state.Reputation);
        Assert.Same(evidence.Contributions, state.Contributions);
        Assert.Same(evidence.Tallies, state.Tallies);
    }

    [Fact]
    public void UnknownEvidenceIsIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreEvidence = _ => UnlockEvidence.Empty };

        var state = Met(store);

        Assert.Same(ReputationState.Empty, state.Reputation);
        Assert.Same(EngineerContributions.Empty, state.Contributions);
        Assert.Same(EngineerTallies.Empty, state.Tallies);
    }

    [Fact]
    public void RestoredEvidenceMergesEachReadingWithTheLiveOneOnRestoreLate()
    {
        UnlockEvidence? restored = null;
        var store = new GameStateStore { RestoreEvidence = _ => restored };
        var state = Met(store);
        var live = Evidence("Federation", 2, TallyEngineers[1]);
        state.Reputation = live.Reputation;
        state.Contributions = live.Contributions;
        state.Tallies = live.Tallies;
        restored = Evidence("Empire", 1, TallyEngineers[0]);

        store.RestoreLate();

        Assert.NotNull(state.Reputation.Reading("Empire"));
        Assert.NotNull(state.Reputation.Reading("Federation"));
        Assert.NotNull(state.Contributions.Reading(1, "Commodity", "tritium"));
        Assert.NotNull(state.Contributions.Reading(2, "Commodity", "tritium"));
        Assert.NotNull(state.Tallies.For(TallyEngineers[0]));
        Assert.NotNull(state.Tallies.For(TallyEngineers[1]));
    }

    [Fact]
    public void UnknownEvidenceIsIgnoredOnRestoreLate()
    {
        UnlockEvidence? restored = null;
        var store = new GameStateStore { RestoreEvidence = _ => restored };
        var state = Met(store);
        restored = UnlockEvidence.Empty;

        store.RestoreLate();

        Assert.Same(ReputationState.Empty, state.Reputation);
        Assert.Same(EngineerContributions.Empty, state.Contributions);
    }

    // TechBrokerUnlocks

    [Fact]
    public void KnownTechBrokerUnlocksAreTakenWhenACommanderIsMet()
    {
        var unlocks = Unlocked(TechBrokerCatalogue.Joined[0]);
        var store = new GameStateStore { RestoreTechBroker = _ => unlocks };

        Assert.Same(unlocks, Met(store).TechBroker);
    }

    [Fact]
    public void UnknownTechBrokerUnlocksAreIgnoredWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreTechBroker = _ => new TechBrokerUnlocks() };

        Assert.Same(TechBrokerUnlocks.Empty, Met(store).TechBroker);
    }

    [Fact]
    public void RestoredTechBrokerUnlocksJoinTheLiveOnesOnRestoreLate()
    {
        TechBrokerUnlocks? restored = null;
        var store = new GameStateStore { RestoreTechBroker = _ => restored };
        var state = Met(store);
        state.TechBroker = Unlocked(TechBrokerCatalogue.Joined[1]);
        restored = Unlocked(TechBrokerCatalogue.Joined[0]);

        store.RestoreLate();

        Assert.True(state.TechBroker.IsKnownUnlocked(TechBrokerCatalogue.Joined[0]));
        Assert.True(state.TechBroker.IsKnownUnlocked(TechBrokerCatalogue.Joined[1]));
    }

    [Fact]
    public void UnknownTechBrokerUnlocksAreIgnoredOnRestoreLate()
    {
        TechBrokerUnlocks? restored = null;
        var store = new GameStateStore { RestoreTechBroker = _ => restored };
        var state = Met(store);
        var live = Unlocked(TechBrokerCatalogue.Joined[1]);
        state.TechBroker = live;
        restored = new TechBrokerUnlocks();

        store.RestoreLate();

        Assert.Same(live, state.TechBroker);
    }

    // CombatRecord, SurfaceMiningSites, CreditLedger, CodexLedger: merged whenever offered

    [Fact]
    public void AnEmptyCombatRecordIsStillMergedWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreCombat = _ => CombatRecord.None };

        Assert.True(Met(store).Combat.IsWhole);
    }

    [Fact]
    public void ACombatRecordFromNoHookIsLeftAlone()
    {
        var store = new GameStateStore { RestoreCombat = _ => null };

        Assert.Same(CombatRecord.None, Met(store).Combat);
    }

    [Fact]
    public void AnEmptyCombatRecordIsStillMergedOnRestoreLate()
    {
        CombatRecord? restored = null;
        var store = new GameStateStore { RestoreCombat = _ => restored };
        var state = Met(store);
        state.Combat = CombatRecord.None with { OnRecordSince = T2 };
        restored = CombatRecord.None with { OnRecordSince = T1 };

        store.RestoreLate();

        Assert.True(state.Combat.IsWhole);
        Assert.Equal(T1, state.Combat.OnRecordSince);
    }

    [Fact]
    public void AnEmptySurfaceMiningRecordIsStillMergedWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreSurfaceMining = _ => SurfaceMiningSites.None };

        Assert.True(Met(store).SurfaceMining.IsWhole);
    }

    [Fact]
    public void ASurfaceMiningRecordFromNoHookIsLeftAlone()
    {
        var store = new GameStateStore { RestoreSurfaceMining = _ => null };

        Assert.Same(SurfaceMiningSites.None, Met(store).SurfaceMining);
    }

    [Fact]
    public void AnEmptySurfaceMiningRecordIsStillMergedOnRestoreLate()
    {
        SurfaceMiningSites? restored = null;
        var store = new GameStateStore { RestoreSurfaceMining = _ => restored };
        var state = Met(store);
        restored = SurfaceMiningSites.None;

        store.RestoreLate();

        Assert.True(state.SurfaceMining.IsWhole);
    }

    [Fact]
    public void AnEmptyCreditLedgerIsStillMergedWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreCreditLedger = _ => CreditLedger.None };

        Assert.True(Met(store).CreditLedger.IsWhole);
    }

    [Fact]
    public void ACreditLedgerFromNoHookIsLeftAlone()
    {
        var store = new GameStateStore { RestoreCreditLedger = _ => null };

        Assert.Same(CreditLedger.None, Met(store).CreditLedger);
    }

    [Fact]
    public void AnEmptyCreditLedgerIsStillMergedOnRestoreLate()
    {
        CreditLedger? restored = null;
        var store = new GameStateStore { RestoreCreditLedger = _ => restored };
        var state = Met(store);
        state.CreditLedger = CreditLedger.None with { OnRecordSince = T2 };
        restored = CreditLedger.None with { OnRecordSince = T1 };

        store.RestoreLate();

        Assert.True(state.CreditLedger.IsWhole);
        Assert.Equal(T1, state.CreditLedger.OnRecordSince);
    }

    [Fact]
    public void AnEmptyCodexLedgerIsStillMergedWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreCodexLedger = _ => CodexLedger.None };

        Assert.True(Met(store).CodexLedger.IsWhole);
    }

    [Fact]
    public void ACodexLedgerFromNoHookIsLeftAlone()
    {
        var store = new GameStateStore { RestoreCodexLedger = _ => null };

        Assert.Same(CodexLedger.None, Met(store).CodexLedger);
    }

    [Fact]
    public void AnEmptyCodexLedgerIsStillMergedOnRestoreLate()
    {
        CodexLedger? restored = null;
        var store = new GameStateStore { RestoreCodexLedger = _ => restored };
        var state = Met(store);
        state.CodexLedger = CodexLedger.None with { OnRecordSince = T2 };
        restored = CodexLedger.None with { OnRecordSince = T1 };

        store.RestoreLate();

        Assert.True(state.CodexLedger.IsWhole);
        Assert.Equal(T1, state.CodexLedger.OnRecordSince);
    }

    // Credits

    [Fact]
    public void RestoredCreditsBecomeTheBalanceWhenACommanderIsMet()
    {
        var store = new GameStateStore { RestoreCredits = _ => 1000 };

        Assert.Equal(1000, Met(store).Session.Balance);
    }

    [Fact]
    public void NoRestoredCreditsLeaveTheBalanceUnknown()
    {
        var store = new GameStateStore { RestoreCredits = _ => null };

        Assert.Null(Met(store).Session.Balance);
    }

    [Fact]
    public void RestoredCreditsFillAnUnknownBalanceOnRestoreLate()
    {
        long? restored = null;
        var store = new GameStateStore { RestoreCredits = _ => restored };
        var state = Met(store);
        restored = 1000;

        store.RestoreLate();

        Assert.Equal(1000, state.Session.Balance);
    }

    [Fact]
    public void RestoredCreditsDoNotOverwriteALiveBalanceOnRestoreLate()
    {
        long? restored = null;
        var store = new GameStateStore { RestoreCredits = _ => restored };
        _ = Met(store);
        store.Apply(Event($$"""{"timestamp":"{{T1:O}}","event":"LoadGame","FID":"{{Fid}}","Commander":"Cmdr","Credits":500}"""));
        restored = 1000;

        store.RestoreLate();

        Assert.Equal(500, store.Active!.Session.Balance);
    }

    private static readonly int[] TallyEngineers =
    [
        .. EngineerDirectory.All
            .Where(engineer => new[] { engineer.MeetingTest, engineer.UnlockTest }.OfType<UnlockTest.Tally>().Any())
            .Select(engineer => engineer.Id)
            .Take(2),
    ];

    private static CommanderGameState Met(GameStateStore store)
    {
        store.Pick(Commander);
        return store.Active!;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static FleetRegistry KnownFleet(string system) =>
        FleetRegistry.Empty with { TakenAt = T1, SnapshotSystem = system };

    private static JournalLocation Here(string system) => new(system, null, false, null);

    private static ShipLoadouts Ships(int id, string type) => ShipLoadouts.Empty with
    {
        Ships = new Dictionary<int, RememberedShip>
        {
            [id] = new(new ShipLoadout { Type = type, ShipId = id }, T1),
        },
    };

    private static OwnedKit Suit(long id) => OwnedKit.Empty with
    {
        Suits = new Dictionary<long, OwnedSuit> { [id] = new(id, "explorationsuit", 1, [], T1) },
    };

    private static CarrierState OwnedCarrier(string system) =>
        CarrierState.None with { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = system };

    private static MissionBoard Board(DateTimeOffset seen, params Mission[] missions) =>
        MissionBoard.Empty with { SeenAt = seen, Missions = missions };

    private static ColonisationSites Claimed(string system, DateTimeOffset at) =>
        ColonisationSites.Empty.Apply(Event(
            $$"""{"timestamp":"{{at:O}}","event":"ColonisationSystemClaim","StarSystem":"{{system}}","SystemAddress":1}"""));

    private static PowerplayCycleMerits Merits(params MeritGain[] gains) =>
        PowerplayCycleMerits.None with { Power = "Zemina Torval", Gains = gains };

    private static BgsEntry Entry(DateTimeOffset at) => new(at, 1, "Faction", BgsWorkKind.Bounty, 100);

    private static BgsWork Work(params BgsEntry[] entries) => BgsWork.None with { Entries = entries };

    private static TechBrokerUnlocks Unlocked(TechBrokerRow row) =>
        TechBrokerUnlocks.Empty.Apply(Event(
            $$"""{"timestamp":"{{T1:O}}","event":"TechnologyBroker","ItemsUnlocked":[{"Name":"{{row.Symbols[0]}}"}]}"""));

    private static UnlockEvidence Evidence(string superpower, int engineerId, int tallyEngineerId) => new(
        ReputationState.Empty.Apply(Event(
            $$"""{"timestamp":"{{T1:O}}","event":"Reputation","{{superpower}}":10.0}""")),
        EngineerContributions.Empty.Apply(Event(
            $$"""{"timestamp":"{{T1:O}}","event":"EngineerContribution","EngineerID":{{engineerId}},"Type":"Commodity","Commodity":"tritium","TotalQuantity":5}""")),
        EngineerTallies.Empty.Apply(Event(
            $$"""{"timestamp":"{{T1:O}}","event":"EngineerProgress","EngineerID":{{tallyEngineerId}}}""")));
}
