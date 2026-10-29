using D47.Core.Callouts;
using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Speaking without being asked (Phase 8).</summary>
public static class CalloutCapability
{
    public const string Id = "callouts";

    // No key here is a prefix of another. "callouts.route" would have been, of "callouts.routeEveryNJumps" —
    // and since one is protected and the other is not, anything matching keys by prefix reads the pair as a
    // protected row being offered to the model.
    public const string EnabledKey = "callouts.enabled";
    public const string DangerKey = "callouts.danger";
    public const string FuelKey = "callouts.fuel";
    public const string RebuyKey = "callouts.rebuy";
    public const string RouteKey = "callouts.routeProgress";
    public const string LongJumpKey = "callouts.longJumpRemark";
    public const string ArrivalKey = "callouts.arrival";
    public const string MaterialsKey = "callouts.materials";
    public const string EmissionsKey = "callouts.emissions";
    public const string LimpetsKey = "callouts.limpets";
    public const string LimpetCargoFloorKey = "callouts.limpetCargoFloor";
    public const string LimpetPercentKey = "callouts.limpetPercent";

    public const string ProspectorKey = "callouts.prospector";

    public const string CoreAsteroidKey = "callouts.coreAsteroid";
    public const string RingHotspotsKey = "callouts.ringHotspots";
    public const string MiningSummaryKey = "callouts.miningSummary";
    public const string HoldFullKey = "callouts.holdFull";
    public const string UnsoldDataAtRiskKey = "callouts.unsoldDataAtRisk";
    public const string OutstandingCrimesKey = "callouts.outstandingCrimes";

    public const string SamplingKey = "callouts.sampling";
    public const string SamplingRangeKey = "callouts.samplingRange";
    public const string SamplingAbandonedKey = "callouts.samplingAbandoned";
    public const string DiscoveryKey = "callouts.discovery";
    public const string FootfallKey = "callouts.footfall";
    public const string FootfallApproachKey = "callouts.footfallApproach";
    public const string MappingKey = "callouts.mapping";
    public const string BodyValueKey = "callouts.bodyValue";
    public const string BodyValueThresholdKey = "callouts.bodyValueThreshold";
    public const string NotableBodiesKey = "callouts.notableBodies";
    public const string NotableCollidingBinaryKey = "callouts.notableCollidingBinary";
    public const string NotableShepherdMoonKey = "callouts.notableShepherdMoon";
    public const string NotableNestedMoonKey = "callouts.notableNestedMoon";
    public const string NotableEccentricOrbitKey = "callouts.notableEccentricOrbit";
    public const string NotableFastRotatorKey = "callouts.notableFastRotator";
    public const string NotableWideRingsKey = "callouts.notableWideRings";
    public const string NotablePremiumFsdInjectionKey = "callouts.notablePremiumFsdInjection";
    public const string NotableLandableAtmosphereKey = "callouts.notableLandableAtmosphere";
    public const string GeologyKey = "callouts.geology";
    public const string CodexDiscoveredKey = "callouts.codexDiscovered";
    public const string RegionFirstKey = "callouts.regionFirst";
    public const string BiologyKey = "callouts.biologyValue";
    public const string SurveyedBiologyKey = "callouts.surveyedBiology";
    public const string EarlierFindsKey = "callouts.earlierFinds";
    public const string BiologyThresholdKey = "callouts.biologyThreshold";
    public const string TradingModeKey = "callouts.tradingMode";
    public const string TradingModeMinHoldKey = "callouts.tradingModeMinHold";
    public const string AnnouncedAttackKey = "callouts.announcedAttack";
    public const string KillsKey = "callouts.kills";
    public const string MetBeforeKey = "callouts.metBefore";
    public const string RivalTerritoryKey = "callouts.rivalTerritory";
    public const string CommunityKey = "callouts.community";
    public const string PowerplayMeritsKey = "callouts.powerplayMerits";
    public const string PowerplaySalvageKey = "callouts.powerplaySalvage";
    public const string PowerplayCycleKey = "callouts.powerplayCycle";
    public const string BgsTickKey = "callouts.bgsTick";
    public const string FriendsOnlineKey = "callouts.friendsOnline";

    public const string WingKey = "callouts.wing";
    public const string GalNetKey = "callouts.galNet";
    public const string ServerStatusKey = "callouts.serverStatus";
    public const string ChecklistKey = "callouts.checklist";
    public const string CommunityGoalSalesKey = "callouts.communityGoalSales";

    public const string CarrierFuelKey = "callouts.carrierFuel";
    public const string CommunityGoalExpiryKey = "callouts.communityGoalExpiry";

    public const string ColonisationKey = "callouts.colonisation";
    public const string FighterKey = "callouts.fighter";
    public const string DockingPadKey = "callouts.dockingPad";
    public const string StationDamageKey = "callouts.stationDamage";
    public const string SessionLengthKey = "callouts.sessionLength";
    public const string SessionLengthHoursKey = "callouts.sessionLengthHours";
    public const string CarrierUpkeepKey = "callouts.carrierUpkeep";
    public const string CarrierPayrollKey = "callouts.carrierPayroll";
    public const string MissionsKey = "callouts.missions";
    public const string RemindersKey = "callouts.reminders";
    public const string DomainKey = "callouts.domain";
    public const string NotableShipsKey = "callouts.notableShips";
    public const string ShipModesKey = "callouts.shipModes";
    public const string RouteEveryKey = "callouts.routeEveryNJumps";
    public const string LongJumpSecondsKey = "callouts.longJumpSeconds";
    public const string HomeSystemKey = "callouts.homeSystem";
    /// <summary>One line at the start of a session, picking up where the Commander left off (Phase 31).</summary>
    public const string ContinuityKey = "callouts.continuity";

    /// <summary>One line after the opening line, recapping the last complete session.</summary>
    public const string RecapKey = "callouts.recap";

    /// <summary>
    /// Something d47 noticed the Commander keeps doing, said when the circumstance comes round again
    /// (Phase 32).
    /// </summary>

    /// <summary>A beat of the Commander's adventure, said when it is reached (Phase 47).</summary>
    public const string AdventureKey = "callouts.adventure";

    public const string BackstoryNudgeKey = "callouts.backstoryNudge";

    public const string AmbientKey = "callouts.ambient";
    public const string AmbientSecondsKey = "callouts.ambientSeconds";
    public const string AmbientMaxSecondsKey = "callouts.ambientMaxSeconds";

    /// <summary>Invented background chatter (#244) — theatre, never the game's own traffic.</summary>
    public const string NpcChatterKey = "callouts.npcChatter";
    public const string NpcChatterSecondsKey = "callouts.npcChatterSeconds";
    public const string NpcChatterMaxSecondsKey = "callouts.npcChatterMaxSeconds";

    /// <summary>Scene chatter at a settlement on foot and in a ship fight.</summary>
    public const string ScenesKey = "callouts.scenes";

    /// <summary>The Narrator, telling the Commander's story during a lull.</summary>
    public const string NarratorKey = "callouts.narrator";
    public const string NarratorSecondsKey = "callouts.narratorSeconds";
    public const string NarratorMaxSecondsKey = "callouts.narratorMaxSeconds";

    public static CapabilityDescriptor Create(Func<string> describe) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Callouts",
        Summary = "Speak up about danger, fuel, route progress and arrivals without waiting to be asked.",
        Examples =
        [
            "what are you watching for",
            "stop calling things out",
            "start calling things out",
        ],

        // Phrases only, and each one at least three words. "callouts" alone would hijack any sentence
        // containing the word, which is the rule JournalCapability documents.
        Keywords =
        [
            "what are you watching",
            "what do you warn about",
            "stop calling things out",
            "start calling things out",
            "stop the callouts",
            "enable callouts",
            "disable callouts",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Callouts", Order = 35 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_callouts",
                Exercise =
                    "Ask D47 what it calls out. The reply lists each unprompted announcement and whether it is "
                    + "on.",
                Description =
                    "List the things D47 announces without being asked, and whether each one is currently on.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(describe())),
            },
        ],
        Settings = Rows(),
    };

    private static IReadOnlyList<SettingRow> Rows()
    {
        var rows = new List<SettingRow>
        {
            new()
            {
                Key = EnabledKey,
                Exercise =
                    "Open Settings › Your core › Callouts, group All callouts, and turn Speak without being asked "
                    + "off. Fly through a jump and let a warning condition happen, such as low fuel: D47 says nothing "
                    + "unprompted, and every callout row below disappears. Say \"stop calling things out\" and the row "
                    + "shows off; say \"start calling things out\" and it shows on.",
                Label = "Speak without being asked",
                Help = "Off means D47 only ever answers. Every warning below stops with it.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                DocsAnchor = "enabled",
                Protected = true,
                Commands =
                [
                    new SettingCommandPhrase("stop calling things out", "false"),
                    new SettingCommandPhrase("stop the callouts", "false"),
                    new SettingCommandPhrase("disable callouts", "false"),
                    new SettingCommandPhrase("start calling things out", "true"),
                    new SettingCommandPhrase("enable callouts", "true"),
                ],
                Binding = SettingBinding.Toggle(
                    s => s.Callouts.Enabled,
                    (s, on) => s with { Callouts = s.Callouts with { Enabled = on } }),
            },
        };

        rows.AddRange(
        [
            Toggle(
                DangerKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Danger (on by default). "
                + "In flight, get interdicted, let your shields drop or run hot: a warning is spoken for each. "
                + "With the row off the same events are silent. Say \"stop calling out danger\": the row shows off "
                + "and that is no longer said. Say \"start calling out danger\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "Danger",
                "Interdiction, hyperdiction, shields down, hull damage, overheating and a full cargo hold.",
                "danger",
                "danger",
                s => s.Callouts.Danger,
                (s, v) => s with { Callouts = s.Callouts with { Danger = v } }),

            Toggle(
                FuelKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Fuel and range (on by "
                + "default). Fly with low fuel, or plot a route longer than the fuel lasts with nowhere to "
                + "refuel: D47 warns. Start a jump toward a star you cannot scoop while out of range and it warns "
                + "before the jump. Say \"stop calling out fuel\": the row shows off and that is no longer said. "
                + "Say \"start calling out fuel\" and the row shows on. The row is absent while Speak without being "
                + "asked is off.",
                "Fuel and range",
                "Low fuel, a route longer than the fuel lasts with nowhere to refuel, and an unscoopable star before a jump out of range.",
                "fuel",
                "fuel",
                s => s.Callouts.Fuel,
                (s, v) => s with { Callouts = s.Callouts with { Fuel = v } }),

            Toggle(
                RebuyKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Rebuy cover (on by "
                + "default). Start a session in a ship whose rebuy is more than your balance: D47 says so once. "
                + "With the row off nothing is said. Say \"stop calling out rebuy\": the row shows off and that is "
                + "no longer said. Say \"start calling out rebuy\" and the row shows on. The row is absent while "
                + "Speak without being asked is off.",
                "Rebuy cover",
                "Once, when your balance will not cover the rebuy on the ship you are flying.",
                "rebuy",
                "rebuy",
                s => s.Callouts.Rebuy,
                (s, v) => s with { Callouts = s.Callouts with { Rebuy = v } }),

            Toggle(
                RouteKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Route progress (on by "
                + "default). Plot a route of several jumps and make one: D47 says the jumps remaining and the "
                + "next system, and warns of a neutron or white dwarf star ahead. Say \"stop calling out route "
                + "progress\": the row shows off and that is no longer said. Say \"start calling out route "
                + "progress\" and the row shows on. The row is absent while Speak without being asked is off.",
                "Route progress",
                "Jumps remaining, the next system, and neutron or white dwarf hazards ahead.",
                "route",
                "route progress",
                s => s.Callouts.Route,
                (s, v) => s with { Callouts = s.Callouts with { Route = v } }),

            Toggle(
                LongJumpKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Hyperspace tunnel "
                + "delays (on by default). Start a jump and stay in the hyperspace tunnel past the time set in "
                + "Hyperspace Tunnel Delay: D47 makes a remark. A jump shorter than that is silent. Say \"stop "
                + "calling out hyperspace tunnel delays\": the row shows off and that is no longer said. Say "
                + "\"start calling out hyperspace tunnel delays\" and the row shows on. The row is absent while "
                + "Speak without being asked is off.",
                "Hyperspace tunnel delays",
                "A remark when a hyperspace jump runs longer than usual.",
                "long-jump",
                "hyperspace tunnel delays",
                s => s.Callouts.LongJump,
                (s, v) => s with { Callouts = s.Callouts with { LongJump = v } }),

            Toggle(
                ArrivalKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Arrivals (on by "
                + "default). Arrive in your home system, a system holding your carrier or stored ships, or one "
                + "with engineering stations: D47 says so on arrival. Say \"stop calling out arrivals\": the row "
                + "shows off and that is no longer said. Say \"start calling out arrivals\" and the row shows on. "
                + "The row is absent while Speak without being asked is off.",
                "Arrivals",
                "Your home system, where your carrier is, ships stored here, and stations offering engineering.",
                "arrival",
                "arrivals",
                s => s.Callouts.Arrival,
                (s, v) => s with { Callouts = s.Callouts with { Arrival = v } }),

            Toggle(
                MaterialsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "Material milestones (on by default). Collect a material you have none of: D47 names it. Keep "
                + "collecting and it reports progress toward a full stock. Say \"stop calling out materials\": the "
                + "row shows off and that is no longer said. Say \"start calling out materials\" and the row shows "
                + "on. The row is absent while Speak without being asked is off.",
                "Material milestones",
                "The first unit of a material, and progress towards a full stock where the cap is known.",
                "materials",
                "materials",
                s => s.Callouts.Materials,
                (s, v) => s with { Callouts = s.Callouts with { Materials = v } }),

            Toggle(
                EmissionsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "High grade emissions (on by default). Arrive at a system that could be running high grade "
                + "emissions: D47 names the grade 5 materials and the faction state. It is silent about materials "
                + "you are full of. Say \"stop calling out high grade emissions\": the row shows off and that is no "
                + "longer said. Say \"start calling out high grade emissions\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "High grade emissions",
                "On arriving somewhere that could be running them: which grade 5 materials, from which "
                + "faction's state. Silent about anything you are already full of.",
                "emissions",
                "high grade emissions",
                s => s.Callouts.Emissions,
                (s, v) => s with { Callouts = s.Callouts with { Emissions = v } }),

            Toggle(
                LimpetsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and turn "
                + "Limpet reminders on (it is off by default). Dock at a station that sells limpets with a big "
                + "hold and few limpets aboard: D47 reminds you to buy some. It is silent when you already carry "
                + "enough. Say \"stop calling out limpet reminders\": the row shows off and that is no longer said. "
                + "Say \"start calling out limpet reminders\" and the row shows on. The row is absent while Speak "
                + "without being asked is off.",
                "Limpet reminders",
                "On docking somewhere that sells limpets, with a big hold and few aboard. Off by default: "
                + "it is for Commanders who fly limpets.",
                "limpets",
                "limpet reminders",
                s => s.Callouts.Limpets,
                (s, v) => s with { Callouts = s.Callouts with { Limpets = v } },

                // Off, for the reason the row says.
                defaultOn: false),

            Toggle(
                ProspectorKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "Prospector results (on by default). Fire a prospector limpet at a rock: D47 says what is in it "
                + "and whether it is the richest of the session. Say \"stop calling out prospector results\": the "
                + "row shows off and that is no longer said. Say \"start calling out prospector results\" and the "
                + "row shows on. The row is absent while Speak without being asked is off.",
                "Prospector results",
                "What a prospector limpet found in a rock, and whether it is the richest of the session. With a "
                + "mining target set, only the target material, and whether the rock is above or below it.",
                "prospector",
                "prospector results",
                s => s.Callouts.Prospector,
                (s, v) => s with { Callouts = s.Callouts with { Prospector = v } }),

            Toggle(
                CoreAsteroidKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "Core asteroids (on by default). Prospect a core asteroid: D47 says it is a core and what is "
                + "inside. Say \"stop calling out core asteroids\": the row shows off and that is no longer said. "
                + "Say \"start calling out core asteroids\" and the row shows on. The row is absent while Speak "
                + "without being asked is off.",
                "Core asteroids",
                "A core asteroid, and what is in it. Separate from prospector results because it is rare.",
                "core-asteroid",
                "core asteroids",
                s => s.Callouts.CoreAsteroid,
                (s, v) => s with { Callouts = s.Callouts with { CoreAsteroid = v } }),

            Toggle(
                RingHotspotsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "Ring hotspots (on by default). Map a ring with the system map or surface scanner: D47 lists "
                + "its hotspots, most first. Say \"stop calling out ring hotspots\": the row shows off and that is "
                + "no longer said. Say \"start calling out ring hotspots\" and the row shows on. The row is absent "
                + "while Speak without being asked is off.",
                "Ring hotspots",
                "When you map a ring, its hotspots, most first. With a mining target set, the target leads, or "
                + "the line says the ring has none.",
                "ring-hotspots",
                "ring hotspots",
                s => s.Callouts.RingHotspots,
                (s, v) => s with { Callouts = s.Callouts with { RingHotspots = v } }),

            Toggle(
                MiningSummaryKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "Mining run summary (on by default). Refine cargo in a mining run, then dock: D47 says the "
                + "tonnes refined, the rate, the materials and the limpets launched. It is silent if nothing was "
                + "refined. Say \"stop calling out mining run summary\": the row shows off and that is no longer "
                + "said. Say \"start calling out mining run summary\" and the row shows on. The row is absent while "
                + "Speak without being asked is off.",
                "Mining run summary",
                "When a mining run ends on docking: tonnes refined, the rate, the materials and the limpets "
                + "launched. Silent when nothing was refined or the run ended in a death.",
                "mining-summary",
                "mining run summary",
                s => s.Callouts.MiningSummary,
                (s, v) => s with { Callouts = s.Callouts with { MiningSummary = v } }),

            Toggle(
                HoldFullKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, and find "
                + "Hold full while mining (on by default). Mine until the hold has no free space: D47 says the "
                + "hold is full, once per mining run. Say \"stop calling out hold full while mining\": the row "
                + "shows off and that is no longer said. Say \"start calling out hold full while mining\" and the "
                + "row shows on. The row is absent while Speak without being asked is off.",
                "Hold full while mining",
                "Once per mining run, when the hold has no free space. Silent when no run is open.",
                "hold-full",
                "hold full while mining",
                s => s.Callouts.HoldFull,
                (s, v) => s with { Callouts = s.Callouts with { HoldFull = v } }),

            Toggle(
                UnsoldDataAtRiskKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Unsold data at "
                + "risk (on by default). Carry unsold scan and biology data worth five million credits or more, "
                + "then drop into a war zone or a hazardous resource extraction site, or let your hull fall below "
                + "sixty percent: D47 warns, at most once every thirty minutes for each of the three. Say \"stop "
                + "calling out unsold data warning\": the row shows off and that is no longer said. Say \"start "
                + "calling out unsold data warning\" and the row shows on. The row is absent while Speak without "
                + "being asked is off.",
                "Unsold data at risk",
                "On dropping into a war zone or a hazardous resource extraction site, or when your hull falls "
                + "below sixty percent, while unsold scan and biology data is worth five million credits or more. "
                + "Said at most once every thirty minutes for each of the three.",
                "unsold-data-at-risk",
                "unsold data warning",
                s => s.Callouts.UnsoldDataAtRisk,
                (s, v) => s with { Callouts = s.Callouts with { UnsoldDataAtRisk = v } }),

            Toggle(
                OutstandingCrimesKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Unpaid fines and "
                + "bounties (on by default). Owe fines or bounties to a faction and arrive in a system where it "
                + "is present: D47 names the largest debt, at most once an hour for each system. Say \"stop "
                + "calling out unpaid fines warning\": the row shows off and that is no longer said. Say \"start "
                + "calling out unpaid fines warning\" and the row shows on. The row is absent while Speak without "
                + "being asked is off.",
                "Unpaid fines and bounties",
                "On arriving in a system where a faction you owe fines or bounties is present, on the ship you "
                + "are flying or on foot. Names the largest debt, at most once an hour for each system.",
                "outstanding-crimes",
                "unpaid fines warning",
                s => s.Callouts.OutstandingCrimes,
                (s, v) => s with { Callouts = s.Callouts with { OutstandingCrimes = v } }),

            Toggle(
                SamplingKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Sampling progress "
                + "(on by default). On a planet with biology, take an organic sample: D47 says how many specimens "
                + "you have taken and how far you moved for the last one. Say \"stop calling out sampling "
                + "progress\": the row shows off and that is no longer said. Say \"start calling out sampling "
                + "progress\" and the row shows on. The row is absent while Speak without being asked is off.",
                "Sampling progress",
                "How many organic specimens you have taken and how far you moved for the last one.",
                "sampling",
                "sampling progress",
                s => s.Callouts.Sampling,
                (s, v) => s with { Callouts = s.Callouts with { Sampling = v } }),

            Toggle(
                SamplingRangeKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Far enough to "
                + "sample (on by default). After taking a sample, walk away from every specimen of the set: D47 "
                + "says when you are far enough to sample again, once per specimen. Say \"stop calling out far "
                + "enough to sample\": the row shows off and that is no longer said. Say \"start calling out far "
                + "enough to sample\" and the row shows on. The row is absent while Speak without being asked is "
                + "off.",
                "Far enough to sample",
                "Once per specimen, when you are the genus' sampling distance from every specimen of the set "
                + "you are taking. Silent without a surface position.",
                "sampling-range",
                "far enough to sample",
                s => s.Callouts.SamplingRange,
                (s, v) => s with { Callouts = s.Callouts with { SamplingRange = v } }),

            Toggle(
                SamplingAbandonedKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Abandoned samples "
                + "(on by default). Take one or two samples, then sample a different species: D47 says the "
                + "earlier samples were dropped. Say \"stop calling out abandoned samples\": the row shows off and "
                + "that is no longer said. Say \"start calling out abandoned samples\" and the row shows on. The "
                + "row is absent while Speak without being asked is off.",
                "Abandoned samples",
                "When a specimen of a different species drops a set of one or two samples you had taken.",
                "sampling-abandoned",
                "abandoned samples",
                s => s.Callouts.SamplingAbandoned,
                (s, v) => s with { Callouts = s.Callouts with { SamplingAbandoned = v } }),

            Toggle(
                DiscoveryKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Undiscovered "
                + "systems (on by default). Arrive at a star nobody has sold data on: D47 says the system is "
                + "undiscovered. Say \"stop calling out undiscovered systems\": the row shows off and that is no "
                + "longer said. Say \"start calling out undiscovered systems\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "Undiscovered systems",
                "The arrival star's autoscan, when nobody has sold data on it yet.",
                "discovery",
                "undiscovered systems",
                s => s.Callouts.Discovery,
                (s, v) => s with { Callouts = s.Callouts with { Discovery = v } }),

            Toggle(
                FootfallKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find First footfall (on "
                + "by default). Disembark onto a body whose scan shows nobody has walked it: D47 says it is a "
                + "first footfall, once per body. Say \"stop calling out first footfall\": the row shows off and "
                + "that is no longer said. Say \"start calling out first footfall\" and the row shows on. The row "
                + "is absent while Speak without being asked is off.",
                "First footfall",
                "The first time you disembark onto a body, once its scan shows nobody has walked it yet.",
                "footfall",
                "first footfall",
                s => s.Callouts.Footfall,
                (s, v) => s with { Callouts = s.Callouts with { Footfall = v } }),

            Toggle(
                FootfallApproachKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find First footfall on "
                + "approach (on by default). Approach a body with biological signals that nobody has walked: D47 "
                + "says so before you land, once per body. Say \"stop calling out first footfall on approach\": the "
                + "row shows off and that is no longer said. Say \"start calling out first footfall on approach\" "
                + "and the row shows on. The row is absent while Speak without being asked is off.",
                "First footfall on approach",
                "On approaching a body with biological signals whose scan shows nobody has walked it yet. "
                + "Once per body.",
                "footfall-approach",
                "first footfall on approach",
                s => s.Callouts.FootfallApproach,
                (s, v) => s with { Callouts = s.Callouts with { FootfallApproach = v } }),

            Toggle(
                MappingKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Mapped body value "
                + "(on by default). Finish mapping a body with the surface scanner: D47 says about what the map "
                + "is worth and the total of mapped bodies not yet sold. Say \"stop calling out mapped bodies\": "
                + "the row shows off and that is no longer said. Say \"start calling out mapped bodies\" and the "
                + "row shows on. The row is absent while Speak without being asked is off.",
                "Mapped body value",
                "When you finish mapping a body with the surface scanner, about what it is worth and the "
                + "total of mapped bodies not yet sold.",
                "mapping",
                "mapped bodies",
                s => s.Callouts.Mapping,
                (s, v) => s with { Callouts = s.Callouts with { Mapping = v } }),

            Toggle(
                BodyValueKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Valuable bodies "
                + "(on by default). Scan a planet that would pay at least the figure in Call out bodies from if "
                + "mapped efficiently: D47 says so with about what mapping pays. Say \"stop calling out valuable "
                + "bodies\": the row shows off and that is no longer said. Say \"start calling out valuable bodies\" "
                + "and the row shows on. The row is absent while Speak without being asked is off.",
                "Valuable bodies",
                "A planet worth mapping, said when you first scan it, with about what mapping it pays. "
                + "The threshold is below.",
                "body-value",
                "valuable bodies",
                s => s.Callouts.BodyValue,
                (s, v) => s with { Callouts = s.Callouts with { BodyValue = v } }),

            Toggle(
                NotableBodiesKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Notable bodies (on "
                + "by default). Scan a body with an unusual trait: D47 says the trait. This is the switch for the "
                + "eight rows under it; with it off they disappear. Say \"stop calling out notable bodies\": the "
                + "row shows off and that is no longer said. Say \"start calling out notable bodies\" and the row "
                + "shows on. The row is absent while Speak without being asked is off.",
                "Notable bodies",
                "A scanned body's notable traits, said when you scan it or when a later scan completes one.",
                "notable-bodies",
                "notable bodies",
                s => s.Callouts.NotableBodies,
                (s, v) => s with { Callouts = s.Callouts with { NotableBodies = v } }),

            Toggle(
                NotableCollidingBinaryKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Colliding binaries "
                + "(on by default). Scan a pair of bodies whose orbits would touch: D47 says so. Say \"stop "
                + "calling out colliding binaries\": the row shows off and that is no longer said. Say \"start "
                + "calling out colliding binaries\" and the row shows on. The row is absent while Speak without "
                + "being asked is off. The row appears only while Notable bodies is on.",
                "Colliding binaries",
                "A pair of bodies whose orbits would touch.",
                "notable-bodies",
                "colliding binaries",
                s => s.Callouts.NotableCollidingBinary,
                (s, v) => s with { Callouts = s.Callouts with { NotableCollidingBinary = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotableShepherdMoonKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Shepherd moons (on "
                + "by default). Scan a moon orbiting inside its parent's rings: D47 says it is a shepherd moon. "
                + "Say \"stop calling out shepherd moons\": the row shows off and that is no longer said. Say "
                + "\"start calling out shepherd moons\" and the row shows on. The row is absent while Speak without "
                + "being asked is off. The row appears only while Notable bodies is on.",
                "Shepherd moons",
                "A moon orbiting inside its parent's rings.",
                "notable-bodies",
                "shepherd moons",
                s => s.Callouts.NotableShepherdMoon,
                (s, v) => s with { Callouts = s.Callouts with { NotableShepherdMoon = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotableNestedMoonKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Moons of moons (on "
                + "by default). Scan a moon whose parent is itself a moon: D47 says so. Say \"stop calling out "
                + "moons of moons\": the row shows off and that is no longer said. Say \"start calling out moons of "
                + "moons\" and the row shows on. The row is absent while Speak without being asked is off. The row "
                + "appears only while Notable bodies is on.",
                "Moons of moons",
                "A moon whose parent is itself a moon.",
                "notable-bodies",
                "moons of moons",
                s => s.Callouts.NotableNestedMoon,
                (s, v) => s with { Callouts = s.Callouts with { NotableNestedMoon = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotableEccentricOrbitKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Eccentric orbits "
                + "(on by default). Scan a body with an orbital eccentricity of 0.8 or more: D47 says the orbit "
                + "is eccentric. Say \"stop calling out eccentric orbits\": the row shows off and that is no longer "
                + "said. Say \"start calling out eccentric orbits\" and the row shows on. The row is absent while "
                + "Speak without being asked is off. The row appears only while Notable bodies is on.",
                "Eccentric orbits",
                "An orbit with an eccentricity of 0.8 or more.",
                "notable-bodies",
                "eccentric orbits",
                s => s.Callouts.NotableEccentricOrbit,
                (s, v) => s with { Callouts = s.Callouts with { NotableEccentricOrbit = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotableFastRotatorKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Fast rotators (on "
                + "by default). Scan a body that is not tidally locked and turns in under six hours: D47 says it "
                + "is a fast rotator. Say \"stop calling out fast rotators\": the row shows off and that is no "
                + "longer said. Say \"start calling out fast rotators\" and the row shows on. The row is absent "
                + "while Speak without being asked is off. The row appears only while Notable bodies is on.",
                "Fast rotators",
                "A body that is not tidally locked and turns in under six hours.",
                "notable-bodies",
                "fast rotators",
                s => s.Callouts.NotableFastRotator,
                (s, v) => s with { Callouts = s.Callouts with { NotableFastRotator = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotableWideRingsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Wide rings (on by "
                + "default). Scan a body with a ring wider than ten times its radius: D47 says the rings are "
                + "wide. Say \"stop calling out wide rings\": the row shows off and that is no longer said. Say "
                + "\"start calling out wide rings\" and the row shows on. The row is absent while Speak without "
                + "being asked is off. The row appears only while Notable bodies is on.",
                "Wide rings",
                "A ring wider than ten times the body's radius.",
                "notable-bodies",
                "wide rings",
                s => s.Callouts.NotableWideRings,
                (s, v) => s with { Callouts = s.Callouts with { NotableWideRings = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotablePremiumFsdInjectionKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Premium FSD "
                + "injection materials (on by default). Scan a landable body with five or six of the six "
                + "materials for the premium FSD injection: D47 says so. Say \"stop calling out premium FSD "
                + "injection bodies\": the row shows off and that is no longer said. Say \"start calling out "
                + "premium FSD injection bodies\" and the row shows on. The row is absent while Speak without "
                + "being asked is off. The row appears only while Notable bodies is on.",
                "Premium FSD injection materials",
                "A landable body with five or six of the six materials for the premium FSD injection.",
                "notable-bodies",
                "premium FSD injection bodies",
                s => s.Callouts.NotablePremiumFsdInjection,
                (s, v) => s with { Callouts = s.Callouts with { NotablePremiumFsdInjection = v } },
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                NotableLandableAtmosphereKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and turn Landable with an "
                + "atmosphere on (it is off by default). Scan a landable body with an atmosphere: D47 says so. "
                + "Say \"stop calling out landable bodies with an atmosphere\": the row shows off and that is no "
                + "longer said. Say \"start calling out landable bodies with an atmosphere\" and the row shows on. "
                + "The row is absent while Speak without being asked is off. The row appears only while Notable "
                + "bodies is on.",
                "Landable with an atmosphere",
                "A landable body with an atmosphere. Off until you turn it on.",
                "notable-bodies",
                "landable bodies with an atmosphere",
                s => s.Callouts.NotableLandableAtmosphere,
                (s, v) => s with { Callouts = s.Callouts with { NotableLandableAtmosphere = v } },
                defaultOn: false,
                appliesWhen: s => s.Callouts.NotableBodies),

            Toggle(
                BiologyKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find High-value biology "
                + "(on by default). Scan a landable body whose biology could reach the figure in Call out biology "
                + "from: D47 says so. Say \"stop calling out high-value biology\": the row shows off and that is no "
                + "longer said. Say \"start calling out high-value biology\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "High-value biology",
                "A landable body whose biology could reach the threshold below, said when you scan it.",
                "biology",
                "high-value biology",
                s => s.Callouts.Biology,
                (s, v) => s with { Callouts = s.Callouts with { Biology = v } }),

            Toggle(
                GeologyKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Geology missing "
                + "from your codex (on by default). Scan a landable body with likely geological features you have "
                + "not logged in this region: D47 names them. Say \"stop calling out missing geology\": the row "
                + "shows off and that is no longer said. Say \"start calling out missing geology\" and the row "
                + "shows on. The row is absent while Speak without being asked is off.",
                "Geology missing from your codex",
                "A landable body's likely geological features you have not logged in this region, said when you scan it.",
                "geology",
                "missing geology",
                s => s.Callouts.Geology,
                (s, v) => s with { Callouts = s.Callouts with { Geology = v } }),

            Toggle(
                CodexDiscoveredKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find New codex entries "
                + "(on by default). Log a new codex entry: D47 says \"Discovered\" and the entry's name. Say \"stop "
                + "calling out codex discoveries\": the row shows off and that is no longer said. Say \"start "
                + "calling out codex discoveries\" and the row shows on. The row is absent while Speak without "
                + "being asked is off.",
                "New codex entries",
                "Every new codex entry, said as \"Discovered\" and its name when you log it.",
                "codex-discovered",
                "codex discoveries",
                s => s.Callouts.CodexDiscovered,
                (s, v) => s with { Callouts = s.Callouts with { CodexDiscovered = v } }),

            Toggle(
                RegionFirstKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Possible region "
                + "firsts (on by default). Log a new organic codex entry that the shipped Canonn table has no "
                + "report of: D47 says it may be a region first. Say \"stop calling out region firsts\": the row "
                + "shows off and that is no longer said. Say \"start calling out region firsts\" and the row shows "
                + "on. The row is absent while Speak without being asked is off.",
                "Possible region firsts",
                "A new organic codex entry the Canonn table in this build has no report of, in this region or "
                + "anywhere, said when you log it.",
                "region-first",
                "region firsts",
                s => s.Callouts.RegionFirst,
                (s, v) => s with { Callouts = s.Callouts with { RegionFirst = v } }),

            Toggle(
                SurveyedBiologyKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Surveyed biology "
                + "on arrival (on by default). Arrive in a system where Spansh has surveyed biology reaching the "
                + "figure in Call out biology from: D47 names the bodies. Say \"stop calling out surveyed "
                + "biology\": the row shows off and that is no longer said. Say \"start calling out surveyed "
                + "biology\" and the row shows on. The row is absent while Speak without being asked is off. The "
                + "row appears only while Look things up in the galaxy is on.",
                "Surveyed biology on arrival",
                "On arriving in a system, the bodies Spansh has surveyed biology on that reach the threshold "
                + "below. Needs galaxy search.",
                "surveyed-biology",
                "surveyed biology",
                s => s.Callouts.SurveyedBiology,
                (s, v) => s with { Callouts = s.Callouts with { SurveyedBiology = v } },
                appliesWhen: s => s.Knowledge.GalaxySearch),

            Toggle(
                EarlierFindsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Earlier biology "
                + "finds on mapping (on by default). Map a body with biology that Canonn has earlier finds on: "
                + "D47 names the variants found there. Say \"stop calling out earlier biology finds\": the row "
                + "shows off and that is no longer said. Say \"start calling out earlier biology finds\" and the "
                + "row shows on. The row is absent while Speak without being asked is off. The row appears only "
                + "while Earlier biology finds from Canonn is on.",
                "Earlier biology finds on mapping",
                "On mapping a body with biology, the variants Canonn has earlier finds of there. Needs "
                + "earlier biology finds from Canonn.",
                "earlier-finds",
                "earlier biology finds",
                s => s.Callouts.EarlierFinds,
                (s, v) => s with { Callouts = s.Callouts with { EarlierFinds = v } },
                appliesWhen: s => s.Knowledge.CommunityBiology),

            Toggle(
                TradingModeKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and turn Trading Mode on (it "
                + "is off by default). Dock with a route plotted: D47 says the best cargo to buy here for the "
                + "system at the end of it, using the filters saved on the Trade route page. Say \"stop calling "
                + "out trading mode\": the row shows off and that is no longer said. Say \"start calling out "
                + "trading mode\" and the row shows on. The row is absent while Speak without being asked is off. "
                + "The row appears only while Look things up in the galaxy is on.",
                "Trading Mode",
                "Docked with a route plotted, the best cargo to buy here for the system at the end of it. "
                + "Needs galaxy search, and uses the filters saved on the Trade route page.",
                "trading-mode",
                "trading mode",
                s => s.Callouts.TradingMode,
                (s, v) => s with { Callouts = s.Callouts with { TradingMode = v } },
                defaultOn: false,
                appliesWhen: s => s.Knowledge.GalaxySearch),

            Toggle(
                AnnouncedAttackKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Announced attacks (on "
                + "by default). Be threatened by an NPC that says it will interdict you or take your cargo: D47 "
                + "warns before it acts. Say \"stop calling out announced attacks\": the row shows off and that is "
                + "no longer said. Say \"start calling out announced attacks\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "Announced attacks",
                "An NPC saying it is about to interdict you or take your cargo, before it does.",
                "announced-attack",
                "announced attacks",
                s => s.Callouts.AnnouncedAttack,
                (s, v) => s with { Callouts = s.Callouts with { AnnouncedAttack = v } }),

            Toggle(
                KillsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Notable kills (on by "
                + "default). Destroy ships: D47 mentions the first kill of the session, the best reward so far, "
                + "two kills inside thirty seconds, and every fifth kill. Say \"stop calling out kills\": the row "
                + "shows off and that is no longer said. Say \"start calling out kills\" and the row shows on. The "
                + "row is absent while Speak without being asked is off.",
                "Notable kills",
                "The first kill of the session, the best reward so far, two kills inside thirty seconds, "
                + "and every fifth kill.",
                "kills",
                "kills",
                s => s.Callouts.Kills,
                (s, v) => s with { Callouts = s.Callouts with { Kills = v } }),

            Toggle(
                MetBeforeKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Commanders met "
                + "before (on by default). Target a Commander you met in an earlier session while not under "
                + "threat: D47 says when, where and what happened, once per Commander per session. Say \"stop "
                + "calling out commanders met before\": the row shows off and that is no longer said. Say \"start "
                + "calling out commanders met before\" and the row shows on. The row is absent while Speak without "
                + "being asked is off.",
                "Commanders met before",
                "A Commander you target who you met in an earlier game session: when, where and what happened, "
                + "once per Commander per session and never while you are under threat.",
                "met-before",
                "commanders met before",
                s => s.Callouts.MetBefore,
                (s, v) => s with { Callouts = s.Callouts with { MetBefore = v } }),

            Toggle(
                RivalTerritoryKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Rival Power territory "
                + "(on by default). Fly in normal space in a system controlled by a Power other than yours: D47 "
                + "says it is rival territory. Say \"stop calling out enemy territory\": the row shows off and that "
                + "is no longer said. Say \"start calling out enemy territory\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "Rival Power territory",
                "Flying in normal space in a system controlled by a Power other than the one you fly for.",
                "rival-territory",
                "enemy territory",
                s => s.Callouts.RivalTerritory,
                (s, v) => s with { Callouts = s.Callouts with { RivalTerritory = v } }),

            Toggle(
                PowerplayMeritsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Powerplay merits (on by "
                + "default). Enter normal space in a Powerplay system you can reinforce or undermine: D47 says "
                + "which way the merit modifier leans. Say \"stop calling out powerplay merits\": the row shows off "
                + "and that is no longer said. Say \"start calling out powerplay merits\" and the row shows on. The "
                + "row is absent while Speak without being asked is off.",
                "Powerplay merits",
                "Which way the merit modifier leans, on first entering normal space in a Powerplay system you can reinforce or undermine.",
                "powerplay-merits",
                "powerplay merits",
                s => s.Callouts.PowerplayMerits,
                (s, v) => s with { Callouts = s.Callouts with { PowerplayMerits = v } }),

            Toggle(
                PowerplaySalvageKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, and find Powerplay salvage (on "
                + "by default). Scoop salvage in your Power's system and start a jump away without handing it in: "
                + "D47 warns before you go. Say \"stop calling out powerplay salvage\": the row shows off and that "
                + "is no longer said. Say \"start calling out powerplay salvage\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "Powerplay salvage",
                "A warning before you jump away from your Power's system with salvage you scooped there and have not handed in.",
                "powerplay-salvage",
                "powerplay salvage",
                s => s.Callouts.PowerplaySalvage,
                (s, v) => s with { Callouts = s.Callouts with { PowerplaySalvage = v } }),

            Toggle(
                PowerplayCycleKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Powerplay cycle (on "
                + "by default). Play until twelve hours before the Powerplay cycle ends: D47 says how many merits "
                + "you have earned for your Power in it. Say \"stop calling out powerplay cycle\": the row shows "
                + "off and that is no longer said. Say \"start calling out powerplay cycle\" and the row shows on. "
                + "The row is absent while Speak without being asked is off.",
                "Powerplay cycle",
                "Twelve hours before the Powerplay cycle ends, how many merits you have earned for your Power in it.",
                "powerplay-cycle",
                "powerplay cycle",
                s => s.Callouts.PowerplayCycle,
                (s, v) => s with { Callouts = s.Callouts with { PowerplayCycle = v } }),

            Toggle(
                BgsTickKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find BGS tick (on by "
                + "default). When the BGS ticks while D47 runs, D47 says so. Say \"stop calling out BGS tick\": the "
                + "row shows off and that is no longer said. Say \"start calling out BGS tick\" and the row shows "
                + "on. The row is absent while Speak without being asked is off. The row appears only while BGS "
                + "tick time under Outside sources is on.",
                "BGS tick",
                "When the BGS ticks, says so. Needs BGS tick time under Outside sources.",
                "bgs-tick",
                "BGS tick",
                s => s.Callouts.BgsTick,
                (s, v) => s with { Callouts = s.Callouts with { BgsTick = v } },
                appliesWhen: s => s.Knowledge.BgsTick),

            Toggle(
                GalNetKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find GalNet headlines (on "
                + "by default). When new GalNet articles arrive, D47 says their headlines. Say \"stop calling out "
                + "GalNet headlines\": the row shows off and that is no longer said. Say \"start calling out GalNet "
                + "headlines\" and the row shows on. The row is absent while Speak without being asked is off. The "
                + "row appears only while GalNet news under Outside sources is on.",
                "GalNet headlines",
                "When new GalNet articles arrive, says their headlines. Needs GalNet news under Outside sources.",
                "galnet",
                "GalNet headlines",
                s => s.Callouts.GalNet,
                (s, v) => s with { Callouts = s.Callouts with { GalNet = v } },
                appliesWhen: s => s.Knowledge.GalNet),

            Toggle(
                ServerStatusKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Server status (on by "
                + "default). When Elite's servers go down for maintenance, go offline or come back, D47 says so. "
                + "Say \"stop calling out Server status\": the row shows off and that is no longer said. Say \"start "
                + "calling out Server status\" and the row shows on. The row is absent while Speak without being "
                + "asked is off. The row appears only while Server status under Outside sources is on.",
                "Server status",
                "When Elite's servers go down for maintenance, go offline or come back, says so. Needs Server status under Outside sources.",
                "server-status",
                "Server status",
                s => s.Callouts.ServerStatus,
                (s, v) => s with { Callouts = s.Callouts with { ServerStatus = v } },
                appliesWhen: s => s.Knowledge.ServerStatus),

            Toggle(
                FriendsOnlineKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Friends online (on "
                + "by default). Have a friend in the game come online: D47 says who. Going offline is not said. "
                + "Say \"stop calling out friends online\": the row shows off and that is no longer said. Say "
                + "\"start calling out friends online\" and the row shows on. The row is absent while Speak without "
                + "being asked is off.",
                "Friends online",
                "When a friend comes online, says who. Going offline is not said.",
                "friends",
                "friends online",
                s => s.Callouts.FriendsOnline,
                (s, v) => s with { Callouts = s.Callouts with { FriendsOnline = v } }),

            Toggle(
                WingKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Wing changes (on by "
                + "default). Join a wing: D47 says who is in it. When a wingmate joins it says who. Leaving is "
                + "not said. Say \"stop calling out wing changes\": the row shows off and that is no longer said. "
                + "Say \"start calling out wing changes\" and the row shows on. The row is absent while Speak "
                + "without being asked is off.",
                "Wing changes",
                "When you join a wing, says who is in it, and when a wingmate joins, says who. Leaving is not said.",
                "wing",
                "wing changes",
                s => s.Callouts.Wing,
                (s, v) => s with { Callouts = s.Callouts with { Wing = v } }),

            Toggle(
                ChecklistKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Checklist changes "
                + "(on by default). With a checklist plan open, finish what a plan item needs, or let the journal "
                + "change an item: D47 says the change, the last unit a plan needed, or the line ticked by the "
                + "event it watches for. Say \"stop calling out checklist changes\": the row shows off and that is "
                + "no longer said. Say \"start calling out checklist changes\" and the row shows on. The row is "
                + "absent while Speak without being asked is off.",
                "Checklist changes",
                "A plan item the journal has just changed its mind about, the last unit a plan needed, and a line ticked by the event it watches for.",
                "checklist",
                "checklist changes",
                s => s.Callouts.Checklist,
                (s, v) => s with { Callouts = s.Callouts with { Checklist = v } }),

            Toggle(
                ContinuityKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Picking up where you "
                + "left off (on by default). Start a session after an earlier one: D47 says how long it has been, "
                + "where you were and what your plans were waiting on. It is silent when there is nothing to say. "
                + "Say \"stop calling out where I left off\": the row shows off and that is no longer said. Say "
                + "\"start calling out where I left off\" and the row shows on. The row is absent while Speak "
                + "without being asked is off.",
                "Picking up where you left off",
                "One line at the start of a session: how long it has been, where you were, and what your "
                + "plans were waiting on. Silent when there is nothing to say.",
                "continuity",
                "where I left off",
                s => s.Callouts.Continuity,
                (s, v) => s with { Callouts = s.Callouts with { Continuity = v } }),

            Toggle(
                RecapKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Last session recap "
                + "(on by default). Start a session after an earlier one: after the opening line D47 says where "
                + "your last session ended, the ship and the most notable thing in it. It is silent with no "
                + "earlier session. Say \"stop calling out the last session recap\": the row shows off and that is "
                + "no longer said. Say \"start calling out the last session recap\" and the row shows on. The row "
                + "is absent while Speak without being asked is off.",
                "Last session recap",
                "One line after the opening line: where your last session ended, the ship, and the most "
                + "notable thing that happened in it. Silent when there was no earlier session.",
                "recap",
                "the last session recap",
                s => s.Callouts.Recap,
                (s, v) => s with { Callouts = s.Callouts with { Recap = v } }),

            Toggle(
                AdventureKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Narrator and stories, group Stories, and find Story objectives "
                + "(on by default). Follow a story and reach the place an objective waits for: D47 says the "
                + "objective. With the row off the story stays in the conversation and is not read out. Say \"stop "
                + "calling out the story\": the row shows off and that is no longer said. Say \"start calling out "
                + "the story\" and the row shows on. The row is absent while Speak without being asked is off.",
                "Story objectives",
                "An objective of the story you are following, said when you reach the place it waits for. Off "
                + "leaves the story in the conversation and stops it being read out.",
                "adventure",
                "the story",
                s => s.Callouts.Adventure,
                (s, v) => s with { Callouts = s.Callouts with { Adventure = v } }),

            Toggle(
                BackstoryNudgeKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Narrator and stories, group Stories, and find Backstory "
                + "reminder (on by default). With a Backstory set, reach a story objective that turns the story: "
                + "after it D47 says one plain line that your Backstory still describes where the story began and "
                + "is yours to change. Say \"stop calling out the backstory reminder\": the row shows off and that "
                + "is no longer said. Say \"start calling out the backstory reminder\" and the row shows on. The "
                + "row is absent while Speak without being asked is off. The row appears only while Story "
                + "objectives is on.",
                "Backstory reminder",
                "After a story objective that turns the story, one plain line saying your Backstory still "
                + "describes where it began and is yours to change. Said only when you have a Backstory, and "
                + "only if the objective itself was said. No model writes or changes it.",
                "backstory-nudge",
                "the backstory reminder",
                s => s.Callouts.BackstoryNudge,
                (s, v) => s with { Callouts = s.Callouts with { BackstoryNudge = v } }),

            Toggle(
                CommunityGoalSalesKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Community Goal sales "
                + "(on by default). Sell the Community Goal commodity: D47 says where that leaves you this "
                + "session, net of what the cargo cost. Say \"stop calling out community goal sales\": the row "
                + "shows off and that is no longer said. Say \"start calling out community goal sales\" and the row "
                + "shows on. The row is absent while Speak without being asked is off.",
                "Community Goal sales",
                "After every sale of the Community Goal commodity, where that leaves you this session, "
                + "net of what the cargo cost. The sale's own figure is on your screen; this is the total.",
                "community-goal-sales",
                "community goal sales",
                s => s.Callouts.CommunityGoalSales,
                (s, v) => s with { Callouts = s.Callouts with { CommunityGoalSales = v } }),

            Toggle(
                CommunityGoalExpiryKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Community Goal "
                + "expiry (on by default). Play with a joined community goal that has eight hours or less left: "
                + "D47 says so, once for each goal. Say \"stop calling out community goal expiry\": the row shows "
                + "off and that is no longer said. Say \"start calling out community goal expiry\" and the row "
                + "shows on. The row is absent while Speak without being asked is off.",
                "Community Goal expiry",
                "When a community goal you have joined has eight hours or less left. Said once for each goal.",
                "community-goal-expiry",
                "community goal expiry",
                s => s.Callouts.CommunityGoalExpiry,
                (s, v) => s with { Callouts = s.Callouts with { CommunityGoalExpiry = v } }),

            Toggle(
                ColonisationKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Colonisation "
                + "deadlines (on by default). Claim a system or visit a construction site: D47 says the beacon "
                + "and primary port deadlines and the squadron rule, reminds you as they near, and says how many "
                + "trips the remaining tonnes need in this ship. Say \"stop calling out colonisation deadlines\": "
                + "the row shows off and that is no longer said. Say \"start calling out colonisation deadlines\" "
                + "and the row shows on. The row is absent while Speak without being asked is off.",
                "Colonisation deadlines",
                "At a system claim, the beacon and primary port deadlines and the squadron rule; reminders as they near; "
                + "and how many trips a construction site's remaining tonnes need in this ship.",
                "colonisation",
                "colonisation deadlines",
                s => s.Callouts.Colonisation,
                (s, v) => s with { Callouts = s.Callouts with { Colonisation = v } }),

            Toggle(
                FighterKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Fighter pilot, and find Fighter "
                + "pilot (on by default). Launch, dock, lose or rebuild a fighter with a hired pilot assigned: "
                + "the pilot speaks. With no pilot assigned it is silent. Say \"stop calling out the fighter "
                + "pilot\": the row shows off and that is no longer said. Say \"start calling out the fighter "
                + "pilot\" and the row shows on. The row is absent while Speak without being asked is off.",
                "Fighter pilot",
                "Your active hired pilot says so when the fighter launches, docks, is lost or is rebuilt. Silent with no pilot assigned.",
                "fighter",
                "the fighter pilot",
                s => s.Callouts.Fighter,
                (s, v) => s with { Callouts = s.Callouts with { Fighter = v } }),

            Toggle(
                DockingPadKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Landing pad (on by "
                + "default). Get landing clearance at a Coriolis, Orbis, Ocellus or asteroid base: D47 says where "
                + "the pad is, as a clock hour and a depth. Say \"stop calling out the landing pad\": the row shows "
                + "off and that is no longer said. Say \"start calling out the landing pad\" and the row shows on. "
                + "The row is absent while Speak without being asked is off.",
                "Landing pad",
                "Where the granted pad is in a Coriolis, Orbis, Ocellus or asteroid base, as a clock hour and a depth.",
                "docking",
                "the landing pad",
                s => s.Callouts.DockingPad,
                (s, v) => s with { Callouts = s.Callouts with { DockingPad = v } }),

            Toggle(
                StationDamageKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and find Station damage (on "
                + "by default). Dock at a station that is under attack, damaged, under repairs or abandoned: D47 "
                + "says so. Say \"stop calling out damaged stations\": the row shows off and that is no longer "
                + "said. Say \"start calling out damaged stations\" and the row shows on. The row is absent while "
                + "Speak without being asked is off.",
                "Station damage",
                "On docking, when the station is under attack, damaged, under repairs or abandoned.",
                "station-damage",
                "damaged stations",
                s => s.Callouts.StationDamage,
                (s, v) => s with { Callouts = s.Callouts with { StationDamage = v } }),

            Toggle(
                CommunityKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Mention the Discord "
                + "every two weeks (on by default). Play for fourteen days: at a quiet moment D47 says one line "
                + "pointing at the D47 Discord, once every 14 days. Say \"stop calling out the Discord\": the row "
                + "shows off and that is no longer said. Say \"start calling out the Discord\" and the row shows "
                + "on. The row is absent while Speak without being asked is off.",
                "Mention the Discord every two weeks",
                "One line pointing at the D47 Discord, said at a quiet moment once every 14 days.",
                "community",
                "the Discord",
                s => s.Callouts.Community,
                (s, v) => s with { Callouts = s.Callouts with { Community = v } }),

            Toggle(
                SessionLengthKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and turn Break reminder on "
                + "(it is off by default). Set Remind me to take a break after (hours) to 2, then play for two "
                + "hours: D47 reminds you to take a break, once per session. Say \"stop calling out how long I "
                + "have been flying\": the row shows off and that is no longer said. Say \"start calling out how "
                + "long I have been flying\" and the row shows on. The row is absent while Speak without being "
                + "asked is off.",
                "Break reminder",
                "Reminds you to take a break once a session has run as long as you choose. Said once per session. Off by default.",
                "session-length",
                "how long I have been flying",
                s => s.Callouts.SessionLength,
                (s, v) => s with { Callouts = s.Callouts with { SessionLength = v } },
                defaultOn: false),

            Toggle(
                CarrierFuelKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Carrier fuel (on by "
                + "default). Dock at your own carrier or plot its jump with tritium for less than two full jumps "
                + "at its current load: D47 warns, once for each reading. Say \"stop calling out carrier fuel\": "
                + "the row shows off and that is no longer said. Say \"start calling out carrier fuel\" and the row "
                + "shows on. The row is absent while Speak without being asked is off.",
                "Carrier fuel",
                "On docking at your own carrier or plotting its jump, when its tritium is less than two "
                + "full jumps at its current load. Said once for each reading.",
                "carrier-fuel",
                "carrier fuel",
                s => s.Callouts.CarrierFuel,
                (s, v) => s with { Callouts = s.Callouts with { CarrierFuel = v } }),

            Toggle(
                CarrierUpkeepKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Carrier upkeep (on "
                + "by default). Dock at your own carrier or plot its jump with a balance covering less than four "
                + "weeks of upkeep: D47 warns, once for each recorded balance. Say \"stop calling out carrier "
                + "upkeep\": the row shows off and that is no longer said. Say \"start calling out carrier upkeep\" "
                + "and the row shows on. The row is absent while Speak without being asked is off.",
                "Carrier upkeep",
                "On docking at your own carrier or plotting its jump, when its balance covers less than "
                + "four weeks of upkeep. Said once for each recorded balance.",
                "carrier-upkeep",
                "carrier upkeep",
                s => s.Callouts.CarrierUpkeep,
                (s, v) => s with { Callouts = s.Callouts with { CarrierUpkeep = v } }),

            Toggle(
                CarrierPayrollKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Carrier captain, and find Carrier "
                + "payroll (on by default). Dock at your own carrier for the first time after the weekly tick: "
                + "the carrier captain mentions the week's payroll and the name Elite has on file for them, once "
                + "each tick week. Say \"stop calling out carrier payroll\": the row shows off and that is no "
                + "longer said. Say \"start calling out carrier payroll\" and the row shows on. The row is absent "
                + "while Speak without being asked is off.",
                "Carrier payroll",
                "On the first docking at your own carrier after the weekly tick, the captain mentions the "
                + "week's payroll and the name Elite has on file for them. Said once for each tick week.",
                "carrier-payroll",
                "carrier payroll",
                s => s.Callouts.CarrierPayroll,
                (s, v) => s with { Callouts = s.Callouts with { CarrierPayroll = v } }),

            Toggle(
                MissionsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Mission hand-ins and "
                + "expiry (on by default). Dock where a mission hands in, leave with one unclaimed, or wait until "
                + "an hour and ten minutes before one expires: D47 speaks. Accept a mission whose cargo is more "
                + "than your hold, or a collect mission whose cargo is sold where you are docked: D47 says so. "
                + "Say \"stop calling out mission callouts\": the row shows off and that is no longer said. Say "
                + "\"start calling out mission callouts\" and the row shows on. The row is absent while Speak "
                + "without being asked is off.",
                "Mission hand-ins and expiry",
                "On docking where missions hand in, on leaving with one unclaimed, and an hour and ten "
                + "minutes before a mission expires. On accepting a mission, when its cargo is more than "
                + "your hold or a collect mission's cargo is sold at the station you are docked at.",
                "missions",
                "mission callouts",
                s => s.Callouts.Missions,
                (s, v) => s with { Callouts = s.Callouts with { Missions = v } }),

            Toggle(
                RemindersKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Reminders (on by "
                + "default). Ask D47 to remind you of a moment in the game, such as \"remind me when I next dock\", "
                + "then reach it: D47 says the reminder once, ending in your words. With none set nothing is "
                + "said. Say \"stop calling out reminders\": the row shows off and that is no longer said. Say "
                + "\"start calling out reminders\" and the row shows on. The row is absent while Speak without "
                + "being asked is off.",
                "Reminders",
                "A reminder you set for a moment in the game — your next docking, a station, a system, your own "
                + "carrier, an empty or full hold, a full material, your next session — said once when it comes, "
                + "ending in your own words. With none set, nothing is said.",
                "reminders",
                "reminders",
                s => s.Callouts.Reminders,
                (s, v) => s with { Callouts = s.Callouts with { Reminders = v } }),

            Toggle(
                DomainKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Your core's own "
                + "subject (on by default). Fly with a core that has a subject, such as Quartermaster, Sentinel "
                + "or Chart, and earn credits: the core occasionally remarks on that subject with a figure from "
                + "this session. It is silent with personality off. Say \"stop calling out core remarks\": the row "
                + "shows off and that is no longer said. Say \"start calling out core remarks\" and the row shows "
                + "on. The row is absent while Speak without being asked is off.",
                "Your core's own subject",
                "An occasional remark from the core aboard about the subject it pays attention to, with a "
                + "figure from this session. Quartermaster says the credits an hour after a sale, a mission "
                + "or a cashed voucher, at most once an hour, and names a community goal you have not joined, once per goal. Sentinel says the credits an hour from bounties, combat bonds and vouchers. Chart says the credits an hour from exploration data. Mender says what repairs have cost this session. Archivist counts the firsts this session. Warden and the cores you write have no subject "
                + "and say nothing. Silent with personality off.",
                "domain",
                "core remarks",
                s => s.Callouts.Domain,
                (s, v) => s with { Callouts = s.Callouts with { Domain = v } }),

            Toggle(
                NotableShipsKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Notable ships (on by "
                + "default). Pass a notable ship, such as a wedding or funeral convoy, nomads, a refugee "
                + "flotilla, a tour liner, a protester or a training flight: the core remarks, once per kind per "
                + "session. Say \"stop calling out notable ships\": the row shows off and that is no longer said. "
                + "Say \"start calling out notable ships\" and the row shows on. The row is absent while Speak "
                + "without being asked is off.",
                "Notable ships",
                "A remark from the core aboard when a notable ship passes: a wedding or funeral convoy, nomads, "
                + "a refugee flotilla, a tour liner, a protester or propagandist, a training flight. Once per kind per session.",
                "notable-ships",
                "notable ships",
                s => s.Callouts.NotableShips,
                (s, v) => s with { Callouts = s.Callouts with { NotableShips = v } }),

            Toggle(
                ShipModesKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, and find Ship modes (on by "
                + "default). Hold the ship in a mode for a while, such as silent running, flight assist off, "
                + "hardpoints out with nothing hostile around, the scoop left open or the gear down away from a "
                + "station: the core remarks, once per mode per session. Say \"stop calling out ship modes\": the "
                + "row shows off and that is no longer said. Say \"start calling out ship modes\" and the row shows "
                + "on. The row is absent while Speak without being asked is off.",
                "Ship modes",
                "A remark from the core aboard when you hold the ship in a mode for a while: silent running, "
                + "flight assist off, hardpoints out with nothing hostile around, the cargo scoop left open, "
                + "the landing gear down away from a station, or mass locked in danger. Once per mode per session.",
                "ship-modes",
                "ship modes",
                s => s.Callouts.ShipModes,
                (s, v) => s with { Callouts = s.Callouts with { ShipModes = v } }),

            Toggle(
                AmbientKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, and find Ambient (on by "
                + "default). Fly quietly for a few minutes: the core or a crew member says an unprompted "
                + "in-character line. The two rows under this one set the gap between lines. Say \"stop calling "
                + "out in ship chatter\": the row shows off and that is no longer said. Say \"start calling out in "
                + "ship chatter\" and the row shows on. The row is absent while Speak without being asked is off. "
                + "The row appears only while a language model is configured.",

                // Named for who is speaking rather than for what the line is like, on the Commander's
                // instruction (2026-09-02). "Ambient" described the occasion and left a Commander to work out
                // whose voice it was; the pair reads as a boundary now - inside the ship, and outside it -
                // and borrows the wording the Aboard voice slot already uses so one vocabulary covers both
                // surfaces.
                "In Ship chatter",
                "Your core and your crew - the voices inside your ship. The occasional "
                + "in-character line to you, said because nothing has happened rather than because "
                + "something did; only the AI speaks unasked, and your crew answer on the comms "
                + "key. Written by the model in the core's own voice, so it needs a "
                + "language model configured; with none there is no chatter.",
                "ambient",
                "in ship chatter",
                s => s.Callouts.Ambient,
                (s, v) => s with { Callouts = s.Callouts with { Ambient = v } },

                // Chatter is model-written or it is nothing (#245): with no provider there is nothing this
                // toggle could govern, and a row that does not apply is absent rather than a switch that does
                // nothing.
                appliesWhen: s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId),

            Toggle(
                NpcChatterKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, and find NPC chatter (on "
                + "by default). Fly in a busy system: made-up radio traffic plays on the System comms channel, "
                + "such as pilots talking to each other or the dock telling somebody off. A hail selects the one "
                + "who hailed you. Say \"stop calling out npc chatter\": the row shows off and that is no longer "
                + "said. Say \"start calling out npc chatter\" and the row shows on. The row is absent while Speak "
                + "without being asked is off. The row appears only while a language model is configured.",
                "NPC chatter",
                "Anybody outside your ship. Made-up radio traffic from people who do not exist - "
                + "passers-by talking to each other, the dock telling somebody off, the occasional "
                + "word to you. Theatre, written by the model. Everyone you hear is on the System comms "
                + "channel until you jump or they sign off, and a hail selects the one who hailed you; "
                + "speak on the comms key and whoever is selected answers. Each reply is "
                + "billed like any turn, on the background model. The game's own NPC messages are a "
                + "different switch, under Speech.",
                "npc-chatter",
                "npc chatter",
                s => s.Callouts.NpcChatter,
                (s, v) => s with { Callouts = s.Callouts with { NpcChatter = v } },

                // The same rule as the ambient row above (#245): no model, no theatre, no row.
                appliesWhen: s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId),

            Toggle(
                ScenesKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, and find Scene chatter (on "
                + "by default). Be on foot at a settlement or in a fight and do something notable, such as "
                + "arriving or being found: the people there react on their radio, at most once every 45 seconds. "
                + "Say \"stop calling out scene chatter\": the row shows off and that is no longer said. Say \"start "
                + "calling out scene chatter\" and the row shows on. The row is absent while Speak without being "
                + "asked is off. The row appears only while a language model is configured.",
                "Scene chatter",
                "The people of a settlement you are on foot at, or the pilots of a fight you are in, heard on "
                + "their radio reacting to what just happened: your arrival, being found or interdicted, a killing, "
                + "your death and your escape. Needs a current "
                + "scenario, which they are given whatever Who knows about it is set to. At most one exchange "
                + "every 45 seconds. Written by the model; with no model, or personality off, there are no scenes.",
                "scenes",
                "scene chatter",
                s => s.Callouts.Scenes,
                (s, v) => s with { Callouts = s.Callouts with { Scenes = v } },
                appliesWhen: s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId),

            Toggle(
                NarratorKey,
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Narrator and stories, group Narrator, and find Narrator (on by "
                + "default). Play through a lull outside supercruise and combat, with a character sheet, "
                + "backstory or scenario written: the Narrator tells your story in the third person. Say \"stop "
                + "calling out the narrator\": the row shows off and that is no longer said. Say \"start calling "
                + "out the narrator\" and the row shows on. The row is absent while Speak without being asked is "
                + "off. The row appears only while a language model is configured.",
                "Narrator",
                "A voice of its own that tells your story in the third person during a lull, from your "
                + "character sheet, your backstory and your scenario. Never in supercruise or during a fight, "
                + "and silent while all three are empty, unless COVAS is aboard: then it speaks in place of "
                + "COVAS's idle remarks. Written by the model, which is sent the whole "
                + "backstory each time; with no model, or personality off, there is no narration.",
                "narrator",
                "the narrator",
                s => s.Callouts.Narrator,
                (s, v) => s with { Callouts = s.Callouts with { Narrator = v } },
                appliesWhen: s => LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId),
        ]);

        rows.Add(new SettingRow
        {
            Key = SessionLengthHoursKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, with Break reminder on, and "
                + "set Remind me to take a break after (hours) to 2, 3, 4 or 6. After that many hours of one "
                + "session D47 reminds you to take a break, once. The row is absent while Break reminder is off.",
            Advanced = true,
            Label = "Remind me to take a break after (hours)",
            Help = "How long a session runs before the break reminder is said.",
            Kind = SettingKind.Choice,
            Choices = ["2", "3", "4", "6"],
            DefaultDisplay = "4",
            DocsAnchor = "session-length",
            AppliesWhen = s => s.Callouts is { Enabled: true, SessionLength: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.SessionLengthHours.ToString(CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        SessionLengthHours = v is "2" or "3" or "4" or "6"
                            ? int.Parse(v, CultureInfo.InvariantCulture)
                            : s.Callouts.SessionLengthHours,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = AmbientSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, with In Ship chatter on, "
                + "and set The least time between In Ship chatter (seconds) to 30 and the most to 30. Fly "
                + "quietly: a chatter line arrives about every thirty seconds. Set it to 0 and the chatter stops. "
                + "The row is absent without a language model.",
            Advanced = true,

            // Reworded twice, and the property name deliberately not either time: for its new sibling (#258),
            // and again on 2026-09-02 when the pair took the name of who is speaking.
            Label = "The least time between In Ship chatter (seconds)",
            Help = "Between two lines from your core or your crew. Each gap lands "
                   + "somewhere between this and the row below, so the chatter never ticks like a "
                   + "clock. Lower is a talkative companion; higher is a quiet one; 0 silences it. "

                   // The floor between the two kinds (#257), said on the row it clamps against.
                   + "The two kinds are also kept apart from each other, so a line from inside "
                   + "your ship and an exchange from outside it never arrive back to back - and "
                   + "neither is ever held apart by longer than this row.",
            Kind = SettingKind.Number,
            DefaultDisplay = "300",
            DocsAnchor = "ambient",
            AppliesWhen = s => s.Callouts is { Enabled: true, Ambient: true }
                               && LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.AmbientSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    // Four hours is the ceiling, which is what the old row's 240 minutes was.
                    Callouts = s.Callouts with { AmbientSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().AmbientSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = AmbientMaxSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, with In Ship chatter on, "
                + "and set The most time between In Ship chatter (seconds) to 60 with the least at 30. Fly "
                + "quietly: gaps between chatter lines fall between thirty and sixty seconds and never exceed "
                + "sixty. The row is absent without a language model.",
            Advanced = true,
            Label = "The most time between In Ship chatter (seconds)",
            Help = "Between two lines from your core or your crew. Equal to the "
                   + "row above pins a fixed cadence; anything below it reads as equal.",
            Kind = SettingKind.Number,
            DefaultDisplay = "600",
            DocsAnchor = "ambient",
            AppliesWhen = s => s.Callouts is { Enabled: true, Ambient: true }
                               && LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.AmbientMaxSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    // The row above's rules: four hours is the ceiling, and a value that will not parse falls
                    // back to the default rather than to zero.
                    Callouts = s.Callouts with { AmbientMaxSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().AmbientMaxSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = NpcChatterSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, with NPC chatter on, and "
                + "set The least time between NPC chatter (seconds) to 30 and the most to 30. Fly in a busy "
                + "system: an exchange arrives about every thirty seconds. Set it to 0 and the chatter stops. The "
                + "row is absent without a language model.",
            Advanced = true,
            Label = "The least time between NPC chatter (seconds)",
            Help = "Between two exchanges among people outside your ship. Each gap "
                   + "lands somewhere between this and the row below, so the chatter never ticks "
                   + "like a clock; 0 silences it. "

                   // The In Ship row's clause, from this side of the boundary (#257).
                   + "The two kinds are also kept apart from each other, so an exchange and a "
                   + "line from inside your ship never arrive back to back - and neither is ever "
                   + "held apart by longer than this row.",
            Kind = SettingKind.Number,
            DefaultDisplay = "300",
            DocsAnchor = "npc-chatter",
            AppliesWhen = s => s.Callouts is { Enabled: true, NpcChatter: true }
                               && LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.NpcChatterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    // The ambient row's rules (#245's sibling above): four hours is the ceiling, and a value
                    // that will not parse falls back to the default rather than to zero, since zero is the
                    // one value that means silence.
                    Callouts = s.Callouts with { NpcChatterSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().NpcChatterSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = NpcChatterMaxSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Chatter and messages, group Chatter, with NPC chatter on, and "
                + "set The most time between NPC chatter (seconds) to 60 with the least at 30. Fly in a busy "
                + "system: gaps between exchanges fall between thirty and sixty seconds and never exceed sixty. "
                + "The row is absent without a language model.",
            Advanced = true,
            Label = "The most time between NPC chatter (seconds)",
            Help = "Between two exchanges among people outside your ship. Equal to "
                   + "the row above pins a fixed cadence; anything below it reads as equal.",
            Kind = SettingKind.Number,
            DefaultDisplay = "600",
            DocsAnchor = "npc-chatter",
            AppliesWhen = s => s.Callouts is { Enabled: true, NpcChatter: true }
                               && LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.NpcChatterMaxSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with { NpcChatterMaxSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().NpcChatterMaxSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = NarratorSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Narrator and stories, group Narrator, with Narrator on, and set "
                + "The least time between narrations (seconds) to 60 and the most to 60. In a lull the Narrator "
                + "speaks about once a minute. Set it to 0 and the Narrator goes silent.",
            Advanced = true,
            Label = "The least time between narrations (seconds)",
            Help = "Each gap lands somewhere between this and the row below; 0 silences the Narrator.",
            Kind = SettingKind.Number,
            DefaultDisplay = "1800",
            DocsAnchor = "narrator",
            AppliesWhen = s => s.Callouts is { Enabled: true, Narrator: true }
                               && LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.NarratorSeconds.ToString(CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with { NarratorSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().NarratorSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = NarratorMaxSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Other voices › Narrator and stories, group Narrator, with Narrator on, and set "
                + "The most time between narrations (seconds) to 120 with the least at 60. In a lull the gaps "
                + "between narrations fall between one and two minutes. Setting it below the least reads as equal "
                + "to it.",
            Advanced = true,
            Label = "The most time between narrations (seconds)",
            Help = "Equal to the row above pins a fixed cadence; anything below it reads as equal.",
            Kind = SettingKind.Number,
            DefaultDisplay = "3600",
            DocsAnchor = "narrator",
            AppliesWhen = s => s.Callouts is { Enabled: true, Narrator: true }
                               && LlmProviderCatalog.Selected(s.Llm.Provider).Id != LlmProviderCatalog.NoneId,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.NarratorMaxSeconds.ToString(CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with { NarratorMaxSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().NarratorMaxSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = RouteEveryKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, with Route progress on, and set "
                + "Report route progress every N jumps to 2. Plot a route of six jumps and make them: D47 reports "
                + "progress after every second jump. Set it to 0 and the progress reports stop.",
            Advanced = true,
            Label = "Report route progress every N jumps",
            Help = "Every 3 is reassuring on a short trip and unbearable on a long one; 0 silences it.",
            Kind = SettingKind.Number,
            DefaultDisplay = "3",
            DocsAnchor = "route-interval",
            AppliesWhen = s => s.Callouts is { Enabled: true, Route: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.RouteEveryNJumps.ToString(),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        RouteEveryNJumps = int.TryParse(v, out var jumps) && jumps >= 0
                            ? jumps
                            : s.Callouts.RouteEveryNJumps,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = LimpetCargoFloorKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, with "
                + "Limpet reminders on, and set Only remind me about limpets above to 64. Dock at a station that "
                + "sells limpets in a ship with a hold over 64 tonnes and few limpets aboard: D47 reminds you. In "
                + "a ship with a hold of 64 tonnes or less it is silent.",
            Advanced = true,
            Label = "Only remind me about limpets above",
            Help = "Cargo capacity, in tonnes. Below it you are not running limpets and the reminder is noise.",
            Kind = SettingKind.Number,
            DefaultDisplay = "64",
            DocsAnchor = "limpet-floor",
            AppliesWhen = s => s.Callouts is { Enabled: true, Limpets: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.LimpetCargoFloor.ToString(),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        LimpetCargoFloor = int.TryParse(v, out var tonnes) && tonnes >= 0
                            ? tonnes
                            : s.Callouts.LimpetCargoFloor,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = LimpetPercentKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Mining and materials, group Mining and material callouts, with "
                + "Limpet reminders on, and set Remind me when limpets are under to 5. In a 256 tonne hold, dock "
                + "where limpets are sold with 12 limpets aboard: D47 reminds you. With 13 or more it is silent.",
            Advanced = true,
            Label = "Remind me when limpets are under",

            // The denominator is on the row, in words.
            Help = "As a percentage of your cargo capacity. 5 means 12 limpets in a 256 tonne hold is low.",
            Kind = SettingKind.Number,
            DefaultDisplay = "5",
            DocsAnchor = "limpet-percent",
            AppliesWhen = s => s.Callouts is { Enabled: true, Limpets: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.LimpetPercent.ToString(),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        LimpetPercent = int.TryParse(v, out var percent) && percent is >= 0 and <= 100
                            ? percent
                            : s.Callouts.LimpetPercent,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = BodyValueThresholdKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, with Valuable bodies on, "
                + "and set Call out bodies from (credits) to 500000. Scan a planet worth less than that mapped: "
                + "nothing is said. Scan one worth more: D47 says so with about what mapping pays.",
            Advanced = true,
            Label = "Call out bodies from (credits)",
            Help = "The least a planet would pay if mapped efficiently before it is said when scanned.",
            Kind = SettingKind.Number,
            DefaultDisplay = "500000",
            DocsAnchor = "body-value-threshold",
            AppliesWhen = s => s.Callouts is { Enabled: true, BodyValue: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.BodyValueThreshold.ToString(CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        BodyValueThreshold =
                            long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var credits)
                            && credits >= 0
                                ? credits
                                : s.Callouts.BodyValueThreshold,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = BiologyThresholdKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, and set Call out biology "
                + "from (credits) to 10000000. Scan a landable body whose biology could reach less than that: "
                + "nothing is said. Scan one that could reach more: D47 says so. The row appears while High-value "
                + "biology or Surveyed biology on arrival is on.",
            Advanced = true,
            Label = "Call out biology from (credits)",
            Help = "The least a body's biology could reach at best, or was surveyed at, before it is said.",
            Kind = SettingKind.Number,
            DefaultDisplay = "10000000",
            DocsAnchor = "biology-threshold",
            AppliesWhen = s => s.Callouts is { Enabled: true } callouts
                && (callouts.Biology || (callouts.SurveyedBiology && s.Knowledge.GalaxySearch)),
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.BiologyThreshold.ToString(CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        BiologyThreshold =
                            long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var credits)
                            && credits >= 0
                                ? credits
                                : s.Callouts.BiologyThreshold,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = TradingModeMinHoldKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Callouts, group Plans and reminders, with Trading Mode on, and set "
                + "Trading Mode requires a cargo capacity of at least (tonnes) to 25. Dock with a route plotted "
                + "in a ship with 25 or more tonnes free of limpets: D47 names the best cargo to buy. In a ship "
                + "with less, Trading Mode says nothing. The row appears only while Look things up in the galaxy "
                + "is on.",
            Advanced = true,
            Label = "Trading Mode requires a cargo capacity of at least (tonnes)",
            Help = "1 to 50: cargo capacity less the limpets aboard. Below this, Trading Mode says nothing.",
            Kind = SettingKind.Number,
            DefaultDisplay = "25",
            DocsAnchor = "trading-mode-min-hold",
            AppliesWhen = s => s.Callouts is { Enabled: true, TradingMode: true } && s.Knowledge.GalaxySearch,
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.TradingModeMinHold.ToString(CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        TradingModeMinHold =
                            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tonnes)
                            && tonnes is >= TradingModeCallout.LeastMinHold and <= TradingModeCallout.MostMinHold
                                ? tonnes
                                : s.Callouts.TradingModeMinHold,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = LongJumpSecondsKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › In flight, group Flight callouts, with Hyperspace tunnel delays "
                + "on, and set Hyperspace Tunnel Delay (in seconds) to 30. Start a jump and stay in the tunnel "
                + "past thirty seconds from entering hyperspace: D47 makes a remark. A jump shorter than that is "
                + "silent.",
            Advanced = true,
            Label = "Hyperspace Tunnel Delay (in seconds)",
            Help = "Measured from entering hyperspace rather than from starting the jump.",
            Kind = SettingKind.Number,
            Step = 0.5,
            DefaultDisplay = "30",
            DocsAnchor = "long-jump-threshold",
            AppliesWhen = s => s.Callouts is { Enabled: true, LongJump: true },
            Binding = new SettingBinding
            {
                // Invariant on both sides.
                Read = s => s.Callouts.LongJumpSeconds.ToString("0.#", CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        LongJumpSeconds =
                            double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                            && seconds > 0
                                ? seconds
                                : s.Callouts.LongJumpSeconds,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = HomeSystemKey,
            Exercise =
                "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                + "open Settings › Your core › Exploring, group Exploration callouts, with Arrivals on, and enter "
                + "your home system in Home system. Arrive in that system: D47 names it as home. Clear the row "
                + "and the home remark stops. The row appears only while Arrivals is on.",
            Advanced = true,
            Label = "Home system",
            Help = "Named for the arrival callout. There is no default — no journal event reports where you call home.",
            Kind = SettingKind.Text,
            NamesASystem = true,
            DefaultDisplay = "(none)",
            DocsAnchor = "home-system",
            Scope = SettingScope.Commander,
            AppliesWhen = s => s.Callouts is { Enabled: true, Arrival: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.HomeSystem,
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        HomeSystem = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
                    },
                },
            },
        });

        return rows;
    }

    private static SettingRow Toggle(
        string key,
        string exercise,
        string label,
        string help,
        string anchor,
        string subject,
        Func<D47Settings, bool> read,
        Func<D47Settings, bool, D47Settings> write,

        // Every callout that fires because the game said something is on.
        bool defaultOn = true,

        // A parameter for the same reason defaultOn is: almost every callout applies whenever callouts do,
        // and the ones that need a model (#245) say so at their call site.
        Func<D47Settings, bool>? appliesWhen = null) => new()
    {
        Key = key,
        Exercise = exercise,
        Advanced = true,
        Label = label,
        Help = help,
        Kind = SettingKind.Toggle,
        DefaultDisplay = defaultOn ? "on" : "off",
        DocsAnchor = anchor,
        Group = "What D47 speaks up about",

        // Protected, like the master switch and for the same reason: anything the model can call, a hostile
        // in-game message can attempt to invoke.
        Protected = true,

        // A protected row is unreachable from the tool surface by design, so without a phrase here it cannot
        // be set by voice at all — and nothing would report that.
        Commands =
        [
            new SettingCommandPhrase($"stop warning me about {subject}", "false"),
            new SettingCommandPhrase($"stop calling out {subject}", "false"),
            new SettingCommandPhrase($"start warning me about {subject}", "true"),
            new SettingCommandPhrase($"start calling out {subject}", "true"),
        ],
        AppliesWhen = s => s.Callouts.Enabled && (appliesWhen?.Invoke(s) ?? true),
        Binding = SettingBinding.Toggle(read, write),
    };

    /// <summary>
    /// The spoken and rendered answer to "what are you watching for", projected from the engine rather
    /// than from a list written here — a second list is a second thing to go stale.
    /// </summary>
    public static string Describe(CalloutEngine engine, D47Settings settings)
    {
        if (!settings.Callouts.Enabled)
        {
            return "I am not speaking up about anything right now — callouts are switched off.";
        }

        var report = new System.Text.StringBuilder("I speak up about:");

        foreach (var callout in engine.Callouts)
        {
            report.AppendLine();
            report.Append($"  {callout.Id}: {(engine.IsEnabled(callout.Id) ? "on" : "off")}");
        }

        if (settings.Callouts is { Route: true, RouteEveryNJumps: > 0 } callouts)
        {
            report.AppendLine();
            report.Append($"Route progress every {callouts.RouteEveryNJumps} jumps.");
        }

        if (settings.Callouts.HomeSystem is { } home)
        {
            report.AppendLine();
            report.Append($"Home system is {home}.");
        }

        return report.ToString();
    }
}
