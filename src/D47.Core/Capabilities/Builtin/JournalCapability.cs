using System.Globalization;
using System.Text;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything d47 knows about the game from the journal (Phase 2, "TheApp knows where you are"; Phase
/// 7, "Knowing the game").
/// </summary>
public static class JournalCapability
{
    public const string Id = "journal";

    public const string ResetExplorationTool = "clear_unsold_exploration";

    public const string CodexPicturesKey = "journal.codexPictures";

    public const string ShareCodexPicturesKey = "journal.shareCodexPictures";

    public const string OpenNewCodexEntriesKey = "journal.openNewCodexEntries";

    public const string CodexPublishTokenKey = "journal.codexPublishToken";

    /// <param name="history">
    /// How far the walk over older journals has got, so a fleet question says that rather than reporting
    /// an absence the walk has not ruled out (#148).
    /// </param>
    /// <param name="route">The plotted route, which is where a jump count comes from (#152).</param>
    /// <param name="cartography">The mapped bodies not yet sold, or null where nothing keeps them.</param>
    /// <param name="crimes">The unpaid fines and bounties, or null where nothing keeps them.</param>
    /// <param name="liveStatus">The latest Status.json read.</param>
    public static CapabilityDescriptor Create(
        GameStateStore gameState,
        Func<HistoryState>? history = null,
        Func<NavRoute>? route = null,
        CartographyLedger? cartography = null,
        Func<DateTimeOffset>? now = null,
        OutstandingCrimes? crimes = null,
        Func<GameStatus>? liveStatus = null,
        Func<DateTimeOffset?>? bgsTick = null)
    {
        var state = history ?? (() => HistoryState.Done);
        var plotted = route ?? (() => NavRoute.None);

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Foundation",
            Name = "Journal",
            Summary =
                "Report where the Commander is, what they are flying, what they own and what they have "
                + "done this session, from the journal.",
            Examples =
            [
                "where am I",
                "what am I flying",
                "where is my carrier",
                "what ships do I own",
                "what materials am I carrying",
                "how have I done this session",
                "what are my career statistics",
                "what's my reputation with the Empire",
                "how much exploration data am I carrying",
                "reset unsold exploration",
            ],

            // Phrases, not words. "where" and "system" on their own match "Where is Iran?" and "what's your
            // operating system" — questions this capability has no business answering, answered from journal
            // data with total confidence.
            Keywords =
            [
                new("where am i", "get_location"),
                new("where i am", "get_location"),
                new("what system", "get_location"),
                new("which system", "get_location"),
                new("current system", "get_location"),
                new("my location", "get_location"),
                new("am i docked", "get_location"),
                new("what body", "get_location"),
                new("what am i flying", "get_ship"),
                new("my ship", "get_ship"),
                new("my loadout", "get_ship"),

                // Narrowed from "jump range", which was the worst keyword in this list (#161).
                new("my jump range", "get_ship") { Arguments = WithJumpRange },

                // The carrier's position and the ship list are two questions on one tool (#406), so each
                // keyword says which it means rather than leaving the router to invoke with nothing and get
                // the shorter answer by accident. "my fleet carrier" is declared for itself because "my
                // fleet" is contained in it, and the router takes the longest phrase it matches: without this
                // line every carrier question worded the long way — "how far is my fleet carrier from here" —
                // matched "my fleet" alone and read out the whole ship list.
                new("where is my carrier", "get_fleet"),
                new("my fleet carrier", "get_fleet"),
                new("my carrier", "get_fleet"),
                new("what ships do i own", "get_fleet") { Arguments = WithShips },
                new("my ships", "get_fleet") { Arguments = WithShips },
                new("my fleet", "get_fleet") { Arguments = WithShips },
                new("stored modules", "get_stored_modules"),
                new("module storage", "get_stored_modules"),
                new("what materials", "get_materials"),
                new("my materials", "get_materials"),
                new("my backpack", "get_materials"),
                new("ship locker", "get_materials"),
                new("session summary", "get_session_summary"),
                new("how have i done", "get_session_summary"),
                new("this session", "get_session_summary"),
                new("credit ledger", "get_credit_ledger"),
                new("where did my money go", "get_credit_ledger"),
                new("where did my credits go", "get_credit_ledger"),
                new("my earnings", "get_credit_ledger"),
                new("codex ledger", "get_codex_ledger"),
                new("codex entries", "get_codex_ledger"),
                new("codex discoveries", "get_codex_discoveries"),
                new("my codex discoveries", "get_codex_discoveries"),
                new("what codex entries am i missing", "get_codex_discoveries"),
                new("missing codex entries", "get_codex_discoveries"),
                new("geology on this body", "get_body_geology"),
                new("what geology is here", "get_body_geology"),
                new("likely geology", "get_body_geology"),
                new("combat stats", "get_combat_stats"),
                new("combat statistics", "get_combat_stats"),
                new("my kill stats", "get_combat_stats"),
                new("my kills", "get_combat_stats"),
                new("bgs work", "get_bgs_work"),
                new("my bgs work", "get_bgs_work"),
                new("what have i done for the bgs", "get_bgs_work"),
                new("my career statistics", "get_commander_statistics"),
                new("my career stats", "get_commander_statistics"),
                new("commander statistics", "get_commander_statistics"),
                .. SuperpowerKeywords(),
                new("my navy rank", "get_standing"),
                new("my naval rank", "get_standing"),
                new("my navy ranks", "get_standing"),
                new("exploration data am i carrying", "get_unsold_exploration"),
                new("unsold exploration", "get_unsold_exploration"),
                new("crime status", "get_crime_status"),
                new("am i wanted", "get_crime_status"),
                new("my bounties", "get_crime_status"),
                new("my fines", "get_crime_status"),
                new("what do i owe", "get_crime_status"),
                new("reset unsold exploration", ResetExplorationTool),
                new("reset the exploration total", ResetExplorationTool),
            ],
            Display = new CapabilityDisplay { PanelTitle = "Location", Order = 20 },
            Settings =
            [
                new SettingRow
                {
                    Key = CodexPicturesKey,
                    Label = "Codex pictures",
                    Help =
                        "Stock pictures of codex entries are published as files on a GitHub release. "
                        + "D47 fetches the list when a codex page first wants a picture in a session, and each picture when a page first shows it, "
                        + "and keeps them in data\\codex-pictures.\n\n"
                        + "Off, nothing is fetched and a codex page shows only pictures you saved yourself, even for stock pictures already on disk. "
                        + "Files already on disk are kept.",
                    Kind = SettingKind.Toggle,
                    DocsAnchor = "codex-pictures",
                    EgressId = EgressDisclosure.CodexPictures,
                    Binding = new SettingBinding
                    {
                        Read = s => s.Ui.CodexPictures ? "true" : "false",
                        Write = (s, v) => s with
                        {
                            Ui = s.Ui with { CodexPictures = v is not "false" },
                        },
                    },
                },
                new SettingRow
                {
                    Key = ShareCodexPicturesKey,
                    Label = "Share my codex pictures with my other Commanders",
                    Help =
                        "Each Commander keeps their own codex pictures. Ticked, every Commander's pictures are one pool: "
                        + "a codex page shows the active Commander's picture where there is one, and another Commander's where there is not.\n\n"
                        + "Re-crop, Replace and Remove my picture act on the active Commander's picture only.",
                    Kind = SettingKind.Toggle,
                    DocsAnchor = "share-codex-pictures",
                    Binding = new SettingBinding
                    {
                        Read = s => s.Ui.ShareCodexPictures ? "true" : "false",
                        Write = (s, v) => s with
                        {
                            Ui = s.Ui with { ShareCodexPictures = v is "true" },
                        },
                    },
                },
                new SettingRow
                {
                    Key = OpenNewCodexEntriesKey,
                    Label = "Open new codex entries",
                    Help =
                        "When you log a codex entry for the first time, the panel opens that entry's page, in the window and on the headset panel. "
                        + "A page that is open as a dialog, or a keyboard entry, stays where it is.",
                    Kind = SettingKind.Toggle,
                    DocsAnchor = "open-new-codex-entries",
                    Binding = new SettingBinding
                    {
                        Read = s => s.Ui.OpenNewCodexEntries ? "true" : "false",
                        Write = (s, v) => s with
                        {
                            Ui = s.Ui with { OpenNewCodexEntries = v is not "false" },
                        },
                    },
                },
                new SettingRow
                {
                    Key = CodexPublishTokenKey,
                    Advanced = true,
                    Label = "Codex publish token",
                    Help =
                        "For the maintainer of the stock codex pictures. With a token here, a codex entry's page in the main window "
                        + "offers Publish and Unpublish, which put a picture on, or take it off, the stock pictures every Commander sees.\n\n"
                        + "It is a fine-grained GitHub token with contents write on dseelinger/d47-codex-pictures only. It is stored "
                        + "encrypted for this Windows account and is write-only. Without one here, D47 reads "
                        + "D47_CODEX_PUBLISH_TOKEN from the environment.",
                    Kind = SettingKind.Secret,
                    SecretName = Interface.CodexPublishToken.SecretName,
                    DocsAnchor = "codex-publish-token",
                },
            ],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "get_location",
                    AlwaysLoaded = true,
                    Description =
                        "Report the current Commander's star system, the class of its arrival star, body, docking "
                        + "state and what they are doing — supercruise, hyperspace, landed, on foot — from "
                        + "the journal.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeLocation(gameState, plotted()))),
                },
                new ToolDefinition
                {
                    Name = "get_ship",
                    AlwaysLoaded = true,
                    Description =
                        "Report a ship: type, name, hull health, jump range, fuel, cargo held and capacity, "
                        + "and the modules fitted. Omit ship for the one the Commander is flying, live where "
                        + "the journal has described it this session and remembered otherwise. Name any other "
                        + "ship they own to read it from the loadout last seen for it, dated.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "ship",
                            Type = ToolParameterType.String,
                            Description = "Which ship, by name or hull. Omit for the one currently flown.",
                        },
                        new ToolParameter
                        {
                            Name = "jump_range",
                            Type = ToolParameterType.Boolean,
                            Description =
                                "Answer with the unladen jump range alone. Set it only when they asked "
                                + "for the jump range and nothing else about the ship.",
                        },
                    ],
                    Commands = [.. Asking(Flying), .. Asking(JumpRange, WithJumpRange)],
                    Handler = (arguments, _) => Task.FromResult(DescribeShip(gameState, arguments)),
                },
                new ToolDefinition
                {
                    Name = "get_fleet",
                    Description =
                        "Report the Commander's fleet carrier: which system it is in and when that was last "
                        + "reported. Set ships to also list the ships they own and where each one is stored.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "ships",
                            Type = ToolParameterType.Boolean,
                            Description =
                                "List the ships the Commander owns and which system each one is stored in. "
                                + "Set it only when they asked what ships they have or what ships are in a "
                                + "system. Asking where the carrier is is not that question.",
                        },
                    ],
                    Commands = [.. Asking(WhereTheCarrierIs, NoArguments), .. Asking(WhichShips, WithShips)],
                    Handler = (arguments, _) =>
                        Task.FromResult(ToolResult.Ok(DescribeFleet(gameState, arguments, Unread(state())))),
                },
                new ToolDefinition
                {
                    Name = "get_fleet_loadouts",
                    Description =
                        "Compare the Commander's ships by what is fitted to each: cargo capacity, maximum "
                        + "jump range, unladen mass, fuel, value and rebuy. Every ship they have been seen "
                        + "flying is answerable, not only the one they are aboard. Use it for any question "
                        + "that ranks or filters their ships.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "min_cargo",
                            Type = ToolParameterType.Integer,
                            Description = "List only ships with at least this many tonnes of cargo capacity.",
                        },
                        new ToolParameter
                        {
                            Name = "order_by",
                            Type = ToolParameterType.String,
                            Description = "Rank the ships by this figure, largest first. Listed by name otherwise.",
                            AllowedValues = ["jump_range", "cargo"],
                        },
                        new ToolParameter
                        {
                            Name = "mining",
                            Type = ToolParameterType.Boolean,
                            Description =
                                "List only ships fitted for tritium mining — a mining laser or Volley Repeater, "
                                + "a collector controller and a refinery — each with the modules that show it.",
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(
                        ToolResult.Ok(DescribeFleetLoadouts(gameState, arguments, Unread(state())))),
                },
                new ToolDefinition
                {
                    Name = "get_stored_modules",
                    Description =
                        "List the modules the Commander has in storage and which system each one is in, "
                        + "with what transferring it would cost. Answered from the journal, so it says what "
                        + "they already own — not where a module can be bought.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "module",
                            Type = ToolParameterType.String,
                            Description =
                                "Narrow the list to stored modules whose name contains this — for example "
                                + "\"shield\" or \"Frame Shift Drive\". Leave it out for the whole store.",
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(
                        DescribeStoredModules(gameState, arguments)),
                },
                new ToolDefinition
                {
                    Name = "get_materials",
                    AlwaysLoaded = true,
                    Description =
                        "Report the Commander's material holdings — raw, manufactured and encoded — and their "
                        + "on-foot backpack and ship locker contents.",
                    Commands = Asking(Held),
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeMaterials(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_session_summary",
                    AlwaysLoaded = true,
                    Description =
                        "Report what the Commander has done since entering the game: credits earned by source, "
                        + "jumps made, distance travelled, materials gained and bodies scanned.",
                    Commands = Asking(Done),
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeSession(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_bgs_work",
                    Description =
                        "Report the BGS work the Commander has done since the last BGS tick, per system and "
                        + "faction: mission influence marks, bounty and combat bond vouchers, trade sold and its "
                        + "profit, and cartographic and organic data sold. It reports the work done, not the "
                        + "influence it produced.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "faction",
                            Type = ToolParameterType.String,
                            Description = "Report only the work done for this faction. Omit for every faction.",
                        },
                        new ToolParameter
                        {
                            Name = "system",
                            Type = ToolParameterType.String,
                            Description = "Report only the work done in this system. Omit for every system.",
                        },
                    ],
                    Commands = Asking(BgsWorkQuestions),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                        DescribeBgsWork(gameState, arguments, bgsTick?.Invoke(), now?.Invoke() ?? DateTimeOffset.UtcNow))),
                },
                new ToolDefinition
                {
                    Name = "get_combat_stats",
                    Description =
                        "Report the Commander's kills and the credits awarded for them, from every journal on this PC: "
                        + "by enemy ship, by the Commander's own ship, by system and by faction. Credits are the bounties "
                        + "and combat bonds awarded at the kill, not what was redeemed.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "by",
                            Type = ToolParameterType.String,
                            Description = "The grouping to report. Omit for the top entry of each.",
                            AllowedValues = CombatGroupings,
                        },
                        new ToolParameter
                        {
                            Name = "period",
                            Type = ToolParameterType.String,
                            Description =
                                "The last day, week (7 days) or month (30 days) before now, or all of the record. Default all.",
                            AllowedValues = CombatPeriods,
                        },
                        new ToolParameter
                        {
                            Name = "name",
                            Type = ToolParameterType.String,
                            Description = "Report only the entries of the grouping whose name contains this, without regard to case.",
                        },
                    ],
                    Commands = Asking(CombatStatsQuestions),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                        DescribeCombatStats(gameState, state(), arguments, now?.Invoke() ?? DateTimeOffset.UtcNow))),
                },
                new ToolDefinition
                {
                    Name = "get_codex_ledger",
                    Description =
                        "Report the codex entries the Commander logged over a period, how many were new to their "
                        + "codex, by region and category.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "period",
                            Type = ToolParameterType.String,
                            Description =
                                "Since the last sale of exploration data (trip), since this session began, or all of the record. Default trip.",
                            AllowedValues = CodexPeriods,
                        },
                        new ToolParameter
                        {
                            Name = "region",
                            Type = ToolParameterType.String,
                            Description = "Report only this codex region, by name, or here for the region the Commander is in.",
                        },
                    ],
                    Commands = Asking(CodexLedgerQuestions),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                        DescribeCodexLedger(gameState, state(), arguments))),
                },
                new ToolDefinition
                {
                    Name = "get_codex_discoveries",
                    Description =
                        "Report the codex entries known in a region, how many the Commander has logged and how many are "
                        + "missing, by subcategory, with the most-reported missing ones named.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "region",
                            Type = ToolParameterType.String,
                            Description = "The codex region, by name, or here for the region the Commander is in. Default here.",
                        },
                    ],
                    Commands = Asking(CodexDiscoveriesQuestions),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                        DescribeCodexDiscoveries(gameState, state(), arguments))),
                },
                new ToolDefinition
                {
                    Name = "get_body_geology",
                    Description =
                        "Which geological features a body likely has, predicted from its class and volcanism, and whether "
                        + "the Commander has logged each in the codex for this region. A prediction: the codex entry on "
                        + "the surface decides.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "body",
                            Type = ToolParameterType.String,
                            Description =
                                "The body name, or its short form such as \"7 b\". Leave out for the most recently seen "
                                + "body that has geological signals.",
                        },
                    ],
                    Commands = Asking(BodyGeologyQuestions),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                        DescribeBodyGeology(gameState, state(), arguments))),
                },
                new ToolDefinition
                {
                    Name = "get_credit_ledger",
                    Description =
                        "Report the credits the Commander earned and spent, by kind, from every journal on this PC, "
                        + "with the current balance. Bounties and combat bonds count when redeemed.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "period",
                            Type = ToolParameterType.String,
                            Description =
                                "The last day, week (7 days) or month (30 days) before now, or all of the record. Default week.",
                            AllowedValues = CombatPeriods,
                        },
                        new ToolParameter
                        {
                            Name = "kind",
                            Type = ToolParameterType.String,
                            Description = "Report only this kind of transaction.",
                            AllowedValues = [.. CreditKinds.Select(pair => pair.Name)],
                        },
                    ],
                    Commands = Asking(CreditLedgerQuestions),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                        DescribeCreditLedger(gameState, state(), arguments, now?.Invoke() ?? DateTimeOffset.UtcNow))),
                },
                new ToolDefinition
                {
                    Name = "get_commander_statistics",
                    Description =
                        "Report the Commander's career statistics from the journal's Statistics event — bank "
                        + "balance and Merc Coin balance, combat, crime, smuggling, trading, mining, exploration, passengers, search "
                        + "and rescue, squadron, crafting, crew, multicrew, material trading, fleet carrier and "
                        + "exobiology. Set section to one group; omit it to report every section.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "section",
                            Type = ToolParameterType.String,
                            Description =
                                "One section of the career statistics to report. Omit to report every section.",
                            AllowedValues = StatisticsSections,
                        },
                    ],
                    Commands = Asking(MercCoins, BankAccount),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(DescribeStatistics(gameState, arguments))),
                },
                new ToolDefinition
                {
                    Name = "get_crime_status",
                    Description =
                        "Report whether the Commander is wanted and what they owe: their legal state now from "
                        + "the game, whether they are on foot, the unpaid fines and bounties the journal records "
                        + "for each faction on foot and on the ship they are flying, largest first, with the "
                        + "date of the newest crime, how many other ships carry debts, and their notoriety as "
                        + "last written at login. A fine the game never journaled is not in the ledger.",
                    Parameters = [],
                    Commands = Asking(CrimeStatus),
                    Handler = (_, _) => Task.FromResult(DescribeCrimeStatus(gameState, crimes, liveStatus)),
                },
                new ToolDefinition
                {
                    Name = "get_standing",
                    Description =
                        "Report the Commander's reputation with the Empire, Federation, Alliance and "
                        + "Independents as a band and a number out of 100, and their Imperial and Federal navy "
                        + "ranks. Set faction to one superpower, or to a minor faction by name for its band, "
                        + "number and the date it was last read.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "faction",
                            Type = ToolParameterType.String,
                            Description =
                                "A superpower or a minor faction, whole or part of its name. Omit for the four "
                                + "superpowers and both navy ranks.",
                        },
                    ],
                    Commands = Asking(Standing),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(DescribeStanding(gameState, arguments))),
                },
                new ToolDefinition
                {
                    Name = "get_ranks",
                    Description =
                        "Report the Commander's rank and rank progress in every career (Combat, Trade, Exploration, "
                        + "Mercenary, Exobiologist, CQC) and in the Imperial and Federal navies: the current rank, the "
                        + "percent of the way to the next rank, and the name of the next rank. Use it for any "
                        + "question about rank, promotion or how far the Commander is from a rank.",
                    Parameters = [],
                    Commands = Asking(Ranks),
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeRanks(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_unsold_exploration",
                    Description =
                        "The bodies the Commander has mapped with the surface scanner and not yet sold to "
                        + "Universal Cartographics: an estimate of their total value, how many, and how many "
                        + "were mapped efficiently. Stars and bodies scanned but not mapped are not counted. "
                        + "Lost on death.",
                    Parameters = [],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeUnsoldExploration(cartography, gameState.Active))),
                },
                new ToolDefinition
                {
                    Name = ResetExplorationTool,
                    Description = "Set the Commander's unsold exploration total to zero from now.",
                    Parameters = [],
                    Protected = true,
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                        ResetUnsoldExploration(cartography, gameState.Active, now?.Invoke() ?? DateTimeOffset.UtcNow))),
                },
            ],
        };
    }

    private static string DescribeUnsoldExploration(CartographyLedger? ledger, CommanderGameState? state)
    {
        if (ledger is null)
        {
            return "I am not keeping a total of unsold exploration data in this build.";
        }

        if (state is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var unsold = ledger.Unsold(state.Identity.FrontierId);
        var report = new StringBuilder();

        if (!ledger.HistoryFolded)
        {
            report.AppendLine("I am still reading your older journals, so this may leave some out.");
        }

        if (unsold.Held.Count == 0)
        {
            report.AppendLine("You are carrying no unsold mapped bodies.");
            return report.ToString().TrimEnd();
        }

        var priced = unsold.Held.Count - unsold.Unpriced.Count;

        report.AppendLine(
            $"About {unsold.Total.ToString("N0", CultureInfo.InvariantCulture)} credits of mapped bodies unsold, "
            + $"from {priced} bod{(priced == 1 ? "y" : "ies")}. {unsold.Efficient} of them "
            + $"{(unsold.Efficient == 1 ? "was" : "were")} mapped efficiently. This is an estimate, and it "
            + "leaves out stars and bodies scanned but not mapped, so Universal Cartographics will pay more.");

        if (unsold.Unpriced.Count > 0)
        {
            report.AppendLine(
                $"Left out of the total, because no scan gave their class and mass: "
                + $"{string.Join(", ", unsold.Unpriced.Select(map => map.BodyName))}.");
        }

        report.AppendLine();

        foreach (var map in unsold.Held.Where(map => map.Value is not null))
        {
            report.AppendLine(
                $"  {map.BodyName} — about {map.Value!.Value.ToString("N0", CultureInfo.InvariantCulture)} cr");
        }

        return report.ToString().TrimEnd();
    }

    private static string ResetUnsoldExploration(CartographyLedger? ledger, CommanderGameState? state, DateTimeOffset at)
    {
        if (ledger is null)
        {
            return "I am not keeping a total of unsold exploration data in this build.";
        }

        if (state is not { Identity.FrontierId: { Length: > 0 } fid })
        {
            return "No Elite Dangerous journal has been detected yet, so there is no Commander to reset.";
        }

        ledger.Reset(fid, at);

        return "Unsold exploration reset to zero. Bodies mapped from now on count towards it.";
    }

    /// <summary>The section names a <c>Statistics</c> event carries (#263).</summary>
    private static readonly string[] StatisticsSections =
    [
        "Bank_Account", "Combat", "Crime", "Smuggling", "Trading", "Mining", "Exploration", "Passengers",
        "Search_And_Rescue", "Squadron", "Crafting", "Crew", "Multicrew", "Material_Trader_Stats",
        "FLEETCARRIER", "Exobiology",
    ];

    /// <summary>The whole question, against the tool that answers it (reported 2026-08-21).</summary>
    private static IReadOnlyList<ToolCommandPhrase> Asking(IReadOnlyList<string> phrases) =>
        Asking(phrases, NoArguments);

    /// <summary>
    /// The same, for a tool where two groups of phrases mean two different answers — asking where the
    /// carrier is, and asking what ships there are (#406).
    /// </summary>
    private static IReadOnlyList<ToolCommandPhrase> Asking(
        IReadOnlyList<string> phrases, IReadOnlyDictionary<string, string> arguments) =>
        [.. phrases.Select(phrase => new ToolCommandPhrase(phrase, arguments))];

    private static readonly IReadOnlyDictionary<string, string> NoArguments =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> WithJumpRange =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["jump_range"] = "true" };

    private static readonly IReadOnlyDictionary<string, string> WithShips =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ships"] = "true" };

    /// <summary>Where the carrier is.</summary>
    private static readonly string[] WhereTheCarrierIs =
    [
        "where is my fleet carrier", "where is my carrier", "where's my fleet carrier",
        "where's my carrier", "where is my fc", "my fleet carrier", "my carrier",
        "where is my fleet carrier right now", "what system is my carrier in",
        "what system is my fleet carrier in",
    ];

    /// <summary>Asking for the ships themselves, which is what turns the list on.</summary>
    private static readonly string[] WhichShips =
    [
        "what ships do i own", "what ships have i got", "my ships", "my fleet",
        "where are my ships", "list my ships", "list my fleet",
        "what ships are on my carrier", "what ships are on the carrier",
        "what ships are in this system", "what ships are in the carrier's system",
    ];

    private static readonly string[] Flying =
    [
        "what am i flying", "what ship am i flying", "what am i in", "my ship", "my loadout",
    ];

    private static readonly string[] JumpRange =
    [
        "what is my jump range", "what's my jump range", "my jump range", "jump range",
    ];

    private static readonly string[] Held =
    [
        "what materials am i carrying", "what materials do i have", "my materials",
        "what is in my backpack", "what's in my backpack", "my backpack",
        "what is in my ship locker", "what's in my ship locker", "my ship locker",
    ];

    private static readonly string[] Done =
    [
        "how have i done this session", "how have i done", "session summary",
        "how has this session gone", "what have i done this session",
    ];

    private static readonly string[] CombatStatsQuestions =
    [
        "combat stats", "combat statistics", "my combat stats", "my combat statistics", "my kill stats", "my kills",
    ];

    private static readonly string[] CreditLedgerQuestions =
    [
        "credit ledger", "my credit ledger", "where did my money go", "where did my credits go", "my earnings",
    ];

    private static readonly string[] CodexLedgerQuestions =
    [
        "codex ledger", "my codex ledger", "codex entries", "my codex entries",
    ];

    private static readonly string[] CodexDiscoveriesQuestions =
    [
        "codex discoveries", "my codex discoveries", "what codex entries am i missing", "missing codex entries",
    ];

    private static readonly string[] BodyGeologyQuestions =
    [
        "geology on this body", "what geology is here", "likely geology",
    ];

    private static readonly string[] CodexPeriods = ["trip", "session", "all"];

    private const int CodexGroupsNamed = 3;

    private static readonly (string Name, CreditKind Kind)[] CreditKinds =
    [
        ("trade", CreditKind.Trade),
        ("exploration", CreditKind.Exploration),
        ("missions", CreditKind.Missions),
        ("combat", CreditKind.Combat),
        ("vouchers", CreditKind.Vouchers),
        ("rescue", CreditKind.Rescue),
        ("community_goals", CreditKind.CommunityGoals),
        ("colonisation", CreditKind.Colonisation),
        ("powerplay", CreditKind.Powerplay),
        ("ships", CreditKind.Ships),
        ("modules", CreditKind.Modules),
        ("upkeep", CreditKind.Upkeep),
        ("rebuy", CreditKind.Rebuy),
        ("fines", CreditKind.Fines),
        ("crew", CreditKind.Crew),
        ("on_foot", CreditKind.OnFoot),
        ("engineers", CreditKind.Engineers),
        ("carrier", CreditKind.Carrier),
    ];

    private const int CreditKindsNamed = 3;

    private const long UnaccountedSpoken = 1_000_000;

    private static readonly string[] CombatGroupings = ["enemy", "ship", "system", "faction"];

    private static readonly string[] CombatPeriods = ["day", "week", "month", "all"];

    private const int CombatEntriesNamed = 3;

    private const string ConflictZoneKills = "ships in conflict zones";

    private static readonly string[] BgsWorkQuestions =
    [
        "bgs work", "my bgs work", "what have i done for the bgs",
    ];

    private static readonly string[] MercCoins =
    [
        "how many merc coins do i have", "how many merc coins have i got", "my merc coins",
        "my merc coin balance", "what's my merc coin balance", "what is my merc coin balance",
    ];

    private static readonly string[] CrimeStatus =
    [
        "crime status", "am i wanted", "my bounties", "my fines", "what do i owe",
        "do i owe anything", "what bounties do i have", "what fines do i have",
    ];

    private static readonly IReadOnlyDictionary<string, string> BankAccount =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["section"] = "Bank_Account" };

    private static readonly string[] Ranks =
    [
        "what's my rank", "what is my rank", "my rank", "what are my ranks", "my ranks",
        "what rank am i", "how are my ranks",
    ];

    /// <summary>Each rank ladder as the Commander names it, keyed by the journal's own key.</summary>
    private static readonly (string Key, string Label)[] RankLadders =
    [
        ("Combat", "Combat"), ("Trade", "Trade"), ("Explore", "Exploration"), ("Soldier", "Mercenary"),
        ("Exobiologist", "Exobiologist"), ("CQC", "CQC"), ("Empire", "Imperial Navy"), ("Federation", "Federal Navy"),
    ];

    private static readonly string[] Standing =
    [
        "what's my reputation", "what is my reputation", "my reputation",
        "what's my standing", "what is my standing", "my standing",
    ];

    /// <summary>The superpowers, in the order they are reported, and the navy each one has.</summary>
    private static readonly (string Name, string? Navy)[] Superpowers =
    [
        ("Empire", "Imperial Navy"), ("Federation", "Federal Navy"), ("Alliance", null), ("Independent", null),
    ];

    /// <summary>"Reputation with the Empire", "imperial rank" and the like, each naming its superpower.</summary>
    private static IEnumerable<CapabilityKeyword> SuperpowerKeywords()
    {
        (string Superpower, string[] Phrases)[] spoken =
        [
            ("Empire", ["reputation with the empire", "reputation with empire", "standing with the empire",
                "standing with empire", "imperial rank", "imperial navy rank", "empire rank"]),
            ("Federation", ["reputation with the federation", "reputation with federation",
                "standing with the federation", "standing with federation", "federal rank", "federal navy rank",
                "federation rank"]),
            ("Alliance", ["reputation with the alliance", "reputation with alliance", "standing with the alliance",
                "standing with alliance"]),
            ("Independent", ["reputation with the independents", "reputation with independents",
                "standing with the independents", "standing with independents"]),
        ];

        foreach (var (superpower, phrases) in spoken)
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = superpower };

            foreach (var phrase in phrases)
            {
                yield return new CapabilityKeyword(phrase, "get_standing") { Arguments = arguments };
            }
        }
    }

    /// <summary>"What's my reputation with" each minor faction a reading is held for, by its whole name.</summary>
    public static IEnumerable<DynamicCommand> StandingPhrases(Func<CommanderGameState?> commander)
    {
        if (commander() is not { } active)
        {
            yield break;
        }

        foreach (var name in active.Reputation.Factions.Keys)
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = name };

            foreach (var shape in new[]
                     {
                         $"what's my reputation with {name}",
                         $"what is my reputation with {name}",
                         $"my reputation with {name}",
                         $"what's my standing with {name}",
                         $"my standing with {name}",
                     })
            {
                yield return new DynamicCommand(shape, Id, "get_standing", arguments);
            }
        }
    }

    /// <summary>The one answer every other one needs first.</summary>
    private static bool TryActive(GameStateStore gameState, out CommanderGameState active, out string reason)
    {
        if (gameState.Active is { } found)
        {
            active = found;
            reason = string.Empty;
            return true;
        }

        active = null!;
        reason = "No Elite Dangerous journal has been detected yet.";
        return false;
    }

    private static string DescribeLocation(GameStateStore gameState, NavRoute route)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var location = active.Location;

        if (location.StarSystem is null)
        {
            return $"{active.Identity.Name} — no position recorded yet this session.";
        }

        var where = location.Body is not null &&
                    !string.Equals(location.Body, location.StarSystem, StringComparison.OrdinalIgnoreCase)
            ? $"{location.StarSystem}, near {location.Body}"
            : location.StarSystem;

        var docking = location switch
        {
            { Docked: true, StationName: { } station } => $", docked at {station}",
            { Docked: true } => ", docked",
            _ => "",
        };

        var report = new StringBuilder($"{active.Identity.Name} is in {where}{docking}.");

        if (location.Mode is not (FlightMode.Unknown or FlightMode.Docked))
        {
            report.Append($" Currently {Speak(location.Mode)}.");
        }

        report.Append(location.StarClass is { } starClass
            ? $" The star here is {StarClasses.Speak(starClass)}."
            : " The star class here has not been seen in the journal.");

        AppendRoute(report, route, location.StarSystem);

        return report.ToString();
    }

    /// <summary>
    /// What is left of the plotted route, from <c>NavRoute.json</c> — the same source the Route Progress
    /// panel reads (#152).
    /// </summary>
    private static void AppendRoute(StringBuilder report, NavRoute route, string here)
    {
        if (!route.IsPlotted)
        {
            return;
        }

        // Both RouteProgress and NavRoute.Ahead count the whole route as ahead when the Commander is not
        // on it, so a jump count taken without this check overstates it.
        if (RouteProgress.For(route, here).OffRoute)
        {
            report.Append(" A route is plotted, but this system is not on it.");

            return;
        }

        var ahead = route.Ahead(here);

        if (ahead.Count == 0)
        {
            report.Append(" This is the last system on the plotted route.");

            return;
        }

        report.Append($" Next jump: {ahead[0].StarSystem}");
        report.Append(ahead[0].StarClass is { } starClass ? $" (class {starClass})." : ".");
        report.Append($" {ahead.Count} jump{(ahead.Count == 1 ? "" : "s")} left on the route.");
    }

    private static ToolResult DescribeShip(GameStateStore gameState, ToolArguments arguments)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return ToolResult.Ok(reason);
        }

        var named = arguments.TryGetString("ship", out var spoken) && !string.IsNullOrWhiteSpace(spoken)
            ? spoken.Trim()
            : null;
        var rangeOnly = arguments.TryGetBoolean("jump_range", out var wantsRange) && wantsRange;

        if (named is null)
        {
            // The flown ship where the journal has described it this session, and the one last remembered
            // for it otherwise (#337) — so a restart before Elite writes a fresh Loadout still answers,
            // rather than saying the Commander's own ship is unknown.
            var flown = active.FlownShip;

            if (!flown.IsKnown)
            {
                // Specific about why.
                return ToolResult.Ok(
                    "No Loadout event has been seen yet, so I do not know what you are flying. "
                    + "It is written when you enter the game or change your outfitting.");
            }

            var seenAt = active.Ship.IsKnown ? null : active.Loadouts.For(flown.ShipId)?.SeenAt;
            return rangeOnly
                ? JumpRangeOnly(flown, named: false, seenAt)
                : Report(active, flown, isFlown: true, seenAt);
        }

        if (Remembered(active, named) is not { } found)
        {
            return ToolResult.Ok(UnknownShip(active, named));
        }

        var isCurrent = active.Ship.IsKnown && active.Ship.ShipId == found.Loadout.ShipId;
        return rangeOnly
            ? JumpRangeOnly(found.Loadout, named: true, isCurrent ? null : found.SeenAt)
            : Report(active, found.Loadout, isFlown: isCurrent, isCurrent ? null : found.SeenAt);
    }

    private static ToolResult JumpRangeOnly(ShipLoadout ship, bool named, DateTimeOffset? seenAt)
    {
        var subject = named ? $" of {ship.Describe()}" : "";
        var content = ship.MaxJumpRange is { } range
            ? $"Unladen jump range{subject} is {range:0.##} ly{(seenAt is { } seen ? AsOf(seen) : "")}."
            : $"The jump range{subject} is not known yet.";

        return ship.ShipId is { } shipId ? ToolResult.Ok(content) with { Page = PageRef.Ship(shipId) } : ToolResult.Ok(content);
    }

    /// <summary>One ship's report: metrics and the fitted modules, from the live loadout or a remembered
    /// one — dated, and with no claim about the current cargo fill, when it is not (#108).</summary>
    private static ToolResult Report(CommanderGameState active, ShipLoadout ship, bool isFlown, DateTimeOffset? seenAt)
    {
        var report = new StringBuilder(isFlown ? $"Flying {ship.Describe()}" : $"{ship.Describe()}");

        if (ship.Ident is { } ident)
        {
            report.Append($", ident {ident}");
        }

        if (seenAt is { } seen)
        {
            report.Append(AsOf(seen));
        }

        report.AppendLine(".");

        var metrics = new List<string>();

        if (ship.MaxJumpRange is { } range)
        {
            metrics.Add($"maximum jump range {range:0.##} ly");
        }

        if (ship.FuelCapacity is { } tank)
        {
            metrics.Add($"fuel tank {tank:0.##} t");
        }

        if (ship.CargoCapacity is { } cargo)
        {
            // How full, not just how big (#329) — but only for the hold this session actually read, not a
            // remembered ship's last-seen capacity.
            metrics.Add(seenAt is null && active.Hold is { IsKnown: true, IsShip: true } hold
                ? $"cargo {hold.Count}/{cargo} t"
                : $"cargo capacity {cargo} t");
        }

        if (ship.UnladenMass is { } mass)
        {
            metrics.Add($"unladen mass {mass:0.#} t");
        }

        if (ship.HullHealth is { } hull)
        {
            metrics.Add($"hull {hull}%");
        }

        if (metrics.Count > 0)
        {
            report.AppendLine(string.Join(", ", metrics) + ".");
        }

        if (ship.Rebuy is { } rebuy)
        {
            report.AppendLine($"Rebuy {rebuy:N0} cr.");
        }

        var fitted = Fitted(ship);
        string? shortForm = null;

        if (fitted.Count > 0)
        {
            var modules = fitted.Select(entry => entry.Module).ToList();
            var head = report.ToString().TrimEnd();
            var tally = $"{modules.Count} modules fitted, {modules.Count(module => module.IsEngineered)} engineered.";

            report.AppendLine(tally);

            string? group = null;

            foreach (var (heading, slot, module) in fitted)
            {
                if (heading is not null && heading != group)
                {
                    group = heading;
                    report.AppendLine(heading + ":");
                }

                report.AppendLine($"  {slot}: {Fitting(module)}");
            }

            var unpowered = modules.Where(module => !module.Powered).ToList();

            if (unpowered.Count > 0)
            {
                // Worth stating unprompted: an unpowered module is one the Commander believes they have.
                report.AppendLine("Unpowered: " + string.Join(", ", unpowered.Select(Said)) + ".");
            }

            shortForm = $"{head} {tally}"
                        + (unpowered.Count > 0
                            ? $" {unpowered.Count} unpowered: {SpokenList.Names([.. unpowered.Select(Said)])}."
                            : "")
                        + " The full loadout is on Asset Mgmt › Ships.";
        }

        var content = report.ToString().TrimEnd();
        var result = shortForm is null ? ToolResult.Ok(content) : ToolResult.Ok(content, shortForm);

        return ship.ShipId is { } shipId ? result with { Page = PageRef.Ship(shipId) } : result;
    }

    /// <summary>
    /// The modules inside the hull's slot layout, in the outfitting screen's order with its headings, so
    /// cosmetics are left out. A hull with no layout gives every module in journal order, unheaded.
    /// </summary>
    private static List<(string? Heading, string Slot, ShipModule Module)> Fitted(ShipLoadout ship)
    {
        var layout = EliteSpecifications.Slots(ship.Type);

        if (layout.Count == 0)
        {
            return [.. ship.Modules.Select(module => ((string?)null, module.Slot, module))];
        }

        return
        [
            .. layout
                .Select(slot => (slot, module: ship.Modules.FirstOrDefault(module =>
                    string.Equals(module.Slot, slot.Name, StringComparison.OrdinalIgnoreCase))))
                .Where(pair => pair.module is not null)
                .Select(pair => ((string?)ShipSlot.Heading(pair.slot.Kind), pair.slot.Describe(), pair.module!)),
        ];
    }

    /// <summary>The module's name, then its blueprint, grade and experimental effect where it has them.</summary>
    private static string Fitting(ShipModule module)
    {
        if (module.Blueprint is not { } blueprint)
        {
            return Said(module);
        }

        var engineering = new List<string> { BlueprintCatalogue.NameOf(blueprint) ?? ModuleNames.Readable(blueprint) };

        if (module.BlueprintLevel is { } level)
        {
            engineering.Add($"grade {level}");
        }

        if (module.Experimental is { } experimental)
        {
            engineering.Add(BlueprintCatalogue.NameOf(module.ExperimentalSymbol) ?? experimental);
        }

        return $"{Said(module)} — {string.Join(", ", engineering)}";
    }

    private static string Said(ShipModule module) =>
        EliteSpecifications.ModuleName(module.Item) ?? ModuleNames.Readable(module.Item);

    /// <summary>
    /// A named ship among everything remembered — the one being flown included — matched by the
    /// Commander's own name for it first and by hull second. More than one hull of the same type keeps
    /// the one seen most recently (#108).
    /// </summary>
    private static RememberedShip? Remembered(CommanderGameState active, string spoken)
    {
        var remembered = active.Loadouts.Ships.Values.ToArray();
        var named = remembered.Select(ship => ship.Loadout.Name).OfType<string>().ToArray();

        if (named.Length > 0 && Catalogue.Match(named, spoken) is { } byName)
        {
            return remembered.First(
                ship => string.Equals(ship.Loadout.Name, byName, StringComparison.OrdinalIgnoreCase));
        }

        if (EliteSpecifications.Ship(spoken)?.Symbol is not { } symbol)
        {
            return null;
        }

        return remembered
            .Where(ship => string.Equals(ship.Loadout.Type, symbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(ship => ship.SeenAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// A ship named that no loadout has been read for — owned but never boarded, or not a ship the
    /// Commander is known to have at all (#108).
    /// </summary>
    private static string UnknownShip(CommanderGameState active, string spoken)
    {
        var owned = active.Fleet.Ships.ToArray();
        var named = owned.Select(ship => ship.Name).OfType<string>().ToArray();

        var match = named.Length > 0 && Catalogue.Match(named, spoken) is { } byName
            ? owned.FirstOrDefault(ship => string.Equals(ship.Name, byName, StringComparison.OrdinalIgnoreCase))
            : EliteSpecifications.Ship(spoken)?.Symbol is { } symbol
                ? owned.FirstOrDefault(ship => string.Equals(EliteSpecifications.HullSymbol(ship.Type) ?? ship.Type, symbol, StringComparison.OrdinalIgnoreCase))
                : null;

        if (match is not null)
        {
            return $"{match.Describe()} — no loadout has been read for it yet. That is written the next "
                   + "time you board it.";
        }

        var near = EliteSpecifications.NearShips(spoken);

        return near.Count > 0
            ? Catalogue.Unknown("ship", spoken, near)
            : $"I don't know a ship called '{spoken}'.";
    }

    /// <summary>When a remembered figure was last reported.</summary>
    private static string AsOf(DateTimeOffset when) => $", as of {when:yyyy-MM-dd HH:mm} UTC";

    /// <summary>
    /// The opening of every answer that would otherwise report an absence the walk over older journals
    /// has not ruled out, or null once it has ruled one out (#148). A walk that failed or was stopped read
    /// no older journal, so its silence is not evidence of absence either.
    /// </summary>
    private static string? Unread(HistoryState state) => state switch
    {
        HistoryState.Pending or HistoryState.Running =>
            "I have not finished reading the journal history yet, so ",
        HistoryState.Failed or HistoryState.Stopped =>
            "Reading the journal history did not finish, so ",
        _ => null,
    };

    /// <summary>The carrier, and the ships only when they were asked for (#406).</summary>
    private static string DescribeFleet(GameStateStore gameState, ToolArguments arguments, string? unread)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            // Ahead of the no-journal answer, which is the same claim of absence as the two below it (#148).
            return unread is { } opening ? $"{opening}I cannot answer about your fleet." : reason;
        }

        var report = new StringBuilder();
        var carrier = active.Carrier;

        // IsKnown rather than Owned, because a location recovered from history can arrive before any callsign
        // does (#406) — an unnamed carrier whose system is known is still an answer.
        if (carrier.IsKnown)
        {
            report.Append("Fleet carrier");

            if (carrier.Name is { } name)
            {
                report.Append(carrier.CallSign is { } sign ? $" {name} ({sign})" : $" {name}");
            }
            else if (carrier.CallSign is { } callsign)
            {
                report.Append($" {callsign}");
            }

            if (carrier.StarSystem is { Length: > 0 } system)
            {
                report.Append($" is in {system}");

                // And when that was reported, because it is remembered across sessions and a carrier can sit
                // in one system for weeks (#406).
                if (carrier.SeenAt is { } seen)
                {
                    report.Append(AsOf(seen));
                }
            }
            else
            {
                report.Append(" location unknown");
            }

            if (carrier.DestinationSystem is { } destination)
            {
                report.Append($", jumping to {destination}");

                if (carrier.DepartureTime is { } departure)
                {
                    report.Append($" at {departure:HH:mm} UTC");
                }
            }

            report.AppendLine(".");
        }
        else if (unread is { } opening)
        {
            report.AppendLine($"{opening}I cannot say whether you have a fleet carrier.");
        }
        else
        {
            // Precise about the limit of the evidence, and the limit is the folder rather than the session
            // (#406).
            report.AppendLine("No fleet carrier appears in any journal read, so I do not know where one is.");
        }

        if (!arguments.TryGetBoolean("ships", out var wantsShips) || !wantsShips)
        {
            return report.ToString().TrimEnd();
        }

        var fleet = active.Fleet;

        if (!fleet.IsKnown)
        {
            report.AppendLine(unread is { } opening
                ? $"{opening}I do not have your ship list."
                : "I have no ship list yet — it is written when you dock at a station with a shipyard.");
        }
        else
        {
            if (active.Ship.IsKnown)
            {
                report.AppendLine($"Currently flying {active.Ship.Describe()}.");
            }
            else if (active.LastFlownShip is { } lastFlown)
            {
                report.AppendLine($"Last flown {lastFlown.Describe()}; you are logged out.");
            }

            if (fleet.Ships.Count == 0)
            {
                report.AppendLine("No other ships stored.");
            }
            else
            {
                // Dated, exactly as stored modules already are (remediation.md 14, item 3).
                report.AppendLine(
                    $"{fleet.Ships.Count} other ship{(fleet.Ships.Count == 1 ? "" : "s")} stored"
                    + (fleet.TakenAt is { } read ? AsOf(read) : string.Empty)
                    + ":");

                // One ship to a line under the system that holds them, rather than eleven names run together
                // after a colon (#406).
                foreach (var group in fleet.Ships
                             .GroupBy(ship => ship.StarSystem, StringComparer.OrdinalIgnoreCase)
                             .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    report.AppendLine($"  {group.Key}:");

                    foreach (var ship in group)
                    {
                        report.AppendLine($"    {ship.Describe()}");
                    }
                }

                var inTransit = fleet.Ships.Where(ship => ship.InTransit).ToArray();

                if (inTransit.Length > 0)
                {
                    // Past tense, and it says why (remediation.md 14, item 3). "In transit" was read out as a
                    // fact about now — "Sacred Fire is mid-manoeuvre" — of a transfer that had landed the day
                    // before.
                    report.AppendLine(
                        "In transit when that was read: "
                        + string.Join(", ", inTransit.Select(ship => ship.Describe()))
                        + ". A transfer that has landed since would look exactly the same here, so "
                        + "do not say one is still moving.");
                }
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Every ship as it was last seen fitted, from the remembered loadouts rather than from the one
    /// being flown. The figures are the game's own, so ranking ships against each other needs no model
    /// of jump range.
    /// </summary>
    private static string DescribeFleetLoadouts(
        GameStateStore gameState,
        ToolArguments arguments,
        string? unread)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return unread is { } opening ? $"{opening}I have no loadouts to report." : reason;
        }

        var remembered = active.Loadouts.Ships.ToList();
        var report = new StringBuilder();

        if (remembered.Count == 0)
        {
            report.AppendLine(unread is { } opening
                ? $"{opening}I have no loadouts to report."
                : "I have not read a loadout for any of your ships yet. One is written each time you board "
                  + "a ship, and D47 reads your journals for them when it starts.");

            Uncovered(report, active);
            return report.ToString().TrimEnd();
        }

        var candidates = remembered;
        var fits = new Dictionary<int, MiningFit>();
        var notes = new StringBuilder();

        if (arguments.TryGetBoolean("mining", out var mining) && mining)
        {
            var unseen = new List<string>();
            var unfit = new List<string>();

            foreach (var ship in remembered)
            {
                switch (MiningFit.For(ship.Value.Loadout))
                {
                    case null:
                        unseen.Add(ship.Value.Loadout.Describe() ?? "unnamed ship");
                        break;
                    case { IsFit: true } fit:
                        fits[ship.Key] = fit;
                        break;
                    case var lacking:
                        unfit.Add($"{ship.Value.Loadout.Describe()} ({lacking.Evidence})");
                        break;
                }
            }

            candidates = [.. remembered.Where(ship => fits.ContainsKey(ship.Key))];

            if (unfit.Count > 0)
            {
                notes.AppendLine($"Not fitted for mining: {string.Join("; ", unfit)}.");
            }

            if (unseen.Count > 0)
            {
                notes.AppendLine($"Not seen fitted, so not covered: {string.Join(", ", unseen)}.");
            }

            if (candidates.Count == 0)
            {
                report.AppendLine("No ship I remember is fitted for tritium mining.");
                report.Append(notes);
                Uncovered(report, active);
                return report.ToString().TrimEnd();
            }
        }

        var wanted = candidates;

        if (arguments.TryGetInt32("min_cargo", out var minimum))
        {
            wanted = [.. candidates.Where(ship => ship.Value.Loadout.CargoCapacity >= minimum)];
        }

        if (wanted.Count == 0)
        {
            var largest = candidates
                .Select(ship => ship.Value.Loadout)
                .Where(loadout => loadout.CargoCapacity is not null)
                .OrderByDescending(loadout => loadout.CargoCapacity)
                .FirstOrDefault();

            report.AppendLine(largest is null
                ? $"None of the {remembered.Count} ships I remember reports a cargo capacity at all."
                : $"No ship I remember carries {minimum} t. The largest hold is "
                  + $"{largest.Describe()} at {largest.CargoCapacity} t.");

            Uncovered(report, active);
            return report.ToString().TrimEnd();
        }

        var order = arguments.TryGetString("order_by", out var asked) ? asked : string.Empty;

        var listed = order switch
        {
            "jump_range" => wanted.OrderByDescending(ship => ship.Value.Loadout.MaxJumpRange ?? double.MinValue),
            "cargo" => wanted.OrderByDescending(ship => (double?)ship.Value.Loadout.CargoCapacity ?? double.MinValue),
            _ => wanted.OrderBy(ship => ship.Value.Loadout.Describe() ?? string.Empty, StringComparer.OrdinalIgnoreCase),
        };

        report.AppendLine(
            $"{wanted.Count} ship{(wanted.Count == 1 ? string.Empty : "s")}, each as it was last seen fitted "
            + "— refit one and it reads as it was until you board it again. Jump range is the maximum with "
            + "one jump's fuel and an empty hold, as the game reports it, so a full tank or a laden hold "
            + "is shorter.");

        foreach (var ship in listed)
        {
            var loadout = ship.Value.Loadout;
            var figures = new List<string>();

            if (loadout.CargoCapacity is { } cargo)
            {
                figures.Add($"cargo {cargo} t");
            }

            if (loadout.MaxJumpRange is { } jump)
            {
                figures.Add($"jump {jump:0.##} ly");
            }

            if (loadout.UnladenMass is { } mass)
            {
                figures.Add($"unladen mass {mass:0.#} t");
            }

            if (loadout.FuelCapacity is { } fuel)
            {
                figures.Add($"fuel {fuel:0.##} t");
            }

            if (loadout.TotalValue is { } worth)
            {
                figures.Add($"worth {worth:N0} cr");
            }

            if (loadout.Rebuy is { } rebuy)
            {
                figures.Add($"rebuy {rebuy:N0} cr");
            }

            report.Append($"  {loadout.Describe()} — {WhereShipIs(active, ship.Key)}");

            if (figures.Count > 0)
            {
                report.Append(", " + string.Join(", ", figures));
            }

            if (fits.TryGetValue(ship.Key, out var evidence))
            {
                report.Append($", {evidence.Evidence}");
            }

            report.AppendLine(AsOf(ship.Value.SeenAt));
        }

        report.Append(notes);
        Uncovered(report, active);
        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Ships the Commander owns that no loadout has been read for, named so that a ranking is not
    /// mistaken for the whole fleet.
    /// </summary>
    private static void Uncovered(StringBuilder report, CommanderGameState active)
    {
        var missing = active.Fleet.Ships
            .Where(ship => !active.Loadouts.Ships.ContainsKey(ship.ShipId))
            .Select(ship => ship.Describe())
            .ToArray();

        if (missing.Length > 0)
        {
            report.AppendLine($"No loadout read, so not covered above: {string.Join(", ", missing)}.");
        }
    }

    /// <summary>Where a remembered ship is, by the journal's account of the fleet.</summary>
    private static string WhereShipIs(CommanderGameState active, int shipId)
    {
        if (active.Ship.IsKnown && active.Ship.ShipId == shipId)
        {
            return "you are flying it";
        }

        return active.Fleet.Ships.FirstOrDefault(ship => ship.ShipId == shipId) switch
        {
            { InTransit: true } => "in transit",
            { HasSystem: true } stored => stored.StarSystem,
            _ => "location unknown",
        };
    }

    /// <summary>What is in module storage, grouped by where it is.</summary>
    private static ToolResult DescribeStoredModules(GameStateStore gameState, ToolArguments arguments)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return ToolResult.Ok(reason);
        }

        var store = active.Modules;

        if (!store.IsKnown)
        {
            // The same shape of answer the fleet gives, and true for the same reason: the event is written on
            // docking somewhere with outfitting, so silence before that is missing evidence rather than an
            // empty store.
            return ToolResult.Ok(
                "I have no module storage list yet — it is written when you dock at a station with outfitting.");
        }

        var wanted = arguments.TryGetString("module", out var fragment) && !string.IsNullOrWhiteSpace(fragment)
            ? fragment.Trim()
            : null;

        var modules = wanted is null ? store.Modules : store.Matching(wanted);

        if (modules.Count == 0)
        {
            return ToolResult.Ok(wanted is null
                ? "No modules in storage."
                : $"Nothing in storage matches '{wanted}'. {store.Modules.Count} module"
                  + $"{(store.Modules.Count == 1 ? " is" : "s are")} stored in total.");
        }

        var report = new StringBuilder();

        report.Append(wanted is null
            ? $"{modules.Count} module{(modules.Count == 1 ? "" : "s")} in storage"
            : $"{modules.Count} stored module{(modules.Count == 1 ? "" : "s")} match '{wanted}'");

        report.AppendLine(store.TakenAt is { } taken ? AsOf(taken) + "." : ".");
        var headline = report.ToString().TrimEnd().TrimEnd('.');

        foreach (var group in modules
                     .Where(module => !module.InTransit)
                     .GroupBy(module => module.StarSystem, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var here = store.SnapshotSystem is not null
                       && string.Equals(group.Key, store.SnapshotSystem, StringComparison.OrdinalIgnoreCase);

            report.AppendLine();
            report.AppendLine(here ? $"{group.Key} (where you are):" : $"{group.Key}:");

            foreach (var module in group)
            {
                report.Append($"  {module.Describe()}");

                if (!here && module.TransferCost is > 0)
                {
                    report.Append($" — {module.TransferCost.Value:N0} cr to transfer");

                    if (module.TransferTime is > 0)
                    {
                        report.Append($", {Duration(module.TransferTime.Value)}");
                    }
                }

                report.AppendLine();
            }
        }

        var moving = modules.Where(module => module.InTransit).ToArray();

        if (moving.Length > 0)
        {
            report.AppendLine();
            report.AppendLine("In transit: " + string.Join(", ", moving.Select(module => module.Describe())) + ".");
        }

        var stored = modules.Where(module => !module.InTransit).ToList();
        var systems = stored.Select(module => module.StarSystem).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        var origin = store.SnapshotSystem;
        var named = stored
            .OrderBy(module => origin is not null
                               && string.Equals(module.StarSystem, origin, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(module => module.StarSystem, StringComparer.OrdinalIgnoreCase)
            .Select(module => module.Describe())
            .ToList();

        var spoken = headline
                     + (named.Count > 0
                         ? $", in {systems} system{(systems == 1 ? "" : "s")}: {SpokenList.Names(named)}."
                         : ".")
                     + (moving.Length > 0 ? $" {moving.Length} in transit." : "");

        return ToolResult.Ok(report.ToString().TrimEnd(), spoken);
    }

    /// <summary>A transfer time in words.</summary>
    private static string Duration(int seconds) => seconds switch
    {
        < 90 => $"{seconds} seconds",
        < 5400 => $"{Math.Round(seconds / 60.0)} minutes",
        _ => $"{Math.Round(seconds / 3600.0, 1)} hours",
    };

    private static string DescribeMaterials(GameStateStore gameState)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var report = new StringBuilder();
        var materials = active.Materials;

        if (materials.DistinctCount == 0)
        {
            report.AppendLine("No materials recorded yet.");
        }
        else
        {
            report.AppendLine(
                $"{materials.TotalCount} units across {materials.DistinctCount} materials"
                + (materials.SnapshotSeen ? "." : ", counted from this session's events only."));

            foreach (var category in (ReadOnlySpan<MaterialCategory>)
                     [MaterialCategory.Raw, MaterialCategory.Manufactured, MaterialCategory.Encoded])
            {
                var held = materials.InCategory(category);

                if (held.Count == 0)
                {
                    continue;
                }

                // The top few, not all of them.
                var top = held.Take(8).Select(holding => $"{holding.Speak()} {holding.Count}");
                var tail = held.Count > 8 ? $", and {held.Count - 8} more" : "";
                report.AppendLine($"{category}: {string.Join(", ", top)}{tail}.");
            }
        }

        var suit = active.Suit;

        if (suit.IsKnown)
        {
            report.AppendLine(
                $"On foot: {suit.BackpackCount} items in the backpack, {suit.ShipLockerCount} in the ship locker.");

            foreach (var kind in SuitInventory.Kinds)
            {
                var carried = suit.BackpackOf(kind);

                if (carried.Count > 0)
                {
                    report.AppendLine(
                        $"  Backpack {kind.ToLowerInvariant()}: "
                        + string.Join(", ", carried.Select(item => $"{item.Speak()} {item.Count}")) + ".");
                }
            }
        }

        return report.ToString().TrimEnd();
    }

    private static string DescribeSession(GameStateStore gameState)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var session = active.Session;

        if (!session.IsKnown)
        {
            return "No session has started yet — I have not seen you enter the game.";
        }

        var report = new StringBuilder();

        report.Append(session.Elapsed is { } elapsed
            ? $"Session running {elapsed.TotalHours:0.#} hours."
            : "Session just started.");

        report.AppendLine();

        var earnings = new List<string>();
        Add(earnings, "bounties", session.BountyEarnings);
        Add(earnings, "combat bonds", session.CombatBondEarnings);
        Add(earnings, "trade", session.TradeEarnings);
        Add(earnings, "exploration", session.ExplorationEarnings);
        Add(earnings, "missions", session.MissionEarnings);
        Add(earnings, "vouchers", session.VoucherEarnings);

        report.AppendLine(earnings.Count > 0
            ? $"Earned {session.TotalEarnings:N0} cr — {string.Join(", ", earnings)}."
            : "Nothing earned yet.");

        if (session.Jumps > 0)
        {
            report.AppendLine(
                $"{session.Jumps} jump{(session.Jumps == 1 ? "" : "s")}, "
                + $"{session.DistanceTravelled:0.#} ly travelled.");
        }

        if (session.BodiesScanned > 0)
        {
            report.AppendLine($"{session.BodiesScanned} bodies scanned.");
        }

        if (session.MaterialsGained > 0)
        {
            report.AppendLine($"{session.MaterialsGained} material units collected.");
        }

        if (session.Influence.Describe() is { } influence)
        {
            report.AppendLine(influence);
        }

        if (session.Interdictions > 0)
        {
            report.AppendLine($"{session.Interdictions} interdiction{(session.Interdictions == 1 ? "" : "s")}.");
        }

        if (session.Deaths > 0)
        {
            report.AppendLine($"{session.Deaths} death{(session.Deaths == 1 ? "" : "s")}.");
        }

        if (session.Balance is { } balance)
        {
            report.AppendLine($"Balance at session start: {balance:N0} cr.");
        }

        return report.ToString().TrimEnd();

        static void Add(List<string> into, string label, long amount)
        {
            if (amount > 0)
            {
                into.Add($"{amount:N0} {label}");
            }
        }
    }

    private static string DescribeCreditLedger(
        GameStateStore gameState, HistoryState history, ToolArguments arguments, DateTimeOffset now)
    {
        if (history is HistoryState.Pending or HistoryState.Running)
        {
            return "I have not finished reading the journal history yet.";
        }

        if (gameState.Active is not { } active)
        {
            return "I do not know which Commander is flying yet.";
        }

        var period = arguments.TryGetString("period", out var asked) ? asked.Trim().ToLowerInvariant() : "week";
        var kindName = arguments.TryGetString("kind", out var named) ? named.Trim().ToLowerInvariant() : string.Empty;
        CreditKind? kind = null;

        if (kindName.Length > 0)
        {
            kind = CreditKinds.Where(pair => pair.Name == kindName).Select(pair => (CreditKind?)pair.Kind).FirstOrDefault();

            if (kind is null)
            {
                return $"I can break credits down by {string.Join(", ", CreditKinds.Select(pair => pair.Name.Replace('_', ' ')))}.";
            }
        }

        var (from, span) = period switch
        {
            "day" => ((DateTimeOffset?)(now - TimeSpan.FromHours(24)), "24 hours"),
            "month" => (now - TimeSpan.FromDays(30), "30 days"),
            "all" => (null, string.Empty),
            _ => (now - TimeSpan.FromDays(7), "7 days"),
        };

        var ledger = active.CreditLedger;
        var slice = ledger.Since(from);
        var unfinished = history is HistoryState.Failed or HistoryState.Stopped
            ? "Reading the journal history did not finish, so this is only what the live journal shows."
            : string.Empty;
        var carrierBank = slice.ByKind.FirstOrDefault(total => total.Kind == CreditKind.CarrierBank);
        var totals = slice.ByKind.Where(total => total.Kind != CreditKind.CarrierBank).ToList();
        string report;

        if (ledger.Entries.Count == 0)
        {
            report = "No credit transactions in the journals on this PC.";
        }
        else if (kind is { } only)
        {
            var total = totals.FirstOrDefault(entry => entry.Kind == only);
            var label = kindName.Replace('_', ' ');
            var heading = from is null ? $"{Capitalised(label)} on record" : $"{Capitalised(label)} in the last {span}";

            report = total is null
                ? $"No {label} credits {(from is null ? "on record" : $"in the last {span}")}."
                : $"{heading}: {SpokenCredits.Band(total.In)} in, {SpokenCredits.Band(total.Out)} out, from "
                    + $"{total.Count.ToString("N0", CultureInfo.InvariantCulture)} {(total.Count == 1 ? "transaction" : "transactions")}.";
        }
        else if (totals.Count == 0 && carrierBank is null && Math.Abs(slice.Unaccounted) < UnaccountedSpoken)
        {
            report = $"No credits moved in the last {span}.";
        }
        else
        {
            var covers = from is null
                ? ledger.OnRecordSince is { } since
                    ? $"On record since {since.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
                    : "On record"
                : $"In the last {span}";
            var text = new StringBuilder(
                $"{covers}: {SpokenCredits.Band(totals.Sum(total => total.In))} in, "
                + $"{SpokenCredits.Band(totals.Sum(total => total.Out))} out.");

            if (ledger.Balance is { } balance)
            {
                text.Append($" Balance {SpokenCredits.Band(balance)}.");
            }

            text.AppendLine();
            text.AppendLine(CreditTop(
                "Most in", [.. totals.Where(total => total.In > 0).OrderByDescending(total => total.In).Select(total => (total.Kind, total.In))]));
            text.AppendLine(CreditTop(
                "Most out", [.. totals.Where(total => total.Out > 0).OrderByDescending(total => total.Out).Select(total => (total.Kind, total.Out))]));

            if (carrierBank is not null)
            {
                var moved = new List<string>();

                if (carrierBank.Out > 0)
                {
                    moved.Add($"{SpokenCredits.Band(carrierBank.Out)} to your carrier");
                }

                if (carrierBank.In > 0)
                {
                    moved.Add($"{SpokenCredits.Band(carrierBank.In)} back");
                }

                if (moved.Count > 0)
                {
                    text.AppendLine($"You moved {string.Join(" and ", moved)}.");
                }
            }

            if (Math.Abs(slice.Unaccounted) >= UnaccountedSpoken)
            {
                text.AppendLine(
                    $"Your balance was {SpokenCredits.Band(Math.Abs(slice.Unaccounted))} "
                    + $"{(slice.Unaccounted > 0 ? "more" : "less")} than the journal accounts for, such as credits "
                    + "from another Commander or a carrier's scrap refund.");
            }

            report = text.ToString().TrimEnd();
        }

        return unfinished.Length == 0 ? report : $"{report} {unfinished}";
    }

    private static string DescribeCodexLedger(GameStateStore gameState, HistoryState history, ToolArguments arguments)
    {
        if (history is HistoryState.Pending or HistoryState.Running)
        {
            return "I have not finished reading the journal history yet.";
        }

        if (gameState.Active is not { } active)
        {
            return "I do not know which Commander is flying yet.";
        }

        var regionAsked = arguments.TryGetString("region", out var asked) ? asked.Trim() : string.Empty;
        int? regionId = null;
        string? regionName = null;

        if (regionAsked.Length > 0)
        {
            var (id, name, refusal) = ResolveCodexRegion(active, regionAsked);

            if (refusal is not null)
            {
                return refusal;
            }

            (regionId, regionName) = (id, name);
        }

        var ledger = active.CodexLedger;
        var unfinished = history is HistoryState.Failed or HistoryState.Stopped
            ? " Reading the journal history did not finish, so this is only what the live journal shows."
            : string.Empty;

        if (ledger.Entries.Count == 0)
        {
            return "No codex entries in the journals on this PC." + unfinished;
        }

        var period = arguments.TryGetString("period", out var named) ? named.Trim().ToLowerInvariant() : "trip";
        var (from, covers) = period switch
        {
            "session" when ledger.SessionStart is { } start => ((DateTimeOffset?)start, "this session"),
            "all" or "session" => (null, OnRecord(ledger)),
            _ when ledger.TripStart is { } sale =>
                (sale, $"since you sold exploration data on {sale.UtcDateTime.ToString("d MMMM", CultureInfo.InvariantCulture)}"),
            _ => (null, OnRecord(ledger)),
        };

        var items = ledger.Entries
            .Where(entry => (from is null || entry.At >= from) && (regionId is null || entry.RegionId == regionId))
            .ToList();

        if (items.Count == 0)
        {
            return $"No codex entries{(regionName is null ? string.Empty : $" in {regionName}")} {covers}.{unfinished}";
        }

        var newCount = items.Count(entry => entry.IsNew);
        var logged = items.Select(entry => (entry.EntryId, entry.RegionId)).Distinct().Count();
        var heading = regionName is null ? Capitalised(covers) : $"In {regionName}, {covers}";
        var text = new StringBuilder(
            $"{heading}: {newCount.ToString("N0", CultureInfo.InvariantCulture)} new codex "
            + $"{(newCount == 1 ? "entry" : "entries")}, {logged.ToString("N0", CultureInfo.InvariantCulture)} logged in all.");

        text.AppendLine();
        text.Append(CodexTop(
            "Most new",
            [.. items.Where(entry => entry.IsNew)
                .GroupBy(entry => (entry.Category ?? "uncategorised").ToLowerInvariant())
                .Select(group => (Name: group.Key, Count: group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Name, StringComparer.Ordinal)
                .Select(group => $"{group.Count.ToString("N0", CultureInfo.InvariantCulture)} {group.Name}")],
            "Most new: nothing."));

        if (regionId is null)
        {
            var regions = items
                .Where(entry => entry.RegionId is { } id && GalacticRegions.NameOf(id) is not null)
                .GroupBy(entry => entry.RegionId!.Value)
                .ToList();

            if (regions.Count > 1)
            {
                var ranked = regions
                    .Select(group => (Name: GalacticRegions.NameOf(group.Key)!, Count: group.Count(entry => entry.IsNew)))
                    .Where(group => group.Count > 0)
                    .OrderByDescending(group => group.Count)
                    .ThenBy(group => group.Name, StringComparer.Ordinal)
                    .Select(group => $"{group.Count.ToString("N0", CultureInfo.InvariantCulture)} in {group.Name}")
                    .ToList();

                if (ranked.Count > 0)
                {
                    text.AppendLine();
                    text.Append(CodexTop($"In {ranked.Count} {(ranked.Count == 1 ? "region" : "regions")}", ranked, string.Empty));
                }
            }
        }

        return text + unfinished;
    }

    private static (int? Id, string? Name, string? Refusal) ResolveCodexRegion(CommanderGameState active, string asked)
    {
        if (asked.Equals("here", StringComparison.OrdinalIgnoreCase))
        {
            var here = active.Location.StarPos is { } position ? GalacticRegions.Find(position) : null;
            var hereId = here is null ? null : GalacticRegions.IdOf(here);

            return hereId is null ? (null, null, "I do not know which region you are in.") : (hereId, here, null);
        }

        return GalacticRegions.IdOf(asked) is { } id
            ? (id, GalacticRegions.NameOf(id), null)
            : (null, null, $"I do not know a region called {asked}.");
    }

    private static string DescribeCodexDiscoveries(GameStateStore gameState, HistoryState history, ToolArguments arguments)
    {
        if (history is HistoryState.Pending or HistoryState.Running)
        {
            return "I have not finished reading the journal history yet.";
        }

        if (gameState.Active is not { } active)
        {
            return "I do not know which Commander is flying yet.";
        }

        var asked = arguments.TryGetString("region", out var given) && given.Trim().Length > 0 ? given.Trim() : "here";
        var (regionId, regionName, refusal) = ResolveCodexRegion(active, asked);

        if (refusal is not null || regionId is not { } id)
        {
            return refusal!;
        }

        var unfinished = history is HistoryState.Failed or HistoryState.Stopped
            ? " Reading the journal history did not finish, so this is only what the live journal shows."
            : string.Empty;
        var discoveries = CodexDiscoveries.For(active.CodexLedger, CodexCatalogue.Table, id);

        if (discoveries.Known.Count == 0)
        {
            return $"The codex table lists no entries for {regionName}.{unfinished}";
        }

        var number = (int count) => count.ToString("N0", CultureInfo.InvariantCulture);
        var text = new StringBuilder(
            $"In {regionName}, {number(discoveries.Known.Count)} codex {(discoveries.Known.Count == 1 ? "entry is" : "entries are")} known: "
            + $"{number(discoveries.Logged.Count)} logged, {number(discoveries.Missing.Count)} missing.");

        var bySubcategory = discoveries.Known
            .GroupBy(item => item.Entry.SubCategory)
            .Select(group => (Name: group.Key.ToLowerInvariant(), Missing: group.Count(item => item.Logged is null), Total: group.Count()))
            .Where(group => group.Missing > 0)
            .OrderByDescending(group => group.Missing)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .Select(group => $"{number(group.Missing)} of {number(group.Total)} {group.Name}")
            .ToList();

        if (bySubcategory.Count > 0)
        {
            text.AppendLine();
            text.Append(CodexTop("Missing by kind", bySubcategory, string.Empty));
        }

        if (discoveries.Missing.Count > 0)
        {
            text.AppendLine();
            text.Append(CodexTop(
                "Most reported missing",
                [.. discoveries.Missing.Select(item => item.Entry.Name)],
                string.Empty));
        }

        text.AppendLine();
        text.Append(
            $"The record runs {OnRecord(active.CodexLedger)}, so an entry logged before then reads as missing. "
            + CodexDiscoveries.Coverage);

        if (discoveries.Uncovered.Count > 0)
        {
            text.Append($" You logged {number(discoveries.Uncovered.Count)} more here that the codex table does not list.");
        }

        return text + unfinished;
    }

    private static string DescribeBodyGeology(GameStateStore gameState, HistoryState history, ToolArguments arguments)
    {
        if (history is HistoryState.Pending or HistoryState.Running)
        {
            return "I have not finished reading the journal history yet.";
        }

        if (gameState.Active is not { } active)
        {
            return "I do not know which Commander is flying yet.";
        }

        var wanted = arguments.TryGetString("body", out var given) && !string.IsNullOrWhiteSpace(given)
            ? given.Trim()
            : null;
        var body = wanted is not null ? active.Bodies.Named(wanted) : active.Bodies.MostRecentWithGeology;

        if (body is null)
        {
            return wanted is not null
                ? $"I have no scan of {wanted}, so I cannot say which geology it likely has."
                : "I have not seen a body with geological signals.";
        }

        var name = BiologyCallout.Short(body.BodyName, active.Location.StarSystem);
        var count = body.GeologicalCount;

        if (count == 0)
        {
            return $"{name} shows no geological signals.";
        }

        var scan = body.SystemAddress is { } systemAddress && body.BodyId is { } bodyId
            ? active.Scans.For(systemAddress, bodyId)
            : null;

        if (scan is null)
        {
            return $"I have no scan of {name}, so I cannot say which geology it likely has.";
        }

        var signals = $"{count} geological signal{(count == 1 ? string.Empty : "s")}";
        var likely = GeologyCatalogue.Likely(scan);

        if (likely.Count == 0)
        {
            return $"{name} has {signals}, but the geology table has no feature for its class and volcanism.";
        }

        var regionId = active.Location.StarPos is { } position && GalacticRegions.Find(position) is { } region
            ? GalacticRegions.IdOf(region)
            : null;
        var ledger = active.CodexLedger;
        var features = likely
            .Select(feature =>
            {
                var plural = feature.Name.ToLowerInvariant() + "s";

                return regionId is null
                    ? plural
                    : ledger.Logged.Contains((feature.EntryId, regionId))
                        ? $"{plural}, logged in this region"
                        : $"{plural}, not logged in this region";
            })
            .ToList();
        var list = features.Count == 1
            ? features[0]
            : string.Join(", ", features[..^1]) + (regionId is null ? " and " : ", and ") + features[^1];
        var text = $"{name} has {signals}. Likely: {list}.";

        if (regionId is null)
        {
            return text;
        }

        text += $" The record runs {OnRecord(ledger)}, so an entry logged before then reads as not logged.";

        return history is HistoryState.Failed or HistoryState.Stopped
            ? text + " Reading the journal history did not finish, so this is only what the live journal shows."
            : text;
    }

    private static string OnRecord(CodexLedger ledger) =>
        ledger.OnRecordSince is { } since
            ? $"on record since {since.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
            : "on record";

    private static string CodexTop(string heading, IReadOnlyList<string> groups, string none)
    {
        if (groups.Count == 0)
        {
            return none;
        }

        var named = groups.Take(CodexGroupsNamed).ToList();
        var rest = groups.Count - named.Count;

        if (rest > 0)
        {
            named.Add($"and {rest} more");
        }

        return $"{heading}: {string.Join(", ", named)}.";
    }

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string CreditTop(string heading, IReadOnlyList<(CreditKind Kind, long Amount)> kinds)
    {
        if (kinds.Count == 0)
        {
            return $"{heading}: nothing.";
        }

        var named = kinds.Take(CreditKindsNamed)
            .Select(entry => $"{CreditKindLabel(entry.Kind)} {SpokenCredits.Band(entry.Amount)}")
            .ToList();
        var rest = kinds.Count - named.Count;

        if (rest > 0)
        {
            named.Add($"and {rest} more");
        }

        return $"{heading}: {string.Join(", ", named)}.";
    }

    private static string CreditKindLabel(CreditKind kind) => kind switch
    {
        CreditKind.Combat => "bounties and bonds redeemed",
        CreditKind.CommunityGoals => "community goals",
        CreditKind.OnFoot => "on foot",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private const int BgsPairsNamed = 3;

    private readonly record struct CombatEntry(string Label, int Kills, long Awarded);

    private static string DescribeCombatStats(
        GameStateStore gameState, HistoryState history, ToolArguments arguments, DateTimeOffset now)
    {
        if (history is HistoryState.Pending or HistoryState.Running)
        {
            return "I have not finished reading the journal history yet.";
        }

        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var by = arguments.TryGetString("by", out var grouping) ? grouping.Trim().ToLowerInvariant() : string.Empty;
        var period = arguments.TryGetString("period", out var asked) ? asked.Trim().ToLowerInvariant() : "all";
        var name = arguments.TryGetString("name", out var fragment) && !string.IsNullOrWhiteSpace(fragment)
            ? fragment.Trim()
            : null;

        if (by.Length > 0 && !CombatGroupings.Contains(by))
        {
            return $"I can break combat stats down by {string.Join(", ", CombatGroupings)}.";
        }

        var (from, span) = period switch
        {
            "day" => ((DateTimeOffset?)(now - TimeSpan.FromHours(24)), "24 hours"),
            "week" => (now - TimeSpan.FromDays(7), "7 days"),
            "month" => (now - TimeSpan.FromDays(30), "30 days"),
            _ => (null, string.Empty),
        };

        var record = active.Combat;
        var slice = record.Since(from);
        var unfinished = history is HistoryState.Failed or HistoryState.Stopped
            ? "Reading the journal history did not finish, so this is only what the live journal shows."
            : string.Empty;

        if (slice.Kills == 0)
        {
            var none = from is null ? "No kills in the journals on this PC." : $"No kills in the last {span}.";

            return unfinished.Length == 0 ? none : $"{none} {unfinished}";
        }

        var report = new StringBuilder();
        var covers = from is null
            ? record.OnRecordSince is { } since
                ? $"On record since {since.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
                : "On record"
            : $"In the last {span}";

        report.AppendLine(
            $"{covers}: {slice.Kills.ToString("N0", CultureInfo.InvariantCulture)} {(slice.Kills == 1 ? "kill" : "kills")}, "
            + $"{slice.Awarded.ToString("N0", CultureInfo.InvariantCulture)} credits awarded.");

        var enemies = CombatEntries(slice.ByTarget, key => key is null ? ConflictZoneKills : record.TargetName(key));
        var ships = CombatEntries(slice.ByVehicle, key => key.Vehicle switch
        {
            CombatVehicle.Srv => "in an SRV",
            CombatVehicle.OnFoot => "on foot",
            _ => (key.ShipId is { } id ? record.ShipName(id) : null) ?? "a ship not in your journals",
        });
        var systems = CombatEntries(
            slice.BySystem, key => (key is { } address ? record.SystemName(address) : null) ?? "a system not in your journals");
        var against = CombatEntries(slice.ByVictimFaction, key => key ?? "a faction not in your journals");
        var paying = CombatEntries(slice.ByPayingFaction, key => key)
            .OrderByDescending(entry => entry.Awarded)
            .ToList();

        switch (by)
        {
            case "enemy":
                CombatList(report, enemies, name, "ship type", "ship types", bond: true);
                break;
            case "ship":
                CombatList(report, ships, name, "ship", "ships");
                break;
            case "system":
                CombatList(report, systems, name, "system", "systems");
                break;
            case "faction":
                CombatList(report, against, name, "faction", "factions", heading: "Against");
                CombatList(report, paying, name, "faction", "factions", heading: "Paid by", credits: true);
                break;
            default:
                var tops = new List<string>();

                if (enemies.Count > 0)
                {
                    tops.Add($"Most destroyed: {enemies[0].Label}, {enemies[0].Kills}.");
                }

                if (systems.Count > 0)
                {
                    tops.Add($"Most in {systems[0].Label}, {systems[0].Kills}.");
                }

                if (ships.Count > 0)
                {
                    tops.Add(ships[0].Label is "on foot" or "in an SRV"
                        ? $"Most made {ships[0].Label}, {ships[0].Kills}."
                        : $"Most from {ships[0].Label}, {ships[0].Kills}.");
                }

                if (against.Count > 0)
                {
                    tops.Add($"Most against {against[0].Label}, {against[0].Kills}.");
                }

                report.AppendLine(string.Join(' ', tops));
                break;
        }

        report.Append(unfinished);

        return report.ToString().TrimEnd();
    }

    /// <summary>The groups under their displayed names, groups that share a name merged, most kills first.</summary>
    private static List<CombatEntry> CombatEntries<TKey>(
        IReadOnlyList<CombatGroup<TKey>> groups, Func<TKey, string> label) =>
        [
            .. groups
                .GroupBy(group => label(group.Key), StringComparer.Ordinal)
                .Select(merged => new CombatEntry(merged.Key, merged.Sum(group => group.Kills), merged.Sum(group => group.Awarded)))
                .OrderByDescending(entry => entry.Kills)
                .ThenByDescending(entry => entry.Awarded),
        ];

    private static void CombatList(
        StringBuilder report,
        List<CombatEntry> entries,
        string? name,
        string singular,
        string plural,
        string? heading = null,
        bool credits = false,
        bool bond = false)
    {
        var matching = name is null
            ? entries
            : entries.Where(entry => entry.Label.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matching.Count == 0)
        {
            report.AppendLine(name is null ? $"No {plural} recorded." : $"No {singular} matching {name}.");

            return;
        }

        var named = name is null ? matching.Take(CombatEntriesNamed).ToList() : matching;
        var parts = named.Select(entry => CombatEntryText(entry, credits, bond)).ToList();
        var rest = matching.Count - named.Count;

        if (rest > 0)
        {
            parts.Add($"and {rest} more {(rest == 1 ? singular : plural)}");
        }

        report.AppendLine((heading is null ? string.Empty : heading + ": ") + string.Join("; ", parts) + ".");
    }

    private static string CombatEntryText(CombatEntry entry, bool credits, bool bond)
    {
        var paid = entry.Awarded.ToString("N0", CultureInfo.InvariantCulture);
        var kills = entry.Kills.ToString("N0", CultureInfo.InvariantCulture);

        if (credits)
        {
            return $"{entry.Label}, {paid} credits";
        }

        return bond && entry.Label == ConflictZoneKills
            ? $"{kills} in conflict zones, ship type not recorded, {paid} credits"
            : $"{entry.Label}, {kills} {(entry.Kills == 1 ? "kill" : "kills")}, {paid} credits";
    }

    private static string DescribeBgsWork(
        GameStateStore gameState, ToolArguments arguments, DateTimeOffset? tick, DateTimeOffset now)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var from = BgsWork.PeriodStart(tick, now);
        var work = active.BgsWork;
        var faction = arguments.TryGetString("faction", out var factionAsked) && !string.IsNullOrWhiteSpace(factionAsked)
            ? factionAsked.Trim()
            : null;
        var system = arguments.TryGetString("system", out var systemAsked) && !string.IsNullOrWhiteSpace(systemAsked)
            ? systemAsked.Trim()
            : null;

        var pairs = work.Entries
            .Where(entry => entry.At >= from)
            .GroupBy(entry => (entry.SystemAddress, entry.Faction))
            .Where(group => faction is null || string.Equals(group.Key.Faction, faction, StringComparison.OrdinalIgnoreCase))
            .Where(group => system is null
                || string.Equals(work.NameOf(group.Key.SystemAddress), system, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(group => group.Count())
            .ToList();

        var tickTime = tick is { } at ? at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) : null;

        if (pairs.Count == 0)
        {
            return tickTime is null ? "No BGS work in the last 24 hours." : $"No BGS work since the tick at {tickTime} UTC.";
        }

        var report = new StringBuilder();

        report.AppendLine(tickTime is null
            ? "In the last 24 hours. d47 does not know when the BGS tick was; the BGS tick time setting in Galaxy lets it ask."
            : $"Since the BGS tick at {tickTime} UTC, {Ago(now - from)}:");

        var named = faction is null && system is null ? pairs.Take(BgsPairsNamed).ToList() : pairs;

        foreach (var pair in named)
        {
            var place = work.NameOf(pair.Key.SystemAddress) ?? "a system not in your recent journals";
            var kinds = BgsKinds(pair).ToList();

            if (tick is { } boundary
                && work.NameOf(pair.Key.SystemAddress) is { } systemName
                && active.Standings.InfluenceAcross(systemName, pair.Key.Faction, boundary) is { } across)
            {
                kinds.Add(InfluenceClause(across));
            }

            report.AppendLine($"{pair.Key.Faction} in {place}: {string.Join("; ", kinds)}.");
        }

        if (pairs.Count > named.Count)
        {
            var more = pairs.Count - named.Count;
            report.AppendLine($"and {more} more faction{(more == 1 ? "" : "s")}.");
        }

        return report.ToString().TrimEnd();

        static string Ago(TimeSpan age)
        {
            var minutes = (long)Math.Max(0, Math.Floor(age.TotalMinutes));

            return minutes switch
            {
                0 => "less than a minute ago",
                1 => "1 minute ago",
                < 60 => $"{minutes} minutes ago",
                < 120 => "1 hour ago",
                _ => $"{minutes / 60} hours ago",
            };
        }
    }

    private static string InfluenceClause(InfluenceAcrossResult across)
    {
        var before = (across.Before * 100).ToString("0.0", CultureInfo.InvariantCulture);
        var after = (across.After * 100).ToString("0.0", CultureInfo.InvariantCulture);
        var seen = across.LastSeen.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

        return before == after
            ? $"influence {before}%, unchanged when you were there at {seen} UTC"
            : $"influence {before}% before the tick, {after}% when you were there at {seen} UTC";
    }

    private static IEnumerable<string> BgsKinds(IEnumerable<BgsEntry> entries)
    {
        var all = entries.ToList();
        var up = all.Where(entry => entry is { Kind: BgsWorkKind.Mission, Amount: > 0 }).Sum(entry => entry.Amount);
        var down = -all.Where(entry => entry is { Kind: BgsWorkKind.Mission, Amount: < 0 }).Sum(entry => entry.Amount);
        var marks = new List<string>();

        if (up > 0)
        {
            marks.Add($"+{up}");
        }

        if (down > 0)
        {
            marks.Add($"-{down}");
        }

        if (marks.Count > 0)
        {
            yield return $"missions {string.Join(" and ", marks)}";
        }

        foreach (var (kind, label) in new[]
        {
            (BgsWorkKind.Bounty, "bounties"),
            (BgsWorkKind.CombatBond, "combat bonds"),
            (BgsWorkKind.Trade, "trade"),
            (BgsWorkKind.Cartography, "cartography"),
            (BgsWorkKind.Organic, "organic data"),
        })
        {
            var ofKind = all.Where(entry => entry.Kind == kind).ToList();

            if (ofKind.Count == 0)
            {
                continue;
            }

            var credits = ofKind.Sum(entry => entry.Amount).ToString("N0", CultureInfo.InvariantCulture);

            yield return kind == BgsWorkKind.Trade
                ? $"{label} {credits} credits sold for {ofKind.Sum(entry => entry.Profit).ToString("N0", CultureInfo.InvariantCulture)} profit"
                : $"{label} {credits} credits";
        }
    }

    private static string DescribeStanding(GameStateStore gameState, ToolArguments arguments)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var asked = arguments.TryGetString("faction", out var named) && !string.IsNullOrWhiteSpace(named)
            ? named.Trim()
            : null;

        if (asked is null)
        {
            return DescribeSuperpowers(active, Superpowers);
        }

        if (Superpower(asked) is { } superpower)
        {
            return DescribeSuperpowers(active, [superpower]);
        }

        var matches = active.Reputation.Factions
            .Where(faction => faction.Key.Contains(asked, StringComparison.OrdinalIgnoreCase))
            .OrderBy(faction => faction.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var exact = matches.FindIndex(faction => string.Equals(faction.Key, asked, StringComparison.OrdinalIgnoreCase));

        return (exact, matches.Count) switch
        {
            (>= 0, _) => DescribeFaction(matches[exact].Key, matches[exact].Value),
            (_, 0) => $"I have not seen your reputation with a faction called '{asked}'. A faction's standing "
                      + "is read when you arrive in a system where it is present.",
            (_, 1) => DescribeFaction(matches[0].Key, matches[0].Value),
            _ => $"Several factions match '{asked}': {string.Join(", ", matches.Select(faction => faction.Key))}.",
        };
    }

    /// <summary>The superpower a spoken name means, or null for a minor faction.</summary>
    private static (string Name, string? Navy)? Superpower(string asked)
    {
        var name = asked.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? asked[4..].Trim() : asked;

        var meant = name.ToUpperInvariant() switch
        {
            "EMPIRE" or "IMPERIAL" or "IMPERIAL NAVY" => "Empire",
            "FEDERATION" or "FEDERAL" or "FEDERAL NAVY" => "Federation",
            "ALLIANCE" => "Alliance",
            "INDEPENDENT" or "INDEPENDENTS" => "Independent",
            _ => null,
        };

        return meant is null ? null : Superpowers.First(superpower => superpower.Name == meant);
    }

    private static string DescribeRanks(GameStateStore gameState)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        if (!active.Ranks.IsKnown)
        {
            return "The game has not reported your ranks this session. They are written when you enter the game.";
        }

        var report = new StringBuilder().AppendLine($"Ranks{AsOf(active.Ranks.TakenAt!.Value)}:");

        foreach (var (key, label) in RankLadders)
        {
            if (active.Ranks.For(key) is not { } standing)
            {
                continue;
            }

            report.Append($"  {label}: {standing.Describe()}.");
            report.AppendLine(standing.NextName() is { } next ? $" Next rank: {next}." : " Top of the ladder.");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>Each superpower's band and number, and its navy rank where it has one.</summary>
    private static string DescribeSuperpowers(
        CommanderGameState active, IReadOnlyList<(string Name, string? Navy)> superpowers)
    {
        var readings = superpowers
            .Select(superpower => (
                superpower.Name,
                Reading: active.Reputation.Reading(superpower.Name),
                Rank: superpower.Navy is { } navy && active.Ranks.For(superpower.Name) is { } rank
                    ? $"{navy} rank: {rank.Describe()}."
                    : null))
            .ToList();

        var latest = readings.Max(reading => reading.Reading?.SeenAt);
        var report = new StringBuilder().AppendLine(
            latest is { } header
                ? $"Reputation{AsOf(header)}:"
                : "The game has not reported your superpower reputation yet. It is written when you enter the game.");

        foreach (var (name, reading, rank) in readings)
        {
            if (reading is null && rank is null)
            {
                continue;
            }

            report.Append($"  {name}: ");
            report.Append(reading is null ? "reputation not reported." : $"{Band(reading.MyReputation)}.");

            if (reading is not null && reading.SeenAt != latest)
            {
                report.Append($" Read on {reading.SeenAt:yyyy-MM-dd HH:mm} UTC.");
            }

            report.AppendLine(rank is null ? "" : $" {rank}");
        }

        return report.ToString().TrimEnd();
    }

    private static string DescribeFaction(string name, FactionReading reading) =>
        $"{name}: {Band(reading.MyReputation)}, last read on {reading.SeenAt:yyyy-MM-dd}.";

    /// <summary>"Cordial, 28 of 100".</summary>
    private static string Band(double reputation) =>
        $"{ReputationBands.Of(reputation)}, "
        + $"{Math.Round(reputation, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)} of 100";

    private static ToolResult DescribeCrimeStatus(GameStateStore gameState, OutstandingCrimes? crimes, Func<GameStatus>? liveStatus)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return ToolResult.Ok(reason);
        }

        if (crimes is null)
        {
            return ToolResult.Ok("I am not keeping a ledger of fines and bounties in this build.");
        }

        var status = liveStatus?.Invoke() ?? GameStatus.Unknown;
        var legal = status.LegalState is { Length: > 0 } named
            ? $"Legal state now: {named}{(status.OnFoot ? ", on foot" : string.Empty)}."
            : "The game has not reported your legal state.";

        var ship = crimes.FlownShip ?? active.Ship.ShipId;
        var fid = active.Identity.FrontierId;
        var debts = crimes.Owed(fid, ship);
        var others = crimes.OtherShipsOwing(fid, ship);

        var report = new StringBuilder();
        report.AppendLine(legal);

        if (!crimes.HistoryFolded)
        {
            report.AppendLine("I am still reading your older journals, so the debts below may leave some out.");
        }

        var spoken = new StringBuilder(legal);

        if (debts.Count == 0)
        {
            report.AppendLine("The journal records nothing owed on foot or on this ship.");
            spoken.Append(" The journal records nothing owed.");
        }
        else
        {
            report.AppendLine("Unpaid, as the journal records it:");

            foreach (var debt in debts)
            {
                report.AppendLine($"  {DescribeDebt(debt, withDate: true)}");
            }

            spoken.Append(" The journal records ")
                .Append(SpokenList.Names([.. debts.Select(debt => DescribeDebt(debt, withDate: false))]))
                .Append('.');
        }

        if (others > 0)
        {
            var more = $"{others} other ship{(others == 1 ? " carries" : "s carry")} unpaid debts.";
            report.AppendLine(more);
            spoken.Append(' ').Append(more);
        }

        if (active.Statistics is { IsKnown: true } statistics && statistics.Read("Crime.Notoriety") is { } notoriety)
        {
            report.AppendLine(
                $"Notoriety {notoriety.ToString("0.##", CultureInfo.InvariantCulture)}, written at login{AsOf(statistics.TakenAt!.Value)}.");
        }

        report.AppendLine("A fine the game never journaled is not in this ledger.");

        return ToolResult.Ok(report.ToString().TrimEnd(), spoken.ToString());
    }

    private static string DescribeDebt(CrimeDebt debt, bool withDate)
    {
        var amounts = new List<string>();

        if (debt.Fines > 0)
        {
            amounts.Add($"{SpokenCredits.Band(debt.Fines)} credits in fines");
        }

        if (debt.Bounties > 0)
        {
            amounts.Add($"{SpokenCredits.Band(debt.Bounties)} credits in bounties");
        }

        var where = debt.ShipId is null ? "on foot" : "on this ship";

        return $"{debt.Faction}: {string.Join(" and ", amounts)} {where}"
               + (withDate ? $", newest crime {debt.LastCrime:yyyy-MM-dd}" : string.Empty);
    }

    private static string DescribeStatistics(GameStateStore gameState, ToolArguments arguments)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var statistics = active.Statistics;

        if (!statistics.IsKnown)
        {
            return "The game has not reported your career statistics this session.";
        }

        var asOf = AsOf(statistics.TakenAt!.Value);
        var wanted = arguments.TryGetString("section", out var named) && !string.IsNullOrWhiteSpace(named)
            ? named.Trim()
            : null;

        var report = new StringBuilder();

        if (wanted is not null)
        {
            var figures = statistics.Section(wanted);

            if (figures.Count == 0)
            {
                return $"No {CareerStatistics.Label(wanted)} figures in your career statistics{asOf}.";
            }

            report.AppendLine($"{CareerStatistics.Label(wanted)}{asOf}:");
            AppendStatistics(report, figures);
            return report.ToString().TrimEnd();
        }

        report.AppendLine($"Career statistics{asOf}:");

        foreach (var section in statistics.Sections)
        {
            var figures = statistics.Section(section);

            if (figures.Count == 0)
            {
                continue;
            }

            report.AppendLine();
            report.AppendLine($"{CareerStatistics.Label(section)}:");
            AppendStatistics(report, figures);
        }

        return report.ToString().TrimEnd();
    }

    private static void AppendStatistics(StringBuilder report, IReadOnlyList<(string Key, double Value)> figures)
    {
        foreach (var (key, value) in figures)
        {
            report.AppendLine($"  {CareerStatistics.Label(key)}: {CareerStatistics.Format(key, value)}");
        }
    }

    private static string Speak(FlightMode mode) => mode switch
    {
        FlightMode.Normal => "in normal space",
        FlightMode.Supercruise => "in supercruise",
        FlightMode.Hyperspace => "in hyperspace",
        FlightMode.Docked => "docked",
        FlightMode.Landed => "landed",
        FlightMode.OnFoot => "on foot",
        _ => "in an unknown state",
    };
}
