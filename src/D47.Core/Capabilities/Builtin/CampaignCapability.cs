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

    private const int ChronicleSpoken = 3;

    /// <summary>The intents, by the name the tools take.</summary>
    public static readonly IReadOnlyList<string> IntentNames = Enum.GetNames<SystemIntent>();

    private static readonly string[] Questions = ["what is our campaign", "what's our campaign", "campaign report"];

    public static CapabilityDescriptor Create(
        CampaignStore? store,
        Func<CommanderGameState?> gameState,
        SquadronFactions? squadronFactions = null,
        IGalaxyService? galaxy = null,
        FactionSystemsSeen? seen = null) => new()
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
        FactionSystemsSeen? seen = null)
    {
        if (store is null || gameState() is not { } state || state.Identity.FrontierId is not { Length: > 0 } frontierId)
        {
            yield break;
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
