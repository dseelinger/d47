using D47.Core.Adventures;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Lore;
using D47.Core.Mining;
using D47.Core.Persona;
using D47.Core.Reminders;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>The callouts d47 ships with, in the order they are examined.</summary>
public static class ShippedCallouts
{
    /// <summary>What the shipped callouts read from.</summary>
    public sealed record Sources
    {
        public required SettingsService Settings { get; init; }

        public required ILoggerFactory Loggers { get; init; }

        public required ChecklistService Checklists { get; init; }

        public required LoreBook Lore { get; init; }

        public required LoreVisits LoreVisits { get; init; }

        public required AdventureBook Adventures { get; init; }

        public required ViewStateStore ViewState { get; init; }

        public required CommodityLedger Ledger { get; init; }

        public required CommunityGoalSearch CommunityGoal { get; init; }

        public required GameStateStore GameState { get; init; }

        public required ExobiologyLedger Exobiology { get; init; }

        public required CartographyLedger Cartography { get; init; }

        public required OutstandingCrimes Crimes { get; init; }

        public required NearbyFight Fight { get; init; }

        public required SceneTracker Scenes { get; init; }

        public required HandInOffer HandInOffer { get; init; }

        public required MarketBook MarketBook { get; init; }

        public required RoutePlanBook PlanBook { get; init; }

        public required StoryClueCallout StoryClue { get; init; }

        public required JournalReminderStore JournalReminders { get; init; }

        public required StandingWarnings StandingWarnings { get; init; }

        public required MiningTargetStore MiningTargets { get; init; }

        /// <summary>Whether a Commander's reached beats wait behind a story opening that must be said first.</summary>
        public required Func<string?, bool> StoryOpening { get; init; }
    }

    /// <summary>Builds the shipped catalogue and applies the current callout settings to it.</summary>
    public static CalloutEngine Build(Sources sources, DateTimeOffset now)
    {
        var settings = sources.Settings;
        var loggers = sources.Loggers;
        var gameState = sources.GameState;

        var sceneCallout = new SceneCallout(sources.Scenes);
        var surveyedBiology = new SurveyedBiologyCallout(loggers.CreateLogger<SurveyedBiologyCallout>());
        var tradingMode = new TradingModeCallout(loggers.CreateLogger<TradingModeCallout>());
        var biology = new BiologyCallout { AlreadySaid = surveyedBiology.Reported };
        surveyedBiology.AlreadySaid = biology.Named;

        var engine = new CalloutEngine(loggers.CreateLogger<CalloutEngine>())
            .Add(new DangerCallout())

            // Above everything except danger itself (Phase 15).
            .Add(new AnnouncedAttackCallout())
            .Add(new KillCallout())
            .Add(new FuelCallout(loggers.CreateLogger<FuelCallout>()))
            .Add(new FuelReachCallout(loggers.CreateLogger<FuelReachCallout>()))
            .Add(new RebuyCallout())
            .Add(new RouteCallout(loggers.CreateLogger<RouteCallout>()))
            .Add(new LongJumpCallout())
            .Add(new ArrivalCallout())

            // Capacity comes from the derived grade table.
            .Add(new MaterialMilestoneCallout { Capacity = MaterialGrades.CapacityOf })
            .Add(new PromotionCallout())

            // Phase 40, and the same capacity for the opposite purpose: the milestone callout needs it to
            // work out how far along a stock is, and this one needs it to say nothing about a stock that is
            // finished.
            .Add(new EmissionCallout(loggers.CreateLogger<EmissionCallout>())
            {
                Capacity = MaterialGrades.CapacityOf,
            })

            // Phase 41.
            .Add(new LimpetCallout())

            // Phase 11.
            .Add(new CarrierCallout())
            .Add(new CarrierFuelCallout { Plan = () => sources.PlanBook.Last(D47.Core.Knowledge.RoutePlanKind.Carrier), Warnings = sources.StandingWarnings })
            .Add(new CarrierUpkeepCallout { Warnings = sources.StandingWarnings })

            .Add(new MissionCallout { Offer = sources.HandInOffer, Markets = sources.MarketBook, Log = loggers.CreateLogger<MissionCallout>() })

            // Phase 17.
            .Add(new SamplingCallout { Ledger = sources.Exobiology })
            .Add(new SamplingRangeCallout())
            .Add(new AbandonedSamplesCallout())
            .Add(new DiscoveryCallout())
            .Add(new FootfallCallout())
            .Add(new FootfallApproachCallout())
            .Add(new MappingCallout { Ledger = sources.Cartography })
            .Add(new UnsoldDataAtRiskCallout(sources.Cartography, sources.Exobiology))
            .Add(new OutstandingCrimesCallout(sources.Crimes))
            .Add(surveyedBiology)
            .Add(biology)
            .Add(tradingMode)
            .Add(new ProspectorCallout { Target = () => sources.MiningTargets.For(gameState.Active?.Identity.FrontierId) })
            .Add(new CoreAsteroidCallout())
            .Add(new MiningSummaryCallout())
            .Add(new HoldFullCallout())
            .Add(new RingHotspotsCallout { Target = () => sources.MiningTargets.For(gameState.Active?.Identity.FrontierId) })
            .Add(new ChecklistCallout(sources.Checklists))
            .Add(new D47.Core.Reminders.JournalReminderCallout(sources.JournalReminders) { Capacity = MaterialGrades.CapacityOf })

            // Low on purpose.
            .Add(new RivalTerritoryCallout
            {
                LastExplainedDay = () => sources.ViewState.Load().RivalExplainedOn,
                RememberExplainedDay = day => sources.ViewState.Save(sources.ViewState.Load() with { RivalExplainedOn = day }),
            })

            .Add(new PowerplayMeritsCallout())
            .Add(new PowerplaySalvageCallout())
            .Add(new PowerplayCycleCallout
            {
                Boundary = () => (settings.Current.Callouts.WeekBoundaryDay, settings.Current.Callouts.WeekBoundaryHourUtc),
                LastSaidCycle = () => sources.ViewState.Load().PowerplayCycleSaidFor,
                RememberSaidCycle = cycle => sources.ViewState.Save(sources.ViewState.Load() with { PowerplayCycleSaidFor = cycle }),
            })

            // Phase 23.
            .Add(new LoreCallout(sources.Lore, sources.LoreVisits))

            // Phase 31, and the lowest thing here that is not the ambient line: it fires once, at the start
            // of a session, and it is about what was true before the Commander sat down.
            .Add(new ContinuityCallout())

            // The last complete session, after the line above.
            .Add(new RecapCallout())

            // Getting into a game and leaving one (change-requests.md 29), which is a different event from
            // the line above: that one greets when d47 starts, and this one when the game does.
            .Add(new SessionCallout())

            // Where a sale of the Community Goal commodity leaves the session, net of cost (#296).
            .Add(new CommunityGoalSaleCallout(sources.Ledger, sources.CommunityGoal))
            .Add(new CommunityGoalExpiryCallout())
            .Add(new ColonisationCallout())
            .Add(new SessionLengthCallout())
            .Add(new FighterCallout())
            .Add(new DockingCallout())

            // A remark on the subject the core aboard pays attention to (#611).
            .Add(new DomainCallout())

            // A beat of the Commander's story, when they reach it (Phase 47).
            .Add(new D47.Core.Adventures.AdventureCallout(sources.Adventures)
            {
                HasBackstory = () => !string.IsNullOrWhiteSpace(settings.Current.Llm.AboutMe),
                NudgeBackstory = () => settings.Current.Callouts.BackstoryNudge,
                Held = sources.StoryOpening,
            })

            // Invented chatter (#244): the marker only — the app composes the exchange, and with no model the
            // marker composes to nothing.
            .Add(new NpcChatterCallout(sources.Fight) { SceneHoldsTheFight = () => sceneCallout.HoldsTheFight })

            // The people of a settlement, or the pilots of a fight, reacting to the Commander: the marker only,
            // composed like chatter.
            .Add(sceneCallout)
            .Add(new AmbientCallout())

            // The Commander's story, told during a lull: the marker only, written by the model or not said.
            .Add(new NarratorCallout(sources.Fight) { Adventures = () => sources.Adventures.Audible(gameState.Active?.Identity.FrontierId, SystemWallClock.Instance.UtcNow)
                .Where(standing => standing.Adventure.IsActive && !standing.IsDone)
                .ToList() })

            // A clue the running stock story owes: the marker only, written by the model from the hidden layer or not said.
            .Add(sources.StoryClue)
            .Add(new IncomingMessages
            {
                Enabled = () => settings.Current.Speech.SpeakIncomingMessages,
                IncludeNpcs = () => settings.Current.Speech.SpeakNpcMessages,

                // squadleaders follows the Squadron row (#299) — a Commander does not know Elite writes those
                // as two channels, so there is one switch, not two.
                ChannelEnabled = channel => channel switch
                {
                    "starsystem" => settings.Current.Speech.SpeakSystemChat,
                    "local" => settings.Current.Speech.SpeakLocalChat,
                    "wing" => settings.Current.Speech.SpeakWingChat,
                    "squadron" or "squadleaders" => settings.Current.Speech.SpeakSquadronChat,
                    "player" => settings.Current.Speech.SpeakDirectMessages,
                    _ => true,
                },

                // Reads live state directly rather than waiting on the voice-scope follow to install it
                // (#102): a line judged before that follow has ever run still takes the authority road
                // when the state already says it should.
                AuthorityNearOwnCarrier = () =>
                    gameState.Active is { } active
                    && active.Carrier.Owned
                    && active.Carrier.StarSystem is { Length: > 0 } parked
                    && string.Equals(parked, active.Location.StarSystem, StringComparison.OrdinalIgnoreCase),

                // Read live rather than followed, for the same reason as the authority check above (#68).
                DockedStationName = () => gameState.Active?.Location is { Docked: true } here
                    ? here.StationName
                    : null,
                DockedStationAllegiance = () => gameState.Active?.Location.StationAllegiance,
            });

        // Elite echoes what you send back to you on the channel it went out on.

        Apply(engine, settings, now);
        return engine;
    }

    /// <summary>
    /// Pushes the callout settings into the engine and into the individual callouts that carry a
    /// tunable. Every lambda closes over the live <paramref name="settings"/> rather than a snapshot
    /// (#139). <paramref name="now"/> lets the engine tell a callout switched off seconds after it
    /// spoke from one switched off an hour later (#162).
    /// </summary>
    public static void Apply(CalloutEngine engine, SettingsService settings, DateTimeOffset now)
    {
        var callouts = settings.Current.Callouts;

        engine.Enabled = callouts.Enabled;
        engine.SetEnabled("danger", callouts.Danger, now);
        engine.SetEnabled("fuel", callouts.Fuel, now);
        engine.SetEnabled("route", callouts.Route, now);
        engine.SetEnabled("hyperspace-tunnel-delay", callouts.LongJump, now);
        engine.SetEnabled("arrival", callouts.Arrival, now);
        engine.SetEnabled("materials", callouts.Materials, now);
        engine.SetEnabled("emissions", callouts.Emissions, now);
        engine.SetEnabled("limpets", callouts.Limpets, now);
        engine.SetEnabled("announced-attack", callouts.AnnouncedAttack, now);
        engine.SetEnabled("kills", callouts.Kills, now);
        engine.SetEnabled("rebuy", callouts.Rebuy, now);
        engine.SetEnabled("rival-territory", callouts.RivalTerritory, now);
        engine.SetEnabled("powerplay-merits", callouts.PowerplayMerits, now);
        engine.SetEnabled("powerplay-salvage", callouts.PowerplaySalvage, now);
        engine.SetEnabled("powerplay-cycle", callouts.PowerplayCycle, now);
        engine.SetEnabled("sampling", callouts.Sampling, now);
        engine.SetEnabled("sampling-range", callouts.SamplingRange, now);
        engine.SetEnabled("sampling-abandoned", callouts.SamplingAbandoned, now);
        engine.SetEnabled("footfall", callouts.Footfall, now);
        engine.SetEnabled("footfall-approach", callouts.FootfallApproach, now);
        engine.SetEnabled("discovery", callouts.Discovery, now);
        engine.SetEnabled("mapping", callouts.Mapping, now);
        engine.SetEnabled("biology", callouts.Biology, now);
        engine.SetEnabled("surveyed-biology", callouts.SurveyedBiology, now);
        engine.SetEnabled("trading-mode", callouts.TradingMode, now);
        engine.SetEnabled("prospector", callouts.Prospector, now);
        engine.SetEnabled("core-asteroid", callouts.CoreAsteroid, now);
        engine.SetEnabled("ring-hotspots", callouts.RingHotspots, now);
        engine.SetEnabled("mining-summary", callouts.MiningSummary, now);
        engine.SetEnabled("hold-full", callouts.HoldFull, now);
        engine.SetEnabled("unsold-data-at-risk", callouts.UnsoldDataAtRisk, now);
        engine.SetEnabled("outstanding-crimes", callouts.OutstandingCrimes, now);
        engine.SetEnabled("checklist", callouts.Checklist, now);
        engine.SetEnabled("ambient", callouts.Ambient, now);
        engine.SetEnabled("narrator", callouts.Narrator, now);
        engine.SetEnabled("continuity", callouts.Continuity, now);
        engine.SetEnabled("recap", callouts.Recap, now);
        engine.SetEnabled("adventure", callouts.Adventure, now);
        engine.SetEnabled("community-goal-sales", callouts.CommunityGoalSales, now);
        engine.SetEnabled("community-goal-expiry", callouts.CommunityGoalExpiry, now);
        engine.SetEnabled("colonisation", callouts.Colonisation, now);
        engine.SetEnabled("session-length", callouts.SessionLength, now);
        engine.SetEnabled("fighter", callouts.Fighter, now);
        engine.SetEnabled("docking", callouts.DockingPad, now);
        engine.SetEnabled("domain", callouts.Domain, now);
        engine.SetEnabled("carrier-fuel", callouts.CarrierFuel, now);
        engine.SetEnabled("carrier-upkeep", callouts.CarrierUpkeep, now);
        engine.SetEnabled("missions", callouts.Missions, now);
        engine.SetEnabled("reminders", callouts.Reminders, now);

        foreach (var callout in engine.Callouts)
        {
            switch (callout)
            {
                case RouteCallout route:
                    route.EveryNJumps = callouts.RouteEveryNJumps;
                    break;

                case LongJumpCallout longJump:
                    longJump.Threshold = TimeSpan.FromSeconds(callouts.LongJumpSeconds);
                    break;

                case ArrivalCallout arrival:
                    arrival.HomeSystem = callouts.HomeSystem;
                    break;

                case CarrierCallout carrier:
                    carrier.CaptainName = () => settings.Current.Speech.CarrierCaptainName;
                    carrier.TowerName = () => settings.Current.Speech.TowerName;
                    break;

                case BiologyCallout biology:
                    biology.Threshold = () => settings.Current.Callouts.BiologyThreshold;
                    break;

                case SurveyedBiologyCallout surveyed:
                    surveyed.Threshold = () => settings.Current.Callouts.BiologyThreshold;
                    break;

                case TradingModeCallout trading:
                    trading.MinHold = () => settings.Current.Callouts.TradingModeMinHold;
                    trading.Filters = search => search with
                    {
                        MaxPriceAge = settings.Current.Trade.MaxPriceAgeHours,
                        LargePadOnly = settings.Current.Trade.LargePadOnly,
                        Planetary = settings.Current.Trade.Planetary,
                        MaxStationDistance = settings.Current.Trade.MaxStationDistance,
                    };
                    break;

                case LimpetCallout limpets:
                    limpets.Floor = () => settings.Current.Callouts.LimpetCargoFloor;
                    limpets.Percent = () => settings.Current.Callouts.LimpetPercent;
                    break;

                case LoreCallout lore:
                    // Read through the live settings rather than captured once, so a Commander who turns
                    // the lookup off is obeyed on the next arrival rather than on the next launch — the
                    // same shape the ambient row below has.
                    lore.Remarks = () => settings.Current.Callouts.Lore;
                    lore.Window = TimeSpan.FromDays(callouts.LoreCooldownDays);
                    break;

                case AmbientCallout ambient:
                    ambient.Interval = TimeSpan.FromSeconds(callouts.AmbientSeconds);
                    ambient.Longest = TimeSpan.FromSeconds(callouts.AmbientMaxSeconds);

                    // Silent while personality is off.
                    ambient.Enabled = () => settings.Current.Callouts.Ambient && settings.Current.Llm.PersonalityEnabled;
                    break;

                case DomainCallout domain:
                    // Silent while personality is off: with no core aboard there is no subject.
                    domain.Enabled = () => settings.Current.Callouts.Domain && settings.Current.Llm.PersonalityEnabled;
                    domain.Domain = () => PersonaCatalog.Resolve(settings.Current.Persona.Id).Domain;
                    break;

                case NpcChatterCallout chatter:
                    chatter.Interval = TimeSpan.FromSeconds(callouts.NpcChatterSeconds);
                    chatter.Longest = TimeSpan.FromSeconds(callouts.NpcChatterMaxSeconds);

                    // The ambient pair of gates (#244): theatre is personality by any reading, and the
                    // no-model half lives at the compose step for the reason above.
                    chatter.Enabled = () => settings.Current.Callouts.NpcChatter && settings.Current.Llm.PersonalityEnabled;
                    break;

                case SceneCallout scene:
                    scene.Enabled = () => settings.Current.Callouts.Scenes && settings.Current.Llm.PersonalityEnabled;
                    scene.Scenario = () => settings.Current.Llm.Scenario;
                    break;

                case D47.Core.Stories.StoryClueCallout clue:
                    clue.Enabled = () => settings.Current.Llm.PersonalityEnabled;
                    break;

                case NarratorCallout narrator:
                    narrator.Interval = TimeSpan.FromSeconds(callouts.NarratorSeconds);
                    narrator.Longest = TimeSpan.FromSeconds(callouts.NarratorMaxSeconds);
                    narrator.Enabled = () => settings.Current.Callouts.Narrator && settings.Current.Llm.PersonalityEnabled;
                    narrator.HasStory = () => settings.Current.Llm is var llm
                        && (!string.IsNullOrWhiteSpace(llm.CharacterSheet)
                            || !string.IsNullOrWhiteSpace(llm.AboutMe)
                            || !string.IsNullOrWhiteSpace(llm.Scenario));
                    break;

                case SessionLengthCallout length:
                    length.Hours = () => settings.Current.Callouts.SessionLengthHours;
                    break;
            }
        }
    }
}
