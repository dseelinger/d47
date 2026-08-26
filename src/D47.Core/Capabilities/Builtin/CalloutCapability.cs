using D47.Core.Callouts;
using System.Globalization;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Speaking without being asked (list.md Phase 8).
/// <para>
/// It registers one tool, and that tool is read-only. The model can ask what d47 is watching
/// for; it cannot switch a warning off. That is not caution about the model — it is the trust
/// boundary: journal text and in-game messages are untrusted (architecture.md §7), and anything
/// the model can call, a hostile in-game message can attempt to invoke. A model that can
/// disable the interdiction warning is one that can be told to by the Commander interdicting
/// them.
/// </para>
/// <para>
/// So every toggle here is <see cref="SettingRow.Protected"/>: reachable from the panel, from a
/// hotkey and through the model-free keyword router, and not from the tool surface.
/// </para>
/// </summary>
public static class CalloutCapability
{
    public const string Id = "callouts";

    // No key here is a prefix of another. "callouts.route" would have been, of
    // "callouts.routeEveryNJumps" — and since one is protected and the other is not, anything
    // matching keys by prefix reads the pair as a protected row being offered to the model.
    public const string EnabledKey = "callouts.enabled";
    public const string DangerKey = "callouts.danger";
    public const string FuelKey = "callouts.fuel";
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

    public const string SamplingKey = "callouts.sampling";
    public const string AnnouncedAttackKey = "callouts.announcedAttack";
    public const string RivalTerritoryKey = "callouts.rivalTerritory";
    public const string ChecklistKey = "callouts.checklist";
    public const string RouteEveryKey = "callouts.routeEveryNJumps";
    public const string LongJumpSecondsKey = "callouts.longJumpSeconds";
    public const string HomeSystemKey = "callouts.homeSystem";
    /// <summary>
    /// One line at the start of a session, picking up where the Commander left off (list.md Phase
    /// 31). A callout row rather than an autonomous one, because it presses nothing.
    /// </summary>
    public const string ContinuityKey = "callouts.continuity";

    /// <summary>
    /// Something d47 noticed the Commander keeps doing, said when the circumstance comes round
    /// again (list.md Phase 32). The one row in this capability that ships off.
    /// </summary>
    public const string HabitsKey = "callouts.habits";

    /// <summary>A beat of the Commander's adventure, said when it is reached (list.md Phase 47).</summary>
    public const string AdventureKey = "callouts.adventure";

    public const string AmbientKey = "callouts.ambient";
    public const string AmbientSecondsKey = "callouts.ambientSeconds";

    public static CapabilityDescriptor Create(SettingsService settings, Func<string> describe) => new()
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

        // Phrases only, and each one at least three words. "callouts" alone would hijack any
        // sentence containing the word, which is the rule JournalCapability documents.
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
                Binding = new SettingBinding
                {
                    Read = s => s.Callouts.Enabled ? "true" : "false",
                    Write = (s, v) => s with { Callouts = s.Callouts with { Enabled = v is not "false" } },
                },
            },
        };

        rows.AddRange(
        [
            Toggle(
                DangerKey,
                "Danger",
                "Interdiction, shields down, hull damage, overheating and a full cargo hold.",
                "danger",
                "danger",
                s => s.Callouts.Danger,
                (s, v) => s with { Callouts = s.Callouts with { Danger = v } }),

            Toggle(
                FuelKey,
                "Fuel and range",
                "Low fuel, and a route whose next star cannot be scooped when the jump beyond it cannot be made.",
                "fuel",
                "fuel",
                s => s.Callouts.Fuel,
                (s, v) => s with { Callouts = s.Callouts with { Fuel = v } }),

            Toggle(
                RouteKey,
                "Route progress",
                "Jumps remaining, the next system, and neutron or white dwarf hazards ahead.",
                "route",
                "route progress",
                s => s.Callouts.Route,
                (s, v) => s with { Callouts = s.Callouts with { Route = v } }),

            Toggle(
                LongJumpKey,
                "Long jumps",
                "A remark when a hyperspace jump runs longer than usual.",
                "long-jump",
                "long jumps",
                s => s.Callouts.LongJump,
                (s, v) => s with { Callouts = s.Callouts with { LongJump = v } }),

            Toggle(
                ArrivalKey,
                "Arrivals",
                "Your home system, where your carrier is, ships stored here, and stations offering engineering.",
                "arrival",
                "arrivals",
                s => s.Callouts.Arrival,
                (s, v) => s with { Callouts = s.Callouts with { Arrival = v } }),

            Toggle(
                MaterialsKey,
                "Material milestones",
                "The first unit of a material, and progress towards a full stock where the cap is known.",
                "materials",
                "materials",
                s => s.Callouts.Materials,
                (s, v) => s with { Callouts = s.Callouts with { Materials = v } }),

            Toggle(
                EmissionsKey,
                "High grade emissions",
                "On arriving somewhere that could be running them: which grade 5 materials, from which "
                + "faction's state. Silent about anything you are already full of.",
                "emissions",
                "high grade emissions",
                s => s.Callouts.Emissions,
                (s, v) => s with { Callouts = s.Callouts with { Emissions = v } }),

            Toggle(
                LimpetsKey,
                "Limpet reminders",
                "On docking somewhere that sells limpets, with a big hold and few aboard. Off by default: "
                + "it is for Commanders who fly limpets.",
                "limpets",
                "limpet reminders",
                s => s.Callouts.Limpets,
                (s, v) => s with { Callouts = s.Callouts with { Limpets = v } },

                // Off, for the reason the row says. The habit callout is the other one that
                // defaults this way.
                defaultOn: false),

            Toggle(
                ProspectorKey,
                "Prospector results",
                "What a prospector limpet found in a rock, and whether it is the richest of the session.",
                "prospector",
                "prospector results",
                s => s.Callouts.Prospector,
                (s, v) => s with { Callouts = s.Callouts with { Prospector = v } }),

            Toggle(
                CoreAsteroidKey,
                "Core asteroids",
                "A core asteroid, and what is in it. Separate from prospector results because it is rare.",
                "core-asteroid",
                "core asteroids",
                s => s.Callouts.CoreAsteroid,
                (s, v) => s with { Callouts = s.Callouts with { CoreAsteroid = v } }),

            Toggle(
                SamplingKey,
                "Sampling progress",
                "How many organic specimens you have taken and how far you moved for the last one.",
                "sampling",
                "sampling progress",
                s => s.Callouts.Sampling,
                (s, v) => s with { Callouts = s.Callouts with { Sampling = v } }),

            Toggle(
                AnnouncedAttackKey,
                "Announced attacks",
                "An NPC saying it is about to interdict you or take your cargo, before it does.",
                "announced-attack",
                "announced attacks",
                s => s.Callouts.AnnouncedAttack,
                (s, v) => s with { Callouts = s.Callouts with { AnnouncedAttack = v } }),

            Toggle(
                RivalTerritoryKey,
                "Rival Power territory",
                "Flying in normal space in a system controlled by a Power other than the one you fly for.",
                "rival-territory",
                "enemy territory",
                s => s.Callouts.RivalTerritory,
                (s, v) => s with { Callouts = s.Callouts with { RivalTerritory = v } }),

            Toggle(
                ChecklistKey,
                "Checklist changes",
                "A plan item the journal has just changed its mind about, and the last unit a plan needed.",
                "checklist",
                "checklist changes",
                s => s.Callouts.Checklist,
                (s, v) => s with { Callouts = s.Callouts with { Checklist = v } }),

            Toggle(
                ContinuityKey,
                "Picking up where you left off",
                "One line at the start of a session: how long it has been, where you were, and what your "
                + "plans were waiting on. Silent when there is nothing to say.",
                "continuity",
                "where I left off",
                s => s.Callouts.Continuity,
                (s, v) => s with { Callouts = s.Callouts with { Continuity = v } }),

            Toggle(
                HabitsKey,
                "Things you keep doing",
                "Something D47 has noticed in your own journals, said when the situation it is about comes "
                + "round again. Off until you switch it on, and every claim can be dropped for good.",
                "habits",
                "my habits",
                s => s.Callouts.Habits,
                (s, v) => s with { Callouts = s.Callouts with { Habits = v } },

                // The only one off by default (list.md Phase 32, item 3). It fires because of a
                // claim about the Commander rather than because the game said something, and the
                // item is explicit that this changes the deal.
                defaultOn: false),

            Toggle(
                AdventureKey,
                "Adventure beats",
                "A beat of the story you are following, said when you reach the place it waits for. Off "
                + "leaves the story in the conversation and stops it being read out.",
                "adventure",
                "the adventure",
                s => s.Callouts.Adventure,
                (s, v) => s with { Callouts = s.Callouts with { Adventure = v } }),

            Toggle(
                AmbientKey,
                "Ambient remarks",
                "The occasional in-character observation about where you are, said because nothing has happened.",
                "ambient",
                "ambient remarks",
                s => s.Callouts.Ambient,
                (s, v) => s with { Callouts = s.Callouts with { Ambient = v } }),
        ]);

        rows.Add(new SettingRow
        {
            Key = AmbientSecondsKey,
            Advanced = true,
            Label = "At most one ambient remark every",
            Help = "In seconds. Lower is a talkative companion; higher is a quiet one; 0 silences them.",
            Kind = SettingKind.Number,
            DefaultDisplay = "45",
            DocsAnchor = "ambient",
            AppliesWhen = s => s.Callouts is { Enabled: true, Ambient: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.AmbientSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Write = (s, v) => s with
                {
                    // Four hours is the ceiling, which is what the old row's 240 minutes was. A
                    // value that will not parse falls back to the default rather than to zero,
                    // since zero is the one value that means silence.
                    Callouts = s.Callouts with { AmbientSeconds = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        && seconds >= 0
                            ? Math.Min(seconds, 14400)
                            : new CalloutSettings().AmbientSeconds },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = RouteEveryKey,
            Advanced = true,
            Label = "Report route progress every",
            Help = "In jumps. Every 3 is reassuring on a short trip and unbearable on a long one; 0 silences it.",
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
            Advanced = true,
            Label = "Remind me when limpets are under",

            // The denominator is on the row, in words. A percentage whose denominator is not
            // written down is a number nobody can set confidently.
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
            Key = LongJumpSecondsKey,
            Advanced = true,
            Label = "A jump is long after",
            Help = "In seconds, measured from entering hyperspace rather than from starting the jump.",
            Kind = SettingKind.Number,
            Step = 0.5,
            DefaultDisplay = "30",
            DocsAnchor = "long-jump-threshold",
            AppliesWhen = s => s.Callouts is { Enabled: true, LongJump: true },
            Binding = new SettingBinding
            {
                // Invariant on both sides. The store parses and formats invariantly, so a
                // row reading in the machine's own culture is a row that turns 20.5 into 205
                // on any Commander whose decimal separator is a comma - and into nothing at
                // all on the way back.
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
            Advanced = true,
            Label = "Home system",
            Help = "Named for the arrival callout. There is no default — no journal event reports where you call home.",
            Kind = SettingKind.Text,
            DefaultDisplay = "(none)",
            DocsAnchor = "home-system",
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
        string label,
        string help,
        string anchor,
        string subject,
        Func<D47Settings, bool> read,
        Func<D47Settings, bool, D47Settings> write,

        // Every callout that fires because the game said something is on. Phase 32's fires because
        // of a claim d47 made about the Commander, which is a different deal and defaults the other
        // way — so the default is a parameter rather than a constant, and the one caller that
        // passes false says why.
        bool defaultOn = true) => new()
    {
        Key = key,
        Advanced = true,
        Label = label,
        Help = help,
        Kind = SettingKind.Toggle,
        DefaultDisplay = defaultOn ? "on" : "off",
        DocsAnchor = anchor,
        Group = "What D47 speaks up about",
        GroupHelp =
            "Each one is separately switchable, because finding route progress chatty is not a "
            + "reason to lose the interdiction warning.",

        // Protected, like the master switch and for the same reason: anything the model can
        // call, a hostile in-game message can attempt to invoke.
        Protected = true,

        // A protected row is unreachable from the tool surface by design, so without a phrase
        // here it cannot be set by voice at all — and nothing would report that. The subject is
        // what the Commander would call the thing, not the setting key.
        Commands =
        [
            new SettingCommandPhrase($"stop warning me about {subject}", "false"),
            new SettingCommandPhrase($"stop calling out {subject}", "false"),
            new SettingCommandPhrase($"start warning me about {subject}", "true"),
            new SettingCommandPhrase($"start calling out {subject}", "true"),
        ],
        AppliesWhen = s => s.Callouts.Enabled,
        Binding = new SettingBinding
        {
            Read = s => read(s) ? "true" : "false",
            Write = (s, v) => write(s, v is not "false"),
        },
    };

    /// <summary>
    /// The spoken and rendered answer to "what are you watching for", projected from the engine
    /// rather than from a list written here — a second list is a second thing to go stale.
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
