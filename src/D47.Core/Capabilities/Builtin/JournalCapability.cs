using System.Globalization;
using System.Text;
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

    /// <param name="history">
    /// How far the walk over older journals has got, so a fleet question says that rather than reporting
    /// an absence the walk has not ruled out (#148).
    /// </param>
    /// <param name="route">The plotted route, which is where a jump count comes from (#152).</param>
    public static CapabilityDescriptor Create(
        GameStateStore gameState,
        Func<HistoryState>? history = null,
        Func<NavRoute>? route = null)
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
                new("my jump range", "get_ship"),

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
                new("my career statistics", "get_commander_statistics"),
                new("my career stats", "get_commander_statistics"),
                new("commander statistics", "get_commander_statistics"),
                .. SuperpowerKeywords(),
                new("my navy rank", "get_standing"),
                new("my naval rank", "get_standing"),
                new("my navy ranks", "get_standing"),
            ],
            Display = new CapabilityDisplay { PanelTitle = "Location", Order = 20 },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "get_location",
                    Description =
                        "Report the current Commander's star system, body, docking state and what they are "
                        + "doing — supercruise, hyperspace, landed, on foot — from the journal.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeLocation(gameState, plotted()))),
                },
                new ToolDefinition
                {
                    Name = "get_ship",
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
                    ],
                    Commands = Asking(Flying),
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(DescribeShip(gameState, arguments))),
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
                        ToolResult.Ok(DescribeStoredModules(gameState, arguments))),
                },
                new ToolDefinition
                {
                    Name = "get_materials",
                    Description =
                        "Report the Commander's material holdings — raw, manufactured and encoded — and their "
                        + "on-foot backpack and ship locker contents.",
                    Commands = Asking(Held),
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeMaterials(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_session_summary",
                    Description =
                        "Report what the Commander has done since entering the game: credits earned by source, "
                        + "jumps made, distance travelled, materials gained and bodies scanned.",
                    Commands = Asking(Done),
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeSession(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_commander_statistics",
                    Description =
                        "Report the Commander's career statistics from the journal's Statistics event — bank "
                        + "balance, combat, crime, smuggling, trading, mining, exploration, passengers, search "
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
                    Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(DescribeStatistics(gameState, arguments))),
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
            ],
        };
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
        "what is my jump range", "what's my jump range", "my jump range",
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

    private static string DescribeShip(GameStateStore gameState, ToolArguments arguments)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var named = arguments.TryGetString("ship", out var spoken) && !string.IsNullOrWhiteSpace(spoken)
            ? spoken.Trim()
            : null;

        if (named is null)
        {
            // The flown ship where the journal has described it this session, and the one last remembered
            // for it otherwise (#337) — so a restart before Elite writes a fresh Loadout still answers,
            // rather than saying the Commander's own ship is unknown.
            var flown = active.FlownShip;

            if (!flown.IsKnown)
            {
                // Specific about why.
                return "No Loadout event has been seen yet, so I do not know what you are flying. "
                       + "It is written when you enter the game or change your outfitting.";
            }

            var seenAt = active.Ship.IsKnown ? null : active.Loadouts.For(flown.ShipId)?.SeenAt;
            return Report(active, flown, isFlown: true, seenAt);
        }

        if (Remembered(active, named) is not { } found)
        {
            return UnknownShip(active, named);
        }

        var isCurrent = active.Ship.IsKnown && active.Ship.ShipId == found.Loadout.ShipId;
        return Report(active, found.Loadout, isFlown: isCurrent, isCurrent ? null : found.SeenAt);
    }

    /// <summary>One ship's report: metrics and module counts, from the live loadout or a remembered
    /// one — dated, and with no claim about the current cargo fill, when it is not (#108).</summary>
    private static string Report(CommanderGameState active, ShipLoadout ship, bool isFlown, DateTimeOffset? seenAt)
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

        if (ship.Modules.Count > 0)
        {
            report.AppendLine($"{ship.Modules.Count} modules fitted, {ship.Engineered.Count} engineered.");

            if (ship.Unpowered.Count > 0)
            {
                // Worth stating unprompted: an unpowered module is one the Commander believes they have.
                report.AppendLine(
                    "Unpowered: " + string.Join(", ", ship.Unpowered.Select(module => module.Item)) + ".");
            }
        }

        return report.ToString().TrimEnd();
    }

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
                ? owned.FirstOrDefault(ship => string.Equals(ship.Type, symbol, StringComparison.OrdinalIgnoreCase))
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

        var wanted = remembered;

        if (arguments.TryGetInt32("min_cargo", out var minimum))
        {
            wanted = [.. remembered.Where(ship => ship.Value.Loadout.CargoCapacity >= minimum)];
        }

        if (wanted.Count == 0)
        {
            var largest = remembered
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

            report.AppendLine(AsOf(ship.Value.SeenAt));
        }

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
    private static string DescribeStoredModules(GameStateStore gameState, ToolArguments arguments)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var store = active.Modules;

        if (!store.IsKnown)
        {
            // The same shape of answer the fleet gives, and true for the same reason: the event is written on
            // docking somewhere with outfitting, so silence before that is missing evidence rather than an
            // empty store.
            return "I have no module storage list yet — it is written when you dock at a station with outfitting.";
        }

        var wanted = arguments.TryGetString("module", out var fragment) && !string.IsNullOrWhiteSpace(fragment)
            ? fragment.Trim()
            : null;

        var modules = wanted is null ? store.Modules : store.Matching(wanted);

        if (modules.Count == 0)
        {
            return wanted is null
                ? "No modules in storage."
                : $"Nothing in storage matches '{wanted}'. {store.Modules.Count} module"
                  + $"{(store.Modules.Count == 1 ? " is" : "s are")} stored in total.";
        }

        var report = new StringBuilder();

        report.Append(wanted is null
            ? $"{modules.Count} module{(modules.Count == 1 ? "" : "s")} in storage"
            : $"{modules.Count} stored module{(modules.Count == 1 ? "" : "s")} match '{wanted}'");

        report.AppendLine(store.TakenAt is { } taken ? AsOf(taken) + "." : ".");

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

        return report.ToString().TrimEnd();
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
                return $"No {ReadableStatistic(wanted)} figures in your career statistics{asOf}.";
            }

            report.AppendLine($"{ReadableStatistic(wanted)}{asOf}:");
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
            report.AppendLine($"{ReadableStatistic(section)}:");
            AppendStatistics(report, figures);
        }

        return report.ToString().TrimEnd();
    }

    private static void AppendStatistics(StringBuilder report, IReadOnlyList<(string Key, double Value)> figures)
    {
        foreach (var (key, value) in figures)
        {
            report.AppendLine($"  {ReadableStatistic(key)}: {FormatStatistic(key, value)}");
        }
    }

    private static readonly IReadOnlyDictionary<string, string> ReadableStatisticNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FLEETCARRIER"] = "Fleet carrier",
        };

    /// <summary>
    /// A section or figure name in words — the table above where a key reads badly split on its
    /// underscores, the split itself otherwise, so a key Frontier adds later is still reported (#263).
    /// </summary>
    private static string ReadableStatistic(string key) =>
        ReadableStatisticNames.TryGetValue(key, out var name)
            ? name
            : string.Join(
                ' ',
                key.Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));

    /// <summary>
    /// A figure in its unit, guessed from what the key names: credits for a profit, a spend or a
    /// wealth figure; light years for a distance; hours and minutes for a time (#263).
    /// </summary>
    private static string FormatStatistic(string key, double value)
    {
        if (key.Contains("Distance", StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:0.##} ly";
        }

        if (key.Contains("Time", StringComparison.OrdinalIgnoreCase))
        {
            return FormatStatisticDuration(value);
        }

        if (key.Contains("Profit", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Wealth", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Debt", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Spent", StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:N0} cr";
        }

        return value == Math.Floor(value) ? value.ToString("N0") : value.ToString("0.##");
    }

    private static string FormatStatisticDuration(double totalSeconds)
    {
        var span = TimeSpan.FromSeconds(totalSeconds);
        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;

        return hours > 0
            ? $"{hours}h {minutes}m"
            : $"{minutes} minute{(minutes == 1 ? "" : "s")}";
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
