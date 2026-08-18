using System.Globalization;
using D47.Core.Ships;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The fleet, and what the Commander wants doing to it (list.md Phase 26, "Ships").
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

    /// <param name="ships">
    /// The Commander's builds, or null under the designer and in tests that are not about them —
    /// the capability still registers, so its documentation page exists, and every tool answers
    /// that nothing is planned rather than throwing.
    /// </param>
    public static CapabilityDescriptor Create(ShipPlanService? ships = null) => new()
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
        Keywords = ["what have I planned", "read my ship plans", "what am I building"],

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
    };

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
