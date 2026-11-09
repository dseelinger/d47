using D47.Core.Campaigns;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The Commander's BGS campaign: one faction, an intent for each of its systems, and the chronicle of resolved fronts.</summary>
public static class CampaignCapability
{
    public const string Id = "campaign";

    public const string GetTool = "get_campaign";

    public const string SetFactionTool = "set_campaign_faction";

    public const string SetIntentTool = "set_system_intent";

    public const string FrontsTool = "get_campaign_fronts";

    public const string TakeFrontTool = "take_campaign_front";

    public const string NextFrontTool = "next_campaign_front";

    public const string DropFrontTool = "drop_campaign_front";

    public const string NextActionTool = "next_campaign_action";

    public const string ChronicleTool = "read_campaign_chronicle";

    private const int FrontsSpoken = 3;

    private const int ChronicleSpoken = 3;

    /// <summary>The intents, by the name the tools take.</summary>
    public static readonly IReadOnlyList<string> IntentNames = Enum.GetNames<SystemIntent>();

    private static readonly string[] Questions = ["what is our campaign", "what's our campaign", "campaign report"];

    private static readonly string[] ActionQuestions =
    [
        "what should i do for the faction",
        "what should we do for the faction",
        "what is the next campaign action",
    ];

    /// <summary>The kinds of front, by the name the tools take.</summary>
    public static readonly IReadOnlyList<string> FrontKindNames = Enum.GetNames<FrontKind>();

    private static readonly string[] FrontQuestions = ["what are our fronts", "what are the campaign fronts", "campaign fronts"];

    public static CapabilityDescriptor Create(
        CampaignStore? store,
        Func<CommanderGameState?> gameState,
        SquadronFactions? squadronFactions = null,
        IGalaxyService? galaxy = null,
        FactionSystemsSeen? seen = null,
        Func<CampaignFrontsSnapshot?>? fronts = null,
        CampaignFocus? focus = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Campaign",
        Summary = "Keep a plan for one minor faction: what you mean to do in each of its systems.",
        Examples = ["our campaign faction is Hutton Truckers", "hold Shinrarta Dezhra", "what is our campaign"],
        Display = new CapabilityDisplay { PanelTitle = "Campaign", Order = 70, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = GetTool,
                Exercise =
                    "Say \"what is our campaign\". The reply names the campaign faction and how many of its systems "
                    + "are set to each intent.",
                Description =
                    "The Commander's campaign: the faction, the intent for each system the faction is in (Hold where it "
                    + "controls and LeaveAlone elsewhere unless the Commander chose otherwise), and the chronicle of "
                    + "resolved fronts. The faction is the squadron's unless the Commander named another.",
                Commands = [.. Questions.Select(phrase => new ToolCommandPhrase(phrase))],
                Handler = (_, cancellationToken) => GetAsync(store, gameState(), squadronFactions, galaxy, seen, cancellationToken),
            },
            new ToolDefinition
            {
                Name = FrontsTool,
                Exercise =
                    "Say \"what are our fronts\". The reply names the campaign faction's top three fronts, each with "
                    + "its system and why it is a front, and counts the rest.",
                Description =
                    "The campaign faction's fronts, ranked by d47 from the faction report fetched after the last BGS "
                    + "tick and the Commander's own readings: conflicts, coup threats, retreats, thin margins, bad "
                    + "states, unwanted expansion, expansion under way and stale reports. The top three with the reason "
                    + "for each, a count of the rest, and when they were computed. The ranking is d47's; do not reorder "
                    + "it or add fronts.",
                Commands = [.. FrontQuestions.Select(phrase => new ToolCommandPhrase(phrase))],
                Handler = (_, _) => Task.FromResult(Fronts(store, gameState(), squadronFactions, fronts?.Invoke())),
            },
            new ToolDefinition
            {
                Name = ChronicleTool,
                Exercise =
                    "Say \"read the chronicle\". The reply is the Chronicler's passages for the latest three resolved "
                    + "fronts, newest first.",
                Description =
                    "Read the faction's chronicle aloud: the passage the Chronicler wrote once for each resolved front, newest "
                    + "first, at most three, with a count of the rest. Read the passages as given; add nothing to them. "
                    + "Call it only when the Commander asks for the chronicle.",
                Commands = [new ToolCommandPhrase("read the chronicle"), new ToolCommandPhrase("read the legend")],
                Handler = (_, _) => Task.FromResult(ReadChronicle(store, gameState(), squadronFactions)),
            },
            new ToolDefinition
            {
                Name = NextActionTool,
                Exercise =
                    "Say \"what should I do for the faction\" with a front chosen. The reply is one sentence naming "
                    + "the work, then at most one sentence naming work that does not count.",
                Description =
                    "The single next action for the front the Commander has chosen, built by d47 from the front and the "
                    + "Commander's unredeemed bounties and combat bonds: one sentence, then at most one sentence naming "
                    + "work that does not count there. With no front chosen it names the front d47 proposes.",
                Commands = [.. ActionQuestions.Select(phrase => new ToolCommandPhrase(phrase))],
                Handler = (_, _) => Task.FromResult(NextAction(focus, store, gameState())),
            },
            new ToolDefinition
            {
                Name = TakeFrontTool,
                Exercise =
                    "With a front proposed, say \"take it\". The reply names the front and its next action.",
                Description =
                    "Choose the front to work. With no arguments it takes the front d47 proposes. With a system, and "
                    + "optionally a kind, it takes the faction's front there. The Commander's own decision.",
                Parameters =
                [
                    ToolParameter.Text("system", "The star system of the front; leave out to take the proposed front."),
                    ToolParameter.OneOf("kind", "The kind of front there, when the system has more than one.", FrontKindNames),
                ],
                Protected = true,
                RefusalExample = "take it",
                Handler = (arguments, _) => Task.FromResult(Take(focus, store, gameState(), arguments)),
            },
            new ToolDefinition
            {
                Name = NextFrontTool,
                Exercise = "Say \"next front\". The reply names the next front d47 proposes.",
                Description = "Skip the proposed front and propose the next one. The Commander's own decision.",
                Commands = [new ToolCommandPhrase("next front")],
                Protected = true,
                RefusalExample = "next front",
                Handler = (_, _) => Task.FromResult(NextFront(focus)),
            },
            new ToolDefinition
            {
                Name = DropFrontTool,
                Exercise = "Say \"drop the front\". The reply says the front is dropped.",
                Description = "Stop working the chosen front. Nothing is added to the chronicle. The Commander's own decision.",
                Commands = [new ToolCommandPhrase("drop the front")],
                Protected = true,
                RefusalExample = "drop the front",
                Handler = (_, _) => Task.FromResult(Drop(focus)),
            },
            new ToolDefinition
            {
                Name = SetFactionTool,
                Exercise =
                    "Say \"our campaign faction is\" followed by the name of a minor faction. The reply says whether "
                    + "the faction was stored.",
                Description =
                    "Name the minor faction the Commander's campaign is for, after checking the galaxy service knows "
                    + "it. The Commander's own decision.",
                Parameters =
                [
                    ToolParameter.Text("faction", "The minor faction's exact name, as the Commander said it.", required: true),
                ],
                Protected = true,
                RefusalExample = "our campaign faction is Hutton Truckers",
                Handler = (arguments, cancellationToken) =>
                    SetFactionAsync(store, gameState(), squadronFactions, galaxy, seen, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = SetIntentTool,
                Exercise =
                    "Say \"hold\" followed by a system the campaign faction is in. The reply confirms the intent for "
                    + "that system.",
                Description =
                    "Set what the Commander means to do with the campaign faction in one system. The Commander's own "
                    + "decision.",
                Parameters =
                [
                    ToolParameter.Text("system", "The star system.", required: true),
                    ToolParameter.OneOf("intent", "What the Commander means to do there.", IntentNames, required: true),
                ],
                Protected = true,
                RefusalExample = "hold Shinrarta Dezhra",
                Handler = (arguments, _) => Task.FromResult(SetIntent(store, gameState(), arguments)),
            },
        ],
    };

    /// <summary>"hold Lave", "grow Lave" and the rest, for each system the campaign faction is known to be in or that has an intent.</summary>
    public static IEnumerable<DynamicCommand> Phrases(
        CampaignStore? store,
        Func<CommanderGameState?> gameState,
        SquadronFactions? squadronFactions = null,
        FactionSystemsSeen? seen = null,
        CampaignFocus? focus = null,
        Func<CampaignFrontsSnapshot?>? fronts = null)
    {
        if (store is null || gameState() is not { } state || state.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            yield break;
        }

        if (focus?.Proposal is not null)
        {
            yield return new DynamicCommand("take it", Id, TakeFrontTool, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        if (fronts?.Invoke() is { } held && string.Equals(held.FrontierId, frontierId, StringComparison.Ordinal))
        {
            foreach (var front in held.Fronts)
            {
                var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["system"] = front.System,
                    ["kind"] = front.Kind.ToString(),
                };

                yield return new DynamicCommand($"take the {FrontNoun(front)} in {front.System}", Id, TakeFrontTool, arguments);
                yield return new DynamicCommand($"take the front in {front.System}", Id, TakeFrontTool, arguments);
            }
        }

        var campaign = store.For(frontierId);
        var faction = campaign.FactionOr(SquadronFactionResolver.Resolve(state, squadronFactions));
        var systems = new HashSet<string>(campaign.Intents.Keys, StringComparer.OrdinalIgnoreCase);

        if (faction is not null)
        {
            systems.UnionWith(state.Standings.Systems.Where(system => state.Standings.Latest(system)?.InfluenceOf(faction) is not null));
            systems.UnionWith((seen?.For(faction) ?? []).Select(system => system.Name));
        }

        foreach (var system in systems.Order(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var (shape, intent) in Shapes(system))
            {
                yield return new DynamicCommand(
                    shape,
                    Id,
                    SetIntentTool,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system, ["intent"] = intent.ToString() });
            }
        }
    }

    private static IEnumerable<(string Shape, SystemIntent Intent)> Shapes(string system) =>
    [
        ($"hold {system}", SystemIntent.Hold),
        ($"grow {system}", SystemIntent.Grow),
        ($"expand from {system}", SystemIntent.ExpandFromHere),
        ($"keep {system} under seventy five", SystemIntent.StayUnder75),
        ($"keep {system} under seventy five percent", SystemIntent.StayUnder75),
        ($"take control of {system}", SystemIntent.TakeControl),
        ($"leave {system} alone", SystemIntent.LeaveAlone),
    ];

    private static string FrontNoun(Front front) => front.Kind == FrontKind.Conflict && front.Conflict is { } conflict
        ? conflict.Type.ToLowerInvariant()
        : CampaignFronts.Describe(front.Kind).ToLowerInvariant();

    private static ToolResult ReadChronicle(CampaignStore? store, CommanderGameState? state, SquadronFactions? squadronFactions)
    {
        if (store is null || state?.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no chronicle to read.");
        }

        var campaign = store.For(frontierId);

        if (campaign.Chronicle.Count == 0)
        {
            return ToolResult.Ok("No front has resolved yet, so the chronicle is empty.");
        }

        var written = campaign.Chronicle.Where(entry => !string.IsNullOrWhiteSpace(entry.Legend)).Reverse().ToList();

        if (written.Count == 0)
        {
            return ToolResult.Ok("The Chronicler has not written any passage yet.");
        }

        var faction = campaign.FactionOr(SquadronFactionResolver.Resolve(state, squadronFactions)) ?? string.Empty;
        var chronicler = CampaignArcs.Chronicler(CampaignArcs.SeedKey(state.Squadron.Key, faction));
        var read = string.Join(" ", written.Take(ChronicleSpoken).Select(entry => entry.Legend));
        var rest = written.Count - ChronicleSpoken;

        return ToolResult.Ok(
            $"{chronicler}: {read}{(rest > 0 ? $" There are {Plural(rest, "older passage")}." : string.Empty)}");
    }

    private static ToolResult NextAction(CampaignFocus? focus, CampaignStore? store, CommanderGameState? state)
    {
        if (focus is null || store is null || state?.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to work.");
        }

        if (focus.NextAction() is { } action)
        {
            return ToolResult.Ok(Said(action));
        }

        if (store.For(frontierId).Chosen is { } chosen)
        {
            return ToolResult.Ok(
                $"You are working the {CampaignFronts.Describe(chosen.Kind).ToLowerInvariant()} in {chosen.System}, "
                + "but its front is not in the faction report D47 holds yet. D47 will say when it resolves.");
        }

        return ToolResult.Ok(focus.Proposal is { } proposal
            ? $"No front is chosen. D47 proposes the {Proposed(proposal.Front)} Say 'take it' or 'next front'."
            : "No front is chosen and D47 has none to propose yet. Ask 'what are our fronts'.");
    }

    private static string Said(CampaignNextAction action) =>
        action.NotCounting is { } excluded ? $"{action.Sentence} {excluded}" : action.Sentence;

    private static string Proposed(Front front) =>
        $"{CampaignFronts.Describe(front.Kind).ToLowerInvariant()} in {front.System}: {front.Reason}";

    private static ToolResult Take(CampaignFocus? focus, CampaignStore? store, CommanderGameState? state, ToolArguments arguments)
    {
        if (focus is null || store is null || state?.Identity.FrontierId is null)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to work.");
        }

        FrontKind? kind = null;

        if (arguments.TryGetString("kind", out var named) && !string.IsNullOrWhiteSpace(named))
        {
            if (!Enum.TryParse<FrontKind>(named, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return ToolResult.Error($"The kind has to be one of {string.Join(", ", FrontKindNames)}.");
            }

            kind = parsed;
        }

        Front? taken;

        if (arguments.TryGetString("system", out var system) && !string.IsNullOrWhiteSpace(system))
        {
            taken = focus.Take(kind, system);

            if (taken is null)
            {
                return ToolResult.Error($"The faction has no front in {system.Trim()} to take.");
            }
        }
        else
        {
            taken = focus.Accept();

            if (taken is null)
            {
                return ToolResult.Error("D47 has no front proposed to take.");
            }
        }

        var action = focus.NextAction();

        return ToolResult.Ok(
            $"Working the {CampaignFronts.Describe(taken.Kind).ToLowerInvariant()} in {taken.System}."
            + (action is null ? "" : " " + Said(action)));
    }

    private static ToolResult NextFront(CampaignFocus? focus)
    {
        if (focus is null)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to work.");
        }

        return focus.Next() is { } proposal
            ? ToolResult.Ok($"The next front is the {Proposed(proposal.Front)} Say 'take it' or 'next front'.")
            : ToolResult.Ok("There is no front to propose: one is already chosen, or the faction has none.");
    }

    private static ToolResult Drop(CampaignFocus? focus) =>
        focus is null
            ? ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to work.")
            : focus.Drop()
                ? ToolResult.Ok("The front is dropped.")
                : ToolResult.Ok("No front is chosen.");

    /// <summary>The intent as a short instruction.</summary>
    public static string Describe(SystemIntent intent) => intent switch
    {
        SystemIntent.Hold => "hold",
        SystemIntent.Grow => "grow",
        SystemIntent.ExpandFromHere => "expand from here",
        SystemIntent.StayUnder75 => "keep under 75%",
        SystemIntent.TakeControl => "take control",
        _ => "leave alone",
    };

    private static ToolResult SetIntent(CampaignStore? store, CommanderGameState? state, ToolArguments arguments)
    {
        if (store is null || state?.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to change.");
        }

        if (!arguments.TryGetString("system", out var system) || string.IsNullOrWhiteSpace(system))
        {
            return ToolResult.Error("Which system?");
        }

        if (!arguments.TryGetString("intent", out var named) || !Enum.TryParse<SystemIntent>(named, ignoreCase: true, out var intent)
            || !Enum.IsDefined(intent))
        {
            return ToolResult.Error($"The intent has to be one of {string.Join(", ", IntentNames)}.");
        }

        return store.SetIntent(frontierId, system, intent)
            ? ToolResult.Ok($"{system.Trim()}: {Describe(intent)}.")
            : ToolResult.Error("I couldn't save that intent.");
    }

    private static async Task<ToolResult> SetFactionAsync(
        CampaignStore? store,
        CommanderGameState? state,
        SquadronFactions? squadronFactions,
        IGalaxyService? galaxy,
        FactionSystemsSeen? seen,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (store is null || state?.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to change.");
        }

        if (!arguments.TryGetString("faction", out var said) || string.IsNullOrWhiteSpace(said))
        {
            return ToolResult.Error("Which minor faction is it?");
        }

        if (galaxy is null)
        {
            return ToolResult.Error("Checking a faction name needs galaxy search, which is not available.");
        }

        var given = said.Trim();
        var spelled = Catalogue.Match([.. state.Reputation.Factions.Keys], given) ?? given;

        FactionReport report;

        try
        {
            report = await galaxy.FactionAsync(spelled, cancellationToken).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }

        if (report.Systems.Count == 0)
        {
            return ToolResult.Error($"I found no minor faction called {given}, so I have not changed the campaign.");
        }

        seen?.Remember(report);

        var exact = report.Name is { Length: > 0 } name ? name : spelled;
        var campaign = store.For(frontierId);
        var before = campaign.FactionOr(SquadronFactionResolver.Resolve(state, squadronFactions));
        var cleared = campaign.Intents.Count > 0 && !string.Equals(before, exact, StringComparison.OrdinalIgnoreCase);

        if (!store.SetFaction(frontierId, exact, cleared))
        {
            return ToolResult.Error("I couldn't save the campaign faction.");
        }

        return ToolResult.Ok(!cleared
            ? $"The campaign faction is {exact}."
            : before is null
                ? $"The campaign faction is {exact}. The intents set before are cleared."
                : $"The campaign faction is {exact}. The intents set for {before}'s systems are cleared.");
    }

    private static async Task<ToolResult> GetAsync(
        CampaignStore? store,
        CommanderGameState? state,
        SquadronFactions? squadronFactions,
        IGalaxyService? galaxy,
        FactionSystemsSeen? seen,
        CancellationToken cancellationToken)
    {
        if (store is null || state?.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to read.");
        }

        var campaign = store.For(frontierId);
        var squadron = SquadronFactionResolver.Resolve(state, squadronFactions);

        if (campaign.FactionOr(squadron) is not { } faction)
        {
            return ToolResult.Ok(
                "There is no campaign faction: D47 knows no squadron faction for you and none is named. "
                + "Say 'our campaign faction is' and its name.");
        }

        var opening = campaign.Faction is null
            ? $"The campaign faction is {faction}, your squadron's faction."
            : $"The campaign faction is {faction}.";

        IReadOnlyList<FactionSystem>? systems = null;
        string? unavailable = null;

        if (galaxy is null)
        {
            unavailable = "Galaxy search is not available";
        }
        else
        {
            try
            {
                var report = await galaxy.FactionAsync(faction, cancellationToken).ConfigureAwait(false);

                seen?.Remember(report);
                systems = report.Systems;
            }
            catch (GalaxyUnavailableException ex)
            {
                unavailable = ex.Message.TrimEnd('.');
            }
        }

        var lines = new List<(string System, SystemIntent Intent, bool Set)>();

        foreach (var system in (systems ?? []).OrderBy(system => system.Name, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add((system.Name, campaign.IntentFor(system.Name, system.Controls), campaign.Intents.ContainsKey(system.Name)));
        }

        foreach (var (system, intent) in campaign.Intents.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!lines.Any(line => string.Equals(line.System, system, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add((system, intent, true));
            }
        }

        var content = new System.Text.StringBuilder(opening);

        if (unavailable is not null)
        {
            content.Append($" {unavailable}, so only the systems with an intent set are listed.");
        }

        content.Append(lines.Count == 0
            ? " No system has an intent set."
            : " " + string.Join(" ", lines.Select(line => $"{line.System}: {Describe(line.Intent)}{(line.Set ? "" : " (default)")}.")));

        content.Append(' ').Append(Chronicle(campaign.Chronicle));

        var counts = lines
            .GroupBy(line => line.Intent)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Count()} to {Describe(group.Key)}");

        var spoken = lines.Count == 0
            ? $"{opening} No system has an intent set."
            : unavailable is null
                ? $"{opening} {Plural(lines.Count, "system")}: {string.Join(", ", counts)}."
                : $"{opening} {unavailable}, so only the systems with an intent set count: {string.Join(", ", counts)}.";

        return ToolResult.Ok(content.ToString(), spoken);
    }

    private static ToolResult Fronts(
        CampaignStore? store,
        CommanderGameState? state,
        SquadronFactions? squadronFactions,
        CampaignFrontsSnapshot? snapshot)
    {
        if (store is null || state?.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no campaign to read.");
        }

        if (store.For(frontierId).FactionOr(SquadronFactionResolver.Resolve(state, squadronFactions)) is not { } faction)
        {
            return ToolResult.Ok(
                "There is no campaign faction: D47 knows no squadron faction for you and none is named. "
                + "Say 'our campaign faction is' and its name.");
        }

        if (snapshot is null
            || !string.Equals(snapshot.FrontierId, frontierId, StringComparison.Ordinal)
            || !string.Equals(snapshot.Faction, faction, StringComparison.OrdinalIgnoreCase)
            || snapshot.Report is null)
        {
            return ToolResult.Ok(snapshot?.Failure is { } failure && string.Equals(snapshot.Faction, faction, StringComparison.OrdinalIgnoreCase)
                ? $"There are no fronts for {faction} yet: the faction report could not be fetched. {failure} D47 tries again later."
                : $"There are no fronts for {faction} yet. D47 is fetching the faction report.");
        }

        var count = snapshot.Fronts.Count;
        var top = snapshot.Fronts.Take(FrontsSpoken)
            .Select(front => $"{CampaignFronts.Describe(front.Kind)} in {front.System}: {front.Reason}")
            .ToList();

        var said = count == 0
            ? $"{faction} has no fronts: nothing in its {Plural(snapshot.Report.Systems.Count, "system")} needs attention."
            : $"{faction} has {Plural(count, "front")}. {string.Join(" ", top)}"
                + (count > FrontsSpoken ? $" And {count - FrontsSpoken} more." : "");

        var computed = snapshot.ComputedAt is { } at ? $" Computed {AsOf(at)}." : "";

        if (snapshot.Failure is { } failed)
        {
            var fetched = snapshot.ReportFetchedAt is { } fetchedAt ? $" fetched {AsOf(fetchedAt)}" : "";

            return ToolResult.Ok(
                $"{said}{computed} The last fetch failed: {failed} These fronts are from the report{fetched}.",
                $"{said} The last fetch failed, so these fronts are from an older report.");
        }

        return ToolResult.Ok($"{said}{computed}", said);
    }

    private static string AsOf(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    private static string Chronicle(IReadOnlyList<ChronicleEntry> chronicle)
    {
        if (chronicle.Count == 0)
        {
            return "The chronicle holds no resolved fronts yet.";
        }

        var latest = chronicle.TakeLast(ChronicleSpoken).Reverse().Select(entry =>
            $"{entry.Kind} in {entry.System}{(entry.Opponent is { } opponent ? $" against {opponent}" : "")}: "
            + $"{entry.Outcome}, over {Plural(entry.Ticks.Count, "tick")}.");

        return $"The chronicle holds {Plural(chronicle.Count, "resolved front")}, latest first: {string.Join(" ", latest)}";
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
