using D47.Core.Callouts;
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
    public const string RouteEveryKey = "callouts.routeEveryNJumps";
    public const string LongJumpSecondsKey = "callouts.longJumpSeconds";
    public const string HomeSystemKey = "callouts.homeSystem";

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
                    "List the things d47 announces without being asked, and whether each one is currently on.",
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
                Help = "Off means d47 only ever answers. Every warning below stops with it.",
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
        ]);

        rows.Add(new SettingRow
        {
            Key = RouteEveryKey,
            Label = "Report route progress every",
            Help = "In jumps. Every 5 is reassuring on a short trip and unbearable on a long one; 0 silences it.",
            Kind = SettingKind.Number,
            DefaultDisplay = "5",
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
            Key = LongJumpSecondsKey,
            Label = "A jump is long after",
            Help = "In seconds, measured from entering hyperspace rather than from starting the jump.",
            Kind = SettingKind.Number,
            DefaultDisplay = "20",
            DocsAnchor = "long-jump-threshold",
            AppliesWhen = s => s.Callouts is { Enabled: true, LongJump: true },
            Binding = new SettingBinding
            {
                Read = s => s.Callouts.LongJumpSeconds.ToString("0.#"),
                Write = (s, v) => s with
                {
                    Callouts = s.Callouts with
                    {
                        LongJumpSeconds = double.TryParse(v, out var seconds) && seconds > 0
                            ? seconds
                            : s.Callouts.LongJumpSeconds,
                    },
                },
            },
        });

        rows.Add(new SettingRow
        {
            Key = HomeSystemKey,
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
        Func<D47Settings, bool, D47Settings> write) => new()
    {
        Key = key,
        Label = label,
        Help = help,
        Kind = SettingKind.Toggle,
        DefaultDisplay = "on",
        DocsAnchor = anchor,
        Group = "What d47 speaks up about",
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
