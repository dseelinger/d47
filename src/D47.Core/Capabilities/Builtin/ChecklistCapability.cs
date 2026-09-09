using D47.Core.Checklists;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The Commander's checklist, and the plans that write into it (Phase 17).</summary>
public static class ChecklistCapability
{
    public const string Id = "checklists";

    public const string SummaryKey = "checklists.summary";

    // The Commander's words rather than the enum's (remediation.md 10, item 16).
    private static readonly string[] Groups = ["custom", "ship", "system", "suit", "weapon"];

        /// <summary><param name="ships"> The Commander's ship builds (Phase 26).</summary>
        /// <param name="ships">The Commander's ship builds (Phase 26).</param>
        /// <param name="onFoot">The Commander's suit and weapon plans (Phase 27).</param>
    public static CapabilityDescriptor Create(
        ChecklistService checklists,
        Ships.ShipPlanService? ships = null,
        Loadout.OnFootPlanService? onFoot = null) => new()
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

        // Phrases, never bare words. "checklist" alone would hijack any sentence containing it, which is the
        // rule JournalCapability documents and the reason the router is phrase-level.
        Keywords =
        [
            new("what am i working on", "get_checklist"),
            new("read my checklist", "get_checklist"),
            new("what is on my checklist", "get_checklist"),
            new("my checklist", "get_checklist"),
            new("what am i building", "get_checklist"),
        ],
        Display = new CapabilityDisplay { PanelTitle = "Checklists", Order = 63 },
        Settings = [SummaryRow(checklists)],
        Tools =
        [
            // Argument-free and first, so "what am I working on" reaches it through the keyword router with
            // no model in the path.
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
                            "Which list: custom, ship, system, suit or weapon. Omitted shows all of "
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
                    new ToolParameter
                    {
                        Name = "here",
                        Type = ToolParameterType.Boolean,
                        Description = "Only what an engineer in this system could craft today.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(checklists.Report(
                    arguments.TryGetString("group", out var group) ? group : null,
                    arguments.TryGetString("name", out var name) ? name : null,
                    arguments.TryGetString("state", out var state) ? state : null,
                    arguments.TryGetString("kind", out var kind) ? kind : null,
                    arguments.TryGetBoolean("here", out var here) && here))),
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
                        Description = "Which list it belongs on. Defaults to custom.",
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
                            "Who would craft it. Naming one is what lets D47 quote an exact count and "
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
                    "Set what a suit's or a hand weapon's plan says: a grade to buy at Pioneer Supplies, "
                    + "and modifications to have an engineer fit. The grade always comes first: a grade "
                    + "1 item has no modification slots. Modifications are permanent and there are four "
                    + "at most, so set them one at a time. It does not reach the checklist until the "
                    + "Commander promotes it.",
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
                            + "set them one at a time.",
                    },
                    new ToolParameter
                    {
                        Name = "item",
                        Type = ToolParameterType.String,
                        Description =
                            "The suit or weapon by name. Omit for the suit being worn, or the one "
                            + "weapon carried.",
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
                            "Say nothing about this any more. What it already put on the checklist is "
                            + "kept.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(OnFoot(onFoot, arguments))),
            },

            new ToolDefinition
            {
                Name = "plan_colonisation",
                Description =
                    "Propose what a system's construction plan should say about one place — a body or an "
                    + "orbital slot — and what goes there. D47 holds the plan and counts what the depot "
                    + "says is still owed; it cannot tell you what a facility costs, what it will do to "
                    + "the system, or what order to build in — nobody publishes those figures.",
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

            // The committing half.
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
                Commands = Answers(checklists, Accepting, Confirmations),
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
                Commands = Answers(checklists, Declining, Refusals),
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                    checklists.Decline(arguments.TryGetString("id", out var id) ? id : null))),
            },

            // Ordering, on the same boundary and for the same reason: the order is the Commander's answer to
            // what they are working on next, which is not a thing an in-game message gets to rearrange.
            new ToolDefinition
            {
                Name = "move_checklist_item",
                Description =
                    "Move a line up, down, to the top or to the bottom of the checklist. The "
                    + "Commander's own act: not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "to",
                        Type = ToolParameterType.String,
                        Required = true,
                        Description = "up, down, top or bottom.",
                        AllowedValues = Ends,
                    },
                    new ToolParameter
                    {
                        Name = "item",
                        Type = ToolParameterType.String,
                        Description = "The line, in enough of its own words to tell it from the others. "
                                      + "Omit for the selected one.",
                    },
                ],
                Commands = Moves,
                Handler = (arguments, _) => Task.FromResult(
                    arguments.TryGetString("to", out var to) && Ending(to) is { } move
                        ? ToolResult.Ok(checklists.Move(
                            arguments.TryGetString("item", out var item) ? item : null, move).Report)
                        : ToolResult.Error("Say up, down, top or bottom.")),
            },

            // The other level of the order (Phase 42): a whole project at a time, on the same boundary as the
            // line movers and for the same reason.
            new ToolDefinition
            {
                Name = "move_checklist_project",
                Description =
                    "Move a whole project — one ship's list, one system's, the custom list — up, "
                    + "down, to the top or to the bottom of the Commander's order. The Commander's "
                    + "own act: not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "to",
                        Type = ToolParameterType.String,
                        Required = true,
                        Description = "up, down, top or bottom.",
                        AllowedValues = Ends,
                    },
                    new ToolParameter
                    {
                        Name = "project",
                        Type = ToolParameterType.String,
                        Description = "The project by the name the list shows — a ship's name, a "
                                      + "system, or custom. Omit for the selected line's project.",
                    },
                ],
                Commands = ProjectMoves,
                Handler = (arguments, _) => Task.FromResult(
                    arguments.TryGetString("to", out var to) && Ending(to) is { } move
                        ? ToolResult.Ok(checklists.Rank(
                            arguments.TryGetString("project", out var project) ? project : null, move).Report)
                        : ToolResult.Error("Say up, down, top or bottom.")),
            },
        ],
    };

    /// <summary>The four ends, in the one spelling the schema, the phrases and the panel share.</summary>
    private static readonly string[] Ends = ["up", "down", "top", "bottom"];

    private static ChecklistMove? Ending(string said) => said.Trim().ToLowerInvariant() switch
    {
        "up" => ChecklistMove.Up,
        "down" => ChecklistMove.Down,
        "top" => ChecklistMove.Top,
        "bottom" => ChecklistMove.Bottom,
        _ => null,
    };

    /// <summary>
    /// A declared phrase carries the arguments it means, and these mean none — "accept that" answers
    /// the question d47 just asked, which is every proposal waiting.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Words that mean accept whatever else is going on.</summary>
    private static readonly string[] Accepting =
    [
        "accept", "accepted", "accept it", "accept the proposal", "accept the proposals",
        "accept that", "add it to my checklist",
    ];

    /// <summary>The same, in the other direction.</summary>
    private static readonly string[] Declining =
    [
        "decline", "declined", "decline it", "decline the proposal", "decline the proposals",
        "leave my checklist alone",

        // Clearing the queue outright, asked for in #154 alongside the decay: a Commander who has heard a
        // proposal twice and does not want it needs a way to end the question that is not answering it one at
        // a time.
        "decline everything", "decline them all", "decline all of it",
    ];

    /// <summary>
    /// Words that only mean accept while d47 is holding a question (remediation.md 10, item 10).
    /// </summary>
    private static readonly string[] Confirmations =
    [
        "yes", "yes please", "yep", "yeah", "go ahead", "go for it", "do it", "do it then",
        "please do", "confirm", "confirmed", "affirmative", "make it so",
    ];

    /// <summary>The same, for saying no to the question that is waiting.</summary>
    private static readonly string[] Refusals =
    [
        "no", "no thanks", "nope", "leave it", "leave that", "forget it", "never mind",
        "negative", "cancel that", "drop it",
    ];

    /// <summary>Every way a Commander says "move that", against the end it means.</summary>
    private static readonly IReadOnlyList<ToolCommandPhrase> Moves =
    [
        .. Saying("up", ["move it up", "move that up", "move this up", "move it up one",
                         "move the selected item up", "move the currently selected checklist item up",
                         "move the selected checklist item up", "up one"]),

        .. Saying("down", ["move it down", "move that down", "move this down", "move it down one",
                           "move the selected item down", "move the currently selected checklist item down",
                           "move the selected checklist item down", "down one"]),

        .. Saying("top", ["move it to the top", "move that to the top", "move this to the top",
                          "put it at the top", "put that at the top", "put this at the top",
                          "move it to the top of the list", "move to the top", "move to top",
                          "move the selected item to the top",
                          "move the currently selected checklist item to the top",
                          "top of the list", "straight to the top"]),

        .. Saying("bottom", ["move it to the bottom", "move that to the bottom", "move this to the bottom",
                             "put it at the bottom", "put that at the bottom", "put this at the bottom",
                             "move it to the bottom of the list", "move to the bottom", "move to bottom",
                             "move the selected item to the bottom",
                             "move the currently selected checklist item to the bottom",
                             "bottom of the list"]),
    ];

    /// <summary>
    /// The same, a project at a time (Phase 42). "This project" means the selected line's, which is the
    /// line the tab is drawing a highlight round — the named form is the tool's <c>project</c>
    /// parameter, reachable from the panel's chooser, because a declared phrase carries fixed arguments
    /// and the name is the part that varies.
    /// </summary>
    private static readonly IReadOnlyList<ToolCommandPhrase> ProjectMoves =
    [
        .. Saying("up", ["move this project up", "move that project up", "move the project up"]),

        .. Saying("down", ["move this project down", "move that project down", "move the project down"]),

        .. Saying("top", ["move this project to the top", "move that project to the top",
                          "move the project to the top", "put this project first",
                          "put this project at the top"]),

        .. Saying("bottom", ["move this project to the bottom", "move that project to the bottom",
                             "move the project to the bottom", "put this project last",
                             "put this project at the bottom"]),
    ];

    private static IEnumerable<ToolCommandPhrase> Saying(string to, IReadOnlyList<string> phrases) =>
        phrases.Select(phrase => new ToolCommandPhrase(
            phrase,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["to"] = to }));

    /// <summary>
    /// The phrases that answer a proposal: the plain ones always, the conversational ones only while
    /// there is a proposal to be answering.
    /// </summary>
    private static IReadOnlyList<ToolCommandPhrase> Answers(
        ChecklistService checklists,
        IReadOnlyList<string> always,
        IReadOnlyList<string> whileWaiting) =>
    [
        .. always.Select(phrase => new ToolCommandPhrase(phrase, Nothing)),
        .. whileWaiting.Select(phrase => new ToolCommandPhrase(phrase, Nothing)
        {
            When = () => checklists.Proposals.PendingFor(checklists.Document.CommanderFid).Count > 0,
        }),
    ];

    /// <summary>The live half, for prompt position 7.</summary>
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
    /// Writes one slot of a ship's build (Phase 26, "A plan reaches the checklist when you say so").
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

        // The slot is named the way the plan keys it rather than the way the Commander said it, so
        // "thrusters" replaces what the build already says about MainEngines instead of sitting beside it.
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
            arguments.TryGetInt32("grade", out var grade) ? grade : 0,
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

    /// <summary>Writes a suit or weapon plan (Phase 27, "The same page, on foot").</summary>
    private static string OnFoot(Loadout.OnFootPlanService? plans, ToolArguments arguments)
    {
        if (!arguments.TryGetString("equipment", out var equipment) || string.IsNullOrWhiteSpace(equipment))
        {
            return "No suit or weapon was named.";
        }

        if (plans is null)
        {
            return "I am not tracking suit or weapon plans.";
        }

        var weapon = arguments.TryGetBoolean("weapon", out var isWeapon) && isWeapon;

        var named = arguments.TryGetString("item", out var item) && !string.IsNullOrWhiteSpace(item)
            ? item.Trim()
            : equipment.Trim();

        if (Loadout.OnFootPlanService.Which(plans, named, weapon) is not { } build)
        {
            return $"I could not tell which suit or weapon \"{named}\" means, and it is not one I "
                   + "know of either.";
        }

        var dropping = arguments.TryGetBoolean("drop", out var drop) && drop;
        var grade = arguments.TryGetInt32("grade", out var wanted) ? wanted : (int?)null;
        var modification = arguments.TryGetString("modification", out var fit) ? fit : null;

        if (grade is null && string.IsNullOrWhiteSpace(modification))
        {
            return dropping
                ? "Which grade or modification should the plan stop saying?"
                : "A plan for a suit or a weapon needs a grade or a modification.";
        }

        var slot = grade is not null
            ? Loadout.OnFootBuild.GradeSlot
            : Slot(plans, build, modification!);

        if (slot is null)
        {
            return $"{build.Describe()} has all its modification slots planned already, and there "
                   + "are four at most.";
        }

        if (dropping)
        {
            return plans.Clear(build.Id, slot)
                ? $"{build.Describe()}: nothing is planned for {slot} now. What it already put on "
                  + "your checklist is kept."
                : $"{build.Describe()} had nothing planned for {slot}.";
        }

        var plan = new Loadout.KitPlan(slot, grade, modification);

        return plans.Plan(build.Id, plan)
            ? $"{build.Describe()}: {slot} is planned for {plan.Describe()}. It is not on your "
              + "checklist until you say so. Modifications are permanent: four slots at most, and a "
              + "wrong one is recoverable only by buying and re-upgrading a fresh item."
            : "I could not write that down.";
    }

    /// <summary>The slot a modification belongs in: the one already holding it, or the first free one.</summary>
    private static string? Slot(
        Loadout.OnFootPlanService plans, Loadout.OnFootBuild build, string modification)
    {
        var already = build.Slots.FirstOrDefault(slot =>
            ChecklistKeys.Compact(slot.Modification) == ChecklistKeys.Compact(modification));

        return already?.Slot ?? plans.FirstFreeSlot(build, null);
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

    /// <summary>A read-only summary carrying the way into the panel.</summary>
    private static SettingRow SummaryRow(ChecklistService checklists) => new()
    {
        Key = SummaryKey,
        Advanced = true,
        Label = "Your checklist",
        Help = "What you are working on: your own lines, your ship builds and your construction sites. "
               + "Computed items cannot be ticked by hand — they follow your journal.",
        Kind = SettingKind.Info,
        DocsAnchor = "the-checklist-tab",
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
