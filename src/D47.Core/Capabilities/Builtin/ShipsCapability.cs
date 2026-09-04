using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Ships;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The fleet, and what the Commander wants doing to it (Phase 26, "Ships").
/// <para>
/// <b>The plan owns what and the checklist owns when.</b> Nothing here writes to the checklist:
/// <c>promote_ship_plan</c> offers, and accepting stays the Commander's own act through
/// <c>ChecklistProposals</c>.
/// </para>
/// <para>
/// <b>Every tool here is Protected, and the reason is cost as much as safety.</b> The advertised
/// surface is re-billed on every turn, and the largest profile — the SRV's, which carries that
/// vehicle's controls on top of everything else — was measured at <b>39,840</b> bytes against a
/// 40,000 ceiling before this capability existed. <c>ToolProfiles.ComfortableBytes</c> says in as
/// many words that raising the number a third time is the wrong answer, so this capability
/// advertises nothing: the one route that genuinely needs a model to understand free English is
/// <c>plan_ship_build</c>, which already existed and now writes here instead of straight to the
/// checklist. Everything else is a phrase or a press.
/// </para>
/// <para>
/// Safety says the same thing about dropping a build: a plan is often weeks of intent, the model
/// consumes untrusted text, and a hostile in-game message asking d47 to throw one away is exactly
/// the shape of thing the trust boundary exists for. Protected is about the caller, so a spoken
/// delete still works.
/// </para>
/// </summary>
public static class ShipsCapability
{
    public const string Id = "ships";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The key of the row that repairs what is remembered
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// </summary>
    public const string RescanKey = "ships.remembered";

    /// <summary>
    /// The key of the row that decides whether the large hull art is fetched
    /// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>).
    /// </summary>
    public const string HullArtKey = "ships.art";

    /// <summary>
    /// What only the App can do for this capability
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>): read every journal on
    /// disk again and rebuild what each ship was last seen holding.
    /// </summary>
    public sealed record ShipsSurface
    {
        /// <summary>
        /// A sentence describing the stored picture as it stands — how many ships, and how stale
        /// the oldest of them is. Read at draw time rather than captured, so the row says what is
        /// true now rather than what was true when the surface was assembled.
        /// </summary>
        public Func<string>? Remembered { get; init; }

        /// <summary>
        /// The rescan, or null where nothing composed one.
        /// <para>
        /// <b>A delegate that answers a press rather than a press</b>, which is the trap
        /// <c>SpeechCapability.DownloadLocalVoice</c> records: rows are built before the App has
        /// finished constructing itself, so a press asked for here would answer null and the
        /// button would be dropped permanently. Whether the delegate exists is knowable now;
        /// what it returns is not.
        /// </para>
        /// </summary>
        public Func<LongPress?>? Rescan { get; init; }

        /// <summary>
        /// Every member supplied and none of them doing anything, for a test registry to bind.
        /// <para>
        /// <b>Constructed here rather than inline at each call site</b>, so that
        /// <c>HostSurfaceTests</c> can assert every property on the record is non-null: add a
        /// member without adding it here and that test fails, at the moment the omission is made
        /// rather than at a Commander's next launch. It is the same reason a null delegate makes
        /// its row absent rather than dead — see #78.
        /// </para>
        /// </summary>
        public static ShipsSurface Inert => new()
        {
            Remembered = () => "Nothing is remembered in a test.",
            Rescan = () => (_, _) => Task.FromResult<string?>(null),
        };
    }

    /// <param name="ships">
    /// The Commander's builds, or null under the designer and in tests that are not about them —
    /// the capability still registers, so its documentation page exists, and every tool answers
    /// that nothing is planned rather than throwing.
    /// </param>
    /// <param name="surface">
    /// What the App does for this capability, or null under the designer — the row is then absent
    /// rather than present and doing nothing.
    /// </param>
    public static CapabilityDescriptor Create(
        ShipPlanService? ships = null,
        ShipsSurface? surface = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Ships",
        Summary = "Your fleet, the hulls you intend, and one build per ship.",
        Examples =
        [
            "what have I planned",
            "plan grade 5 dirty drives on the thrusters",
            "put that on my checklist",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Ships", Order = 42 },

        // Phrases, never bare words. "ships" alone would hijack any sentence containing it.
        // Each names its tool (#161): promote_ship_plan and drop_ship_plan take no required
        // argument either, and both change a plan.
        Keywords =
        [
            new("what have I planned", "get_ship_plans"),
            new("read my ship plans", "get_ship_plans"),
            new("what am I building", "get_ship_plans"),
        ],

        Tools =
        [
            new ToolDefinition
            {
                // Not get_fleet, which JournalCapability already has and which answers a different
                // question: that one reports what the journal saw in the racks, and this one
                // reports what the Commander means to do about it. Both are true at once, and a
                // Commander asking "what have I planned" is not asking where their Cobra is.
                Protected = true,
                Name = "get_ship_plans",
                Description =
                    "Every ship the Commander owns and every hull they intend to buy, with where each "
                    + "one is and how many slots its build has an opinion about.",
                Commands =
                [
                    new ToolCommandPhrase("what have I planned", Nothing),
                    new ToolCommandPhrase("read my ship plans", Nothing),
                    new ToolCommandPhrase("what am I building", Nothing),
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Fleet(ships))),
            },

            new ToolDefinition
            {
                Name = "promote_ship_plan",
                Description =
                    "Offer a ship's build to the checklist. It is a proposal: the Commander accepts, and "
                    + "one planned change produces the modification plus whatever unlocking and ranking "
                    + "it needs.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "ship",
                        Type = ToolParameterType.String,
                        Description = "Which ship, by name or hull. Omit for the one the Commander is flying.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("put that on my checklist", Nothing),
                    new ToolCommandPhrase("promote this plan", Nothing),
                    new ToolCommandPhrase("add this build to my checklist", Nothing),
                ],
                Handler = (arguments, _) => Task.FromResult(Promote(ships, arguments)),
            },

            // Protected. Weeks of intent, and the model reads untrusted text.
            new ToolDefinition
            {
                Name = "drop_ship_plan",
                Description =
                    "Drop a ship's build. The Commander's own act: not offered to the model, and refused "
                    + "if it asks. What the plan already put on the checklist is kept.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "ship",
                        Type = ToolParameterType.String,
                        Description = "Which ship, by name or hull. Omit for the one the Commander is flying.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Drop(ships, arguments)),
            },
        ],

        Settings = Rows(surface),
    };

    /// <summary>
    /// One row, and it is a repair rather than a preference
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// <para>
    /// <b>Info with a press, which is a shape that cannot be reached from the tool surface.</b>
    /// <c>SettingsService.Apply</c> refuses to write a row with no binding to write, so this needs
    /// no protected flag of its own — and it should not be reachable by a model: it is minutes of
    /// disk reading started by a sentence somebody could put in a chat channel.
    /// </para>
    /// <para>
    /// <b>It says what is stored before it offers to rebuild it</b>, because the question a
    /// Commander arrives with is <i>does this look right</i>, and a button with nothing above it
    /// cannot be answered without pressing it.
    /// </para>
    /// </summary>
    private static IReadOnlyList<SettingRow> Rows(ShipsSurface? surface) =>
    [
        new SettingRow
        {
            Key = RescanKey,
            Label = "What is fitted, remembered",
            Help =
                "What each of your ships was last seen carrying, kept in data\\loadouts.json so a "
                + "ship you last flew months ago is still answerable. It is filled in as you fly "
                + "and caught up from your journals each time D47 starts.\n\n"
                + "Not look right? Rescan. That reads every journal on disk again and rebuilds "
                + "the lot from scratch — a ship nothing in your journals supports stops existing, "
                + "and one that has been sitting there wrong is put back the way the game "
                + "described it. Nothing else is touched: your plans, your checklist and your "
                + "settings are not read and not written. It costs a few seconds and can be done "
                + "as often as you like.",
            Kind = SettingKind.Info,
            DocsAnchor = "remembered",
            PressLabel = surface?.Rescan is null ? null : "Rescan my journals",
            PressAsync = surface?.Rescan is null
                ? null
                : (progress, cancellationToken) =>
                    surface.Rescan.Invoke() is { } rescan
                        ? rescan(progress, cancellationToken)
                        : Task.FromResult<string?>(null),
            Binding = new SettingBinding
            {
                Read = _ => surface?.Remembered?.Invoke() ?? "Nothing is remembered yet.",
            },
        },

        // The pictures (#289). On the Ships card rather than under Interface with the theme and
        // the zoom, because the question it answers - "why has this ship no picture" - is asked
        // by a Commander who is already standing on the fleet page.
        new SettingRow
        {
            Key = HullArtKey,
            Label = "Hull pictures",
            Help =
                "Every ship comes with a small drawing on its card, inside the download. The large "
                + "picture on a ship's own page, and the turntable a card plays when you open it, "
                + "are far bigger — a quarter of a gigabyte for the whole fleet — so they are not "
                + "carried. D47 fetches the two files for a hull the first time you open one of "
                + "those ships, from the same GitHub release the app updates itself from, and "
                + "keeps them in data\\ships.\n\n"
                + "Off, nothing is fetched and every ship keeps the small drawing it came with. "
                + "Files you have already got stay and are still shown.",
            Kind = SettingKind.Toggle,
            DocsAnchor = "hull-art",
            EgressId = EgressDisclosure.HullArt,
            Binding = new SettingBinding
            {
                Read = s => s.Ui.HullArt ? "true" : "false",
                Write = (s, v) => s with
                {
                    Ui = s.Ui with { HullArt = bool.TryParse(v, out var on) && on },
                },
            },
        },
    ];

    /// <summary>
    /// What the model is told about the fleet on every turn, below the cache breakpoint.
    /// <para>
    /// A count and the active ship, and nothing else. The whole fleet is a tool call away and this
    /// block is re-billed every turn, so it says enough for the model to know the question is
    /// answerable and no more.
    /// </para>
    /// </summary>
    public static string? Live(ShipPlanService? ships)
    {
        if (ships is null)
        {
            return null;
        }

        var fleet = ships.Fleet();
        var planned = fleet.Count(entry => entry.Planned > 0);
        var intended = fleet.Count(entry => !entry.IsOwned);

        if (fleet.Count == 0)
        {
            return null;
        }

        var said = $"Fleet: {fleet.Count} ship{(fleet.Count == 1 ? string.Empty : "s")}";

        if (intended > 0)
        {
            said += $", {intended} of them intended rather than owned";
        }

        said += planned > 0
            ? $", {planned} with a build planned."
            : ", none with a build planned.";

        return said;
    }

    private static string Fleet(ShipPlanService? ships)
    {
        if (ships is null)
        {
            return "I am not tracking any ships.";
        }

        var fleet = ships.Fleet();

        if (fleet.Count == 0)
        {
            return "I have not seen your fleet yet. Dock somewhere with a shipyard and I will read it.";
        }

        var lines = fleet.Select(entry => entry.Planned > 0
            ? $"{entry.Describe()}, {entry.Planned} slot{(entry.Planned == 1 ? string.Empty : "s")} planned"
            : entry.Describe());

        return string.Join("\n", lines);
    }

    internal static ToolResult Intend(ShipPlanService? ships, ToolArguments arguments)
    {
        if (ships is null)
        {
            return ToolResult.Error("I am not tracking any ships.");
        }

        if (!arguments.TryGetString("hull", out var hull))
        {
            return ToolResult.Error("Which hull?");
        }

        var name = arguments.TryGetString("name", out var called) ? called : null;

        if (ships.Intend(hull, name) is not { } build)
        {
            var near = Knowledge.EliteSpecifications.NearShips(hull);

            return ToolResult.Error(near.Count > 0
                ? $"I do not know a hull called \"{hull}\". Did you mean {string.Join(", ", near)}?"
                : $"I do not know a hull called \"{hull}\".");
        }

        return ToolResult.Ok(
            $"{build.Describe()}. Buying one will point this plan at it rather than making you start again.");
    }

    private static ToolResult Promote(ShipPlanService? ships, ToolArguments arguments)
    {
        if (ships is null)
        {
            return ToolResult.Error("I am not tracking any ships.");
        }

        if (Whichever(ships, arguments)?.Build is not { } build)
        {
            return ToolResult.Error("I could not tell which ship you mean, or nothing is planned for it.");
        }

        return ToolResult.Ok(ships.Promote(build.Id));
    }

    private static ToolResult Drop(ShipPlanService? ships, ToolArguments arguments)
    {
        if (ships is null)
        {
            return ToolResult.Error("I am not tracking any ships.");
        }

        if (Whichever(ships, arguments)?.Build is not { } build)
        {
            return ToolResult.Error("I could not tell which ship you mean, or nothing is planned for it.");
        }

        return ToolResult.Ok(ships.Delete(build.Id));
    }

    /// <summary>The ship a tool call names, through the service's own matcher.</summary>
    private static FleetEntry? Whichever(ShipPlanService ships, ToolArguments arguments)
    {
        var named = arguments.TryGetString("ship", out var ship) && !string.IsNullOrWhiteSpace(ship)
            ? ship.Trim()
            : null;

        return ShipPlanService.Which(ships, named) is { } build ? ships.Entry(build.Id) : null;
    }
}
