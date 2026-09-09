using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Ships;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The fleet, and what the Commander wants doing to it (Phase 26, "Ships").</summary>
public static class ShipsCapability
{
    public const string Id = "ships";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The key of the row that repairs what is remembered (#128).</summary>
    public const string RescanKey = "ships.remembered";

    /// <summary>The key of the row that decides whether the large hull art is fetched (#289).</summary>
    public const string HullArtKey = "ships.art";

    /// <summary>
    /// What only the App can do for this capability (#128): read every journal on disk again and
    /// rebuild what each ship was last seen holding.
    /// </summary>
    public sealed record ShipsSurface
    {
        /// <summary>
        /// A sentence describing the stored picture as it stands — how many ships, and how stale the
        /// oldest of them is.
        /// </summary>
        public Func<string>? Remembered { get; init; }

        /// <summary>The rescan, or null where nothing composed one.</summary>
        public Func<LongPress?>? Rescan { get; init; }

        /// <summary>Every member supplied and none of them doing anything, for a test registry to bind.</summary>
        public static ShipsSurface Inert => new()
        {
            Remembered = () => "Nothing is remembered in a test.",
            Rescan = () => (_, _) => Task.FromResult<string?>(null),
        };
    }

    /// <summary>
    /// <param name="ships"> The Commander's builds, or null under the designer and in tests that are
    /// not about them — the capability still registers, so its documentation page exists, and every
    /// tool answers that nothing is planned rather than throwing.
    /// </summary>
    /// <param name="ships">
    /// The Commander's builds, or null under the designer and in tests that are not about them — the
    /// capability still registers, so its documentation page exists, and every tool answers that
    /// nothing is planned rather than throwing.
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
                // Not get_fleet, which JournalCapability already has and which answers a different question:
                // that one reports what the journal saw in the racks, and this one reports what the Commander
                // means to do about it.
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

            // Protected.
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

    /// <summary>One row, and it is a repair rather than a preference (#128).</summary>
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

        // The pictures (#289).
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

    /// <summary>What the model is told about the fleet on every turn, below the cache breakpoint.</summary>
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
