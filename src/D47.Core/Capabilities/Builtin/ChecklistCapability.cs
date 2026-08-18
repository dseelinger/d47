using D47.Core.Checklists;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The Commander's checklist, and the plans that write into it (list.md Phase 17).
/// <para>
/// <b>One surface, two kinds, three groups.</b> "What am I working on" has exactly one answer, so
/// a ship's build and a system's construction show up here rather than each growing a capability
/// of its own. What stays separate underneath is how an item's "done" is decided, and that is a
/// property of where the item came from rather than something the Commander picks.
/// </para>
/// <para>
/// <b>Reading is model-callable; writing is not.</b> Every tool here that would change the
/// Commander's own list writes a <em>proposal</em> instead, into a second file, so the trust
/// boundary is inspectable by opening <c>data/</c>. Committing is
/// <see cref="ToolDefinition.Protected"/> — the panel and the model-free keyword router reach it
/// and the model does not. Journal text and in-game comms are untrusted (architecture.md §7), so
/// anything the model can call, a hostile in-game message can attempt to invoke; the worst that
/// achieves here is a proposal that gets declined.
/// </para>
/// <para>
/// <b>The distinction underneath all of it is observing versus asserting.</b> A build intent
/// naming a blueprint and a grade is checked against the <c>Loadout</c> and the result is simply
/// stated, because that is reading the journal back to the Commander. A line they wrote in their
/// own words is something no table can settle, so d47 proposes and waits.
/// </para>
/// </summary>
public static class ChecklistCapability
{
    public const string Id = "checklists";

    public const string SummaryKey = "checklists.summary";

    private static readonly string[] Groups = ["universal", "ship", "system", "suit", "weapon"];

        /// <param name="ships">
    /// The Commander's ship builds (list.md Phase 26). <c>plan_ship_build</c> writes there rather
    /// than proposing straight to the checklist, because the plan owns <em>what</em> and the
    /// checklist owns <em>when</em>. Null under the designer and in tests that are not about
    /// builds, and the tool then says it is tracking none rather than quietly doing the old thing
    /// — a tool that behaves differently depending on how it was wired is one nobody can reason
    /// about.
    /// </param>
    public static CapabilityDescriptor Create(
        ChecklistService checklists, Ships.ShipPlanService? ships = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Checklists",
        Summary = "One list of what you are working on — your own lines, your ship builds and your construction sites.",
        Examples =
        [
            "what am I working on",
            "add buy limpets to my checklist",
            "plan grade 5 dirty drives on the thrusters",
            "what do my plans still need",
        ],

        // Phrases, never bare words. "checklist" alone would hijack any sentence containing it,
        // which is the rule JournalCapability documents and the reason the router is phrase-level.
        Keywords =
        [
            "what am i working on",
            "read my checklist",
            "what is on my checklist",
            "my checklist",
            "what am i building",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Checklists", Order = 58 },
        Settings = [SummaryRow(checklists)],
        Tools =
        [
            // Argument-free and first, so "what am I working on" reaches it through the keyword
            // router with no model in the path.
            new ToolDefinition
            {
                Name = "get_checklist",
                Description =
                    "Read the Commander's checklist: open items first, then what is done, then anything "
                    + "waiting for them to accept. Derived items carry what the journal says about them "
                    + "right now. Filter by group, by name, by state or by kind.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "group",
                        Type = ToolParameterType.String,
                        Description =
                            "Which list: universal, ship, system, suit or weapon. Omitted shows all of "
                            + "them. With no name, each means the one the Commander is in or carrying "
                            + "right now.",
                        AllowedValues = Groups,
                    },
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description =
                            "A specific ship id, star system, suit id or weapon id, when the group is not "
                            + "the current one.",
                    },
                    new ToolParameter
                    {
                        Name = "state",
                        Type = ToolParameterType.String,
                        Description = "Only the open items, or only the finished ones.",
                        AllowedValues = ["open", "complete"],
                    },
                    new ToolParameter
                    {
                        Name = "kind",
                        Type = ToolParameterType.String,
                        Description =
                            "Only the Commander's own lines (authored), only the computed ones (derived), "
                            + "or one plan's — engineeringPlan, colonisationPlan or onFootPlan.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(checklists.Report(
                    arguments.TryGetString("group", out var group) ? group : null,
                    arguments.TryGetString("name", out var name) ? name : null,
                    arguments.TryGetString("state", out var state) ? state : null,
                    arguments.TryGetString("kind", out var kind) ? kind : null))),
            },

            new ToolDefinition
            {
                Name = "get_plan_shortfall",
                Description =
                    "What every live plan still needs, netted across all of them at once: exact material "
                    + "totals against what the Commander holds, storage caps that force more than one "
                    + "trip, engineer ranks that block a grade outright, where several of the shortfall "
                    + "can be gathered in one trip, and what a construction site still wants delivered.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(checklists.Shortfall())),
            },

            new ToolDefinition
            {
                Name = "add_to_checklist",
                Description =
                    "Propose adding a line in the Commander's own words. It is not added until they "
                    + "agree — D47 cannot write their list itself.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "text",
                        Type = ToolParameterType.String,
                        Description = "The line, as the Commander would say it.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "group",
                        Type = ToolParameterType.String,
                        Description = "Which list it belongs on. Defaults to universal.",
                        AllowedValues = Groups,
                    },
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "A specific ship id or star system, when it is not the current one.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                    checklists.ProposeAdd(
                        Scope(checklists, arguments),
                        [arguments.TryGetString("text", out var text) ? text : string.Empty]))),
            },

            new ToolDefinition
            {
                Name = "propose_checklist_change",
                Description =
                    "Propose that one of the Commander's own lines is finished, is open again, or should "
                    + "go. Only their own lines: a computed item's state is read out of the journal and "
                    + "simply stated, so there is nothing there to agree to.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "item",
                        Type = ToolParameterType.String,
                        Description = "The line, in enough of its own words to pick it out.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "change",
                        Type = ToolParameterType.String,
                        Description = "What to propose.",
                        AllowedValues = ["done", "open", "remove"],
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                    checklists.ProposeChange(
                        arguments.TryGetString("item", out var item) ? item : string.Empty,
                        arguments.TryGetString("change", out var change) ? change?.ToLowerInvariant() switch
                        {
                            "open" => ProposalKind.Reopen,
                            "remove" => ProposalKind.Remove,
                            _ => ProposalKind.Complete,
                        } : ProposalKind.Complete))),
            },

            new ToolDefinition
            {
                Name = "plan_ship_build",
                Description =
                    "Set what a ship's build wants in one slot: a blueprint, a grade, an engineer, an "
                    + "experimental effect — any may be left out, and left out means \"any\" rather than "
                    + "\"unknown\". Other slots are untouched. It does not reach the checklist until the "
                    + "Commander promotes the build.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "slot",
                        Type = ToolParameterType.String,
                        Description =
                            "The slot or the module — \"MainEngines\", \"thrusters\", \"Slot01_Size4\".",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "blueprint",
                        Type = ToolParameterType.String,
                        Description = "A blueprint by name — \"Dirty Drive Tuning\". Omit for any.",
                    },
                    new ToolParameter
                    {
                        Name = "grade",
                        Type = ToolParameterType.Integer,
                        Description = "1 to 5. Omit for any grade — that is a wildcard, not an unknown.",
                    },
                    new ToolParameter
                    {
                        Name = "engineer",
                        Type = ToolParameterType.String,
                        Description =
                            "Who would roll it. Naming one is what lets D47 quote an exact roll count and "
                            + "say when a grade is out of rank reach entirely.",
                    },
                    new ToolParameter
                    {
                        Name = "experimental",
                        Type = ToolParameterType.String,
                        Description = "An experimental effect, which becomes its own item on the same slot.",
                    },
                    new ToolParameter
                    {
                        Name = "ship",
                        Type = ToolParameterType.String,
                        Description =
                            "A ship id, name, or a hull they do not own yet. Omit for the one they fly.",
                    },
                    new ToolParameter
                    {
                        Name = "drop",
                        Type = ToolParameterType.Boolean,
                        Description = "Say nothing about this slot. What it already produced is kept.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Build(checklists, ships, arguments))),
            },

            new ToolDefinition
            {
                Name = "plan_on_foot_build",
                Description =
                    "Propose what a suit's or a hand weapon's plan should say: a grade to buy at Pioneer "
                    + "Supplies, and modifications to have an engineer fit. The grade always comes first "
                    + "in the list, because a grade 1 item has no modification slots and an engineer's "
                    + "base has no Pioneer Supplies. Modifications are permanent and there are four at "
                    + "most, so propose them one at a time rather than in a batch. Totals are exact — "
                    + "nothing on foot is rolled.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "equipment",
                        Type = ToolParameterType.String,
                        Description =
                            "The suit or weapon by name — \"Maverick\", \"Dominator\", \"Karma AR-50\".",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "grade",
                        Type = ToolParameterType.Integer,
                        Description =
                            "The grade to reach, 2 to 5. Omit to leave the grade alone — on foot that "
                            + "means no upgrade rather than any upgrade.",
                    },
                    new ToolParameter
                    {
                        Name = "modification",
                        Type = ToolParameterType.String,
                        Description =
                            "One modification to fit — \"Night Vision\", \"Magazine Size\". Permanent, so "
                            + "one per proposal.",
                    },
                    new ToolParameter
                    {
                        Name = "item",
                        Type = ToolParameterType.String,
                        Description =
                            "A suit id or weapon id. Omit for the suit being worn, or the one weapon "
                            + "being carried.",
                    },
                    new ToolParameter
                    {
                        Name = "weapon",
                        Type = ToolParameterType.Boolean,
                        Description = "True when this is about a hand weapon rather than the suit.",
                    },
                    new ToolParameter
                    {
                        Name = "drop",
                        Type = ToolParameterType.Boolean,
                        Description =
                            "Propose that the plan say nothing about this. What it already said is kept "
                            + "as history rather than deleted.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(OnFoot(checklists, arguments))),
            },

            new ToolDefinition
            {
                Name = "plan_colonisation",
                Description =
                    "Propose what a system's construction plan should say about one place — a body or an "
                    + "orbital slot — and what goes there. D47 holds the plan and counts what the depot "
                    + "says is still owed; it cannot tell you what a facility costs, what it will do to "
                    + "the system, or what order to build in, because nobody publishes those figures in a "
                    + "form it can ship.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "place",
                        Type = ToolParameterType.String,
                        Description = "The body or orbital slot the facility goes at.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "facility",
                        Type = ToolParameterType.String,
                        Description = "What goes there, in the Commander's words.",
                    },
                    new ToolParameter
                    {
                        Name = "system",
                        Type = ToolParameterType.String,
                        Description = "The star system. Omit for the one the Commander is in.",
                    },
                    new ToolParameter
                    {
                        Name = "drop",
                        Type = ToolParameterType.Boolean,
                        Description = "Propose that the plan say nothing about this place.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Colonise(checklists, arguments))),
            },

            // The committing half. Protected, so it is never advertised and is refused outright
            // if the model names it anyway — the panel and the phrases below are the only ways in.
            new ToolDefinition
            {
                Name = "accept_proposal",
                Description =
                    "Accept what D47 proposed for the checklist. The Commander's own act: not offered to "
                    + "the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "id",
                        Type = ToolParameterType.String,
                        Description = "One proposal by id. Omit for everything waiting.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("accept the proposal", Nothing),
                    new ToolCommandPhrase("accept the proposals", Nothing),
                    new ToolCommandPhrase("accept that", Nothing),
                    new ToolCommandPhrase("add it to my checklist", Nothing),
                    new ToolCommandPhrase("do it then", Nothing),
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                    checklists.Accept(arguments.TryGetString("id", out var id) ? id : null))),
            },

            new ToolDefinition
            {
                Name = "decline_proposal",
                Description =
                    "Decline what D47 proposed for the checklist. The Commander's own act, like accepting.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "id",
                        Type = ToolParameterType.String,
                        Description = "One proposal by id. Omit for everything waiting.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("decline the proposal", Nothing),
                    new ToolCommandPhrase("decline the proposals", Nothing),
                    new ToolCommandPhrase("leave my checklist alone", Nothing),
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                    checklists.Decline(arguments.TryGetString("id", out var id) ? id : null))),
            },
        ],
    };

    /// <summary>
    /// A declared phrase carries the arguments it means, and these mean none — "accept that"
    /// answers the question d47 just asked, which is every proposal waiting.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The live half, for prompt position 7. Null when there is nothing on the list, so a
    /// Commander who has never used this is not billed for an empty section every turn.
    /// </summary>
    public static string? Live(ChecklistService checklists)
    {
        var document = checklists.Document;
        var open = document.Items.Count(item => item.IsLive && !item.IsComplete);
        var waiting = checklists.Proposals.PendingFor(document.CommanderFid).Count;

        if (open == 0 && waiting == 0)
        {
            return null;
        }

        var said = $"The Commander has {open} open checklist item{(open == 1 ? string.Empty : "s")}.";

        return waiting == 0
            ? said
            : said + $" {waiting} proposal{(waiting == 1 ? " is" : "s are")} waiting for them to accept or decline; "
                   + "D47 cannot accept on their behalf.";
    }

    private static ChecklistScope Scope(ChecklistService checklists, ToolArguments arguments) =>
        checklists.ScopeFor(
            arguments.TryGetString("group", out var group) ? group : null,
            arguments.TryGetString("name", out var name) ? name : null);

    /// <summary>
    /// Writes one slot of a ship's build (list.md Phase 26, "A plan reaches the checklist when you
    /// say so").
    /// <para>
    /// <b>It used to propose straight to the checklist, and no longer does.</b> The plan owns
    /// <em>what</em> and the checklist owns <em>when</em>, so planning a slot changes the build
    /// and nothing else — reaching the list is <c>promote_ship_plan</c>, which is a separate act
    /// the Commander performs. That also makes this tool shorter to reason about: it says what a
    /// ship should be, and stops.
    /// </para>
    /// <para>
    /// The <c>ship</c> parameter is where a hull the Commander does not own starts an intended
    /// build. That is one tool doing two things and it is deliberate: the advertised surface is
    /// re-billed on every turn and had 160 bytes of headroom left when this phase began, so a
    /// second tool for it was not available — see <see cref="ShipsCapability"/>.
    /// </para>
    /// </summary>
    private static string Build(
        ChecklistService checklists, Ships.ShipPlanService? ships, ToolArguments arguments)
    {
        if (!arguments.TryGetString("slot", out var slot) || string.IsNullOrWhiteSpace(slot))
        {
            return "No slot was named.";
        }

        if (ships is null)
        {
            return "I am not tracking ship builds.";
        }

        var named = arguments.TryGetString("ship", out var ship) && !string.IsNullOrWhiteSpace(ship)
            ? ship.Trim()
            : null;

        if (Ships.ShipPlanService.Which(ships, named) is not { } build)
        {
            return named is null
                ? "I do not know which ship that is - no Loadout has arrived yet, and none was named."
                : $"I could not tell which ship \"{named}\" means, and it is not a hull I know of either.";
        }

        // The slot is named the way the plan keys it rather than the way the Commander said it,
        // so "thrusters" replaces what the build already says about MainEngines instead of
        // sitting beside it.
        var canonical = checklists.SlotFor(slot);

        if (arguments.TryGetBoolean("drop", out var drop) && drop)
        {
            return ships.Clear(build.Id, canonical)
                ? $"{build.Describe()}: nothing is planned for {canonical} now. What it already put "
                  + "on your checklist is kept."
                : $"{build.Describe()} had nothing planned for {canonical}.";
        }

        var plan = new Ships.SlotPlan(
            canonical,
            arguments.TryGetString("blueprint", out var blueprint) ? blueprint : null,
            arguments.TryGetInt32("grade", out var grade) ? grade : null,
            arguments.TryGetString("engineer", out var engineer) ? engineer : null,
            arguments.TryGetString("experimental", out var experimental) ? experimental : null);

        if (plan.IsEmpty)
        {
            return "That says nothing about the slot. What should go in it?";
        }

        return ships.Plan(build.Id, plan)
            ? $"{build.Describe()}: {canonical} is planned for {plan.Describe()}. It is not on your "
              + "checklist until you say so."
            : "I could not write that down.";
    }

    private static string OnFoot(ChecklistService checklists, ToolArguments arguments)
    {
        if (!arguments.TryGetString("equipment", out var equipment) || string.IsNullOrWhiteSpace(equipment))
        {
            return "No suit or weapon was named.";
        }

        var weapon = arguments.TryGetBoolean("weapon", out var isWeapon) && isWeapon;
        var group = weapon ? "weapon" : "suit";

        var scope = checklists.ScopeFor(
            group, arguments.TryGetString("item", out var item) ? item : null);

        if (scope.Group is not (ChecklistGroup.Suit or ChecklistGroup.Weapon))
        {
            return weapon
                ? "I do not know which weapon that is — no on-foot loadout has arrived yet with exactly "
                  + "one weapon in it, and none was named."
                : "I do not know which suit that is — I have not seen you on foot yet, and none was named.";
        }

        var dropping = arguments.TryGetBoolean("drop", out var drop) && drop;

        var grade = arguments.TryGetInt32("grade", out var wanted) ? wanted : (int?)null;
        var modification = arguments.TryGetString("modification", out var fit) ? fit : null;

        if (!dropping && grade is null && string.IsNullOrWhiteSpace(modification))
        {
            return "A plan for a suit or a weapon needs a grade or a modification.";
        }

        IReadOnlyList<ChecklistItem> items = dropping
            ? []
            : OnFootPlan.Items(
                scope,
                new OnFootRequest(
                    equipment, grade, modification is { Length: > 0 } ? [modification] : null));

        var said = checklists.ProposePlan(scope, ChecklistSource.OnFootPlan, items, [equipment]);

        return dropping
            ? said
            : said + " Modifications are permanent: four slots at most, and a wrong one is "
              + "recoverable only by buying and re-upgrading a fresh item.";
    }

    private static string Colonise(ChecklistService checklists, ToolArguments arguments)
    {
        if (!arguments.TryGetString("place", out var place) || string.IsNullOrWhiteSpace(place))
        {
            return "No place was named.";
        }

        var scope = checklists.ScopeFor("system", arguments.TryGetString("system", out var system) ? system : null);

        if (scope.Group != ChecklistGroup.System)
        {
            return "I do not know which system that is — no location has arrived yet, and none was named.";
        }

        var dropping = arguments.TryGetBoolean("drop", out var drop) && drop;

        if (!dropping && !arguments.TryGetString("facility", out _))
        {
            return "A place needs something to go on it.";
        }

        IReadOnlyList<ChecklistItem> items = dropping
            ? []
            : ColonisationPlan.Items(
                scope,
                [new FacilityRequest(place, arguments.TryGetString("facility", out var facility) ? facility! : place)]);

        var said = checklists.ProposePlan(scope, ChecklistSource.ColonisationPlan, items, [place]);

        return dropping ? said : said + " " + ColonisationPlan.WhatThisCannotBe;
    }

    /// <summary>
    /// A read-only summary carrying the way into the panel. Not a value the Commander sets — what
    /// they set is a list of things to do, which is not a settings row and cannot be one.
    /// </summary>
    private static SettingRow SummaryRow(ChecklistService checklists) => new()
    {
        Key = SummaryKey,
        Label = "Your checklist",
        Help = "What you are working on: your own lines, your ship builds and your construction sites. "
               + "Computed items cannot be ticked by hand — they follow your journal.",
        Kind = SettingKind.Info,
        DocsAnchor = "the-panel",
        Binding = new SettingBinding { Read = _ => Summarise(checklists) },
    };

    private static string Summarise(ChecklistService checklists)
    {
        var document = checklists.Document;
        var live = document.Items.Where(item => item.IsLive).ToList();
        var open = live.Count(item => !item.IsComplete);
        var done = live.Count - open;

        var line = live.Count == 0
            ? "Nothing on your checklist yet."
            : $"{open} open, {done} done.";

        var waiting = checklists.Proposals.PendingFor(document.CommanderFid).Count;

        if (waiting > 0)
        {
            line += $" {waiting} waiting for you.";
        }

        return checklists.List.Problems.Count == 0
            ? line
            : line + $" {checklists.List.Problems.Count} line{(checklists.List.Problems.Count == 1 ? string.Empty : "s")} refused — open the panel to see why.";
    }
}
