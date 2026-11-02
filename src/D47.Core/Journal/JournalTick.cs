using System.Collections.Concurrent;
using D47.Core.Actions;
using D47.Core.Activities;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Checklists;
using D47.Core.Contribution;
using D47.Core.Knowledge;
using D47.Core.Lore;
using D47.Core.Ticking;

namespace D47.Core.Journal;

/// <summary>The journal tick subscribers, and what they leave for the subscribers registered after them.</summary>
public sealed class JournalTick(JournalTick.Sources sources)
{
    private bool _adopted;
    private int _dockingRequests;

    /// <summary>What the journal fold reads and writes.</summary>
    public sealed record Sources
    {
        public required JournalSpine Journal { get; init; }

        public required GameStateStore GameState { get; init; }

        public required HistoryBackfill History { get; init; }

        public required GameStatusReader Status { get; init; }

        public required NavRouteReader Route { get; init; }

        public required ModulePowerReader ModulePower { get; init; }

        public required MarketReader Markets { get; init; }

        public required EddnContribution Eddn { get; init; }

        public required ChecklistService Checklists { get; init; }

        public required CommodityLedger CommodityLedger { get; init; }

        public required ExobiologyLedger Exobiology { get; init; }

        public required CartographyLedger Cartography { get; init; }

        public required OutstandingCrimes Crimes { get; init; }

        public required ActivityLedger Activities { get; init; }

        public required SignalSightings SignalSightings { get; init; }

        public required CommanderErases CommanderErases { get; init; }

        public required FriendRoster Friends { get; init; }

        public required CommanderLog CommanderLog { get; init; }

        public required TravelLog TravelLog { get; init; }

        public required FullyFoundSystems FullyFound { get; init; }

        public required MailLedger Mail { get; init; }

        public required RoutePlanBook PlanBook { get; init; }

        public required NearbyFight Fight { get; init; }

        public required SceneTracker Scenes { get; init; }

        public required CalloutEngine Callouts { get; init; }

        public required AutonomousActionRunner Autonomous { get; init; }

        public required SamplingStore Sampling { get; init; }

        public required LoadoutStore Loadouts { get; init; }

        public required KitStore Kit { get; init; }

        public required LoreBook Lore { get; init; }

        public required LoreVisits LoreVisits { get; init; }
    }

    /// <summary>This tick's journal events, whichever Commander they are for.</summary>
    public IReadOnlyList<JournalEvent> Events { get; private set; } = [];

    /// <summary>This tick's journal events for the Commander shown.</summary>
    public IReadOnlyList<JournalEvent> Arrived { get; private set; } = [];

    /// <summary>Docking requests seen in the journal (#150): counted on the tick thread, read from any thread.</summary>
    public int DockingRequests => Volatile.Read(ref _dockingRequests);

    /// <summary>Commanders picked on the title bar or the Commanders page, taken on the tick thread.</summary>
    public ConcurrentQueue<CommanderIdentity> Picks { get; } = new();

    /// <summary>The events the Journal page reads (#51).</summary>
    public JournalLog JournalLog { get; } = new();

    /// <summary>Elite's own music track: the journal's witness that the galaxy map is showing (#365), and what ambience follows.</summary>
    public EliteMusic EliteMusic { get; } = new();

    /// <summary>The route file checked against Elite's route targets, folded after the file is polled.</summary>
    public LiveRoute LiveRoute { get; } = new();

    /// <summary>The settlement the Commander approached and has not left, which ambience follows.</summary>
    public SettlementVisit Settlement { get; } = new();

    /// <summary>Polls the journal and the state files and folds this tick's events, ledgers before callouts.</summary>
    public void Fold(TickContext context)
    {
        var journal = sources.Journal;
        var gameState = sources.GameState;
        var status = sources.Status;
        var route = sources.Route;

        // The first tick is the replay of the backlog, and the switch signal has to know that: a Commander change
        // met during the replay is history, not a login.
        var events = journal.Poll(priming: context.IsFirst);

        // While Elite runs a Commander other than the one shown, their events fold into their own state and
        // ledgers and nothing reacts to them.
        var offDuty = gameState.IsOffDuty;
        var inGame = gameState.InGame?.Identity.FrontierId;
        var shown = gameState.Shown(events);

        Events = events;
        Arrived = shown;

        EliteMusic.Observe(events);
        Settlement.Observe(events);

        if (events.Any(journalEvent => journalEvent.Kind == "DockingRequested"))
        {
            Interlocked.Increment(ref _dockingRequests);
        }

        JournalLog.Add(events, journal.Receipts);
        status.Poll();
        route.Poll();

        // The game's own route, so it follows the Commander Elite is running rather than the one shown.
        LiveRoute.Fold(events, route.Current, gameState.InGame?.Location);

        // Elite rewrites this one on outfitting and on a module being switched off, so it is read on the same
        // terms as the other two: only when its write time moves.
        sources.ModulePower.Poll();

        // The commodity board the Commander is standing in front of, if they have opened one.
        sources.Markets.Poll(gameState.InGame?.Location.StarPos);

        // Every event folds in, the backlog included; only a live one from the Commander shown is sent.
        sources.Eddn.Apply(events, context.Now, context.IsFirst, offDuty);

        // Before the callouts and inside this subscriber, so a verdict recomputed from this tick's events is
        // announced on this tick rather than the next.
        sources.Checklists.Poll(announce: !context.IsFirst && !offDuty, shown);

        // Before the callouts, so the sale callout reads a total that includes the sale it is announcing (#296).
        sources.CommodityLedger.Apply(events);

        // Before the callouts too, so an analysis is in the unsold total the sampling callout speaks.
        sources.Exobiology.Apply(events, inGame);
        sources.Cartography.Apply(events, inGame);
        sources.Crimes.Apply(events, inGame);
        sources.Activities.Apply(events, inGame);
        sources.SignalSightings.Apply(events);
        sources.CommanderErases.Apply(events);
        sources.Friends.Apply(events);
        sources.CommanderLog.Apply(events);
        sources.TravelLog.Apply(events);
        sources.FullyFound.Apply(events);

        if (!context.IsFirst)
        {
            sources.Mail.Fold(events, live: true, inGame, announced: !offDuty);
        }

        // Moves a stored plan's reached stop forward on arrival, replay included (#199).
        sources.PlanBook.Apply(events, inGame);

        // Outside the callouts, so the fight is followed while they or chatter are switched off.
        foreach (var journalEvent in shown)
        {
            sources.Fight.Fold(journalEvent, context.Now, context.IsFirst);
            sources.Scenes.Fold(journalEvent);
        }

        var calloutContext = CalloutContext.For(
            context.Now,
            context.IsFirst,
            gameState,
            status.Current,
            route.Current,
            events,
            LiveRoute);

        sources.Callouts.Tick(calloutContext);
        sources.Autonomous.Tick(calloutContext);

        // Written only when a sample actually landed, rather than every tick: this runs ten times a second and
        // the file changes a few times an hour.
        if (events.Any(journalEvent => journalEvent.Kind == "ScanOrganic"))
        {
            sources.Sampling.Save(gameState.All);
        }

        // The same cadence for the ships (#128).
        if (events.LastOrDefault(ShipLoadouts.MayChange) is { } changed)
        {
            // Stamped with that event's own time rather than with the clock, so the watermark the next catch-up
            // walks back to means what it says even when this tick is replaying a backlog from yesterday.
            sources.Loadouts.Save(gameState.All, changed.Timestamp);
        }

        // The same cadence for the suits and weapons (#293).
        if (events.LastOrDefault(OwnedKit.MayChange) is { } kitChanged)
        {
            sources.Kit.Save(gameState.All, kitChanged.Timestamp);
        }

        // The Commander's lore notes are hand-editable, so they are polled like the checklist is; the remark
        // stamps are written only when one was actually made.
        sources.Lore.Store.Poll();

        if (sources.LoreVisits.Dirty)
        {
            sources.LoreVisits.Save();
        }
    }

    /// <summary>
    /// Adopts the history walk's result on the first tick after it is done. Registered directly after
    /// <see cref="Fold"/>, so every later subscriber reads the adopted state on the same tick (#148).
    /// </summary>
    public void AdoptHistory(TickContext context)
    {
        if (_adopted || sources.History.State is not HistoryState.Done)
        {
            return;
        }

        _adopted = true;
        sources.GameState.RestoreLate();
    }

    /// <summary>Shows each Commander queued on <see cref="Picks"/>; game state is written on the tick thread.</summary>
    public void TakePicks(TickContext context)
    {
        while (Picks.TryDequeue(out var picked))
        {
            sources.GameState.Pick(picked);
        }
    }
}
