using System.Text;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Everything d47 knows about the game from the journal (list.md Phase 2, "TheApp knows where
/// you are"; Phase 7, "Knowing the game").
/// <para>
/// Every tool here answers from <see cref="GameStateStore"/> with no model and no network. That
/// is the point of the phase: a Commander asking where their ships are should get the answer
/// from the file Elite already wrote, not from a third-party service and not from a model that
/// will invent a plausible one.
/// </para>
/// </summary>
public static class JournalCapability
{
    public const string Id = "journal";

    public static CapabilityDescriptor Create(GameStateStore gameState)
    {
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
            ],

            // Phrases, not words. "where" and "system" on their own match "Where is Iran?" and
            // "what's your operating system" — questions this capability has no business
            // answering, answered from journal data with total confidence. Every phrase here has
            // to be one that can only be a request about the Commander's own game.
            Keywords =
            [
                "where am i",
                "where i am",
                "what system",
                "which system",
                "current system",
                "my location",
                "am i docked",
                "what body",
                "what am i flying",
                "my ship",
                "my loadout",
                "jump range",
                "where is my carrier",
                "my carrier",
                "what ships do i own",
                "my ships",
                "my fleet",
                "what materials",
                "my materials",
                "my backpack",
                "ship locker",
                "session summary",
                "how have i done",
                "this session",
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
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeLocation(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_ship",
                    Description =
                        "Report the ship the Commander is flying: type, name, hull health, jump range, fuel "
                        + "and cargo capacity, and the modules fitted, from the last Loadout event.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeShip(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_fleet",
                    Description =
                        "List the ships the Commander owns and which system each one is stored in, plus their "
                        + "fleet carrier and its location if they have one.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeFleet(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_materials",
                    Description =
                        "Report the Commander's material holdings — raw, manufactured and encoded — and their "
                        + "on-foot backpack and ship locker contents.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeMaterials(gameState))),
                },
                new ToolDefinition
                {
                    Name = "get_session_summary",
                    Description =
                        "Report what the Commander has done since entering the game: credits earned by source, "
                        + "jumps made, distance travelled, materials gained and bodies scanned.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeSession(gameState))),
                },
            ],
        };
    }

    /// <summary>
    /// The one answer every other one needs first. Stated once here so five tools cannot each
    /// invent their own wording for "Elite has not been running".
    /// </summary>
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

    private static string DescribeLocation(GameStateStore gameState)
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

        if (location.NextJumpSystem is { } next)
        {
            report.Append($" Next jump: {next}");
            report.Append(location.NextJumpStarClass is { } starClass ? $" (class {starClass}).": ".");

            if (location.JumpsRemaining is { } remaining and > 0)
            {
                report.Append($" {remaining} jump{(remaining == 1 ? "" : "s")} left on the route.");
            }
        }

        return report.ToString();
    }

    private static string DescribeShip(GameStateStore gameState)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var ship = active.Ship;

        if (!ship.IsKnown)
        {
            // Specific about why. Loadout fires on entering the game and on every outfitting
            // change, so "not seen yet" almost always means d47 started mid-session — which is
            // a different problem from the ship being unknowable, and has a different fix.
            return "No Loadout event has been seen yet, so I do not know what you are flying. "
                   + "It is written when you enter the game or change your outfitting.";
        }

        var report = new StringBuilder($"Flying {ship.Describe()}");

        if (ship.Ident is { } ident)
        {
            report.Append($", ident {ident}");
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
            metrics.Add($"cargo capacity {cargo} t");
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
                // Worth stating unprompted: an unpowered module is one the Commander believes
                // they have.
                report.AppendLine(
                    "Unpowered: " + string.Join(", ", ship.Unpowered.Select(module => module.Item)) + ".");
            }
        }

        return report.ToString().TrimEnd();
    }

    private static string DescribeFleet(GameStateStore gameState)
    {
        if (!TryActive(gameState, out var active, out var reason))
        {
            return reason;
        }

        var report = new StringBuilder();
        var carrier = active.Carrier;

        if (carrier.Owned)
        {
            report.Append("Fleet carrier ");
            report.Append(carrier.Name is { } name ? $"{name} ({carrier.CallSign})" : carrier.CallSign);
            report.Append(carrier.StarSystem is { } system ? $" is in {system}" : " location unknown");

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
        else
        {
            // Precise about the limit of the evidence. Carrier events only reach an owner's
            // journal, so silence means "none seen this session", which is not the same claim
            // as "you do not own one".
            report.AppendLine("No fleet carrier events have been seen this session.");
        }

        var fleet = active.Fleet;

        if (!fleet.IsKnown)
        {
            report.AppendLine(
                "I have no ship list yet — it is written when you dock at a station with a shipyard.");
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
                report.AppendLine($"{fleet.Ships.Count} other ship{(fleet.Ships.Count == 1 ? "" : "s")} stored:");

                foreach (var group in fleet.Ships
                             .GroupBy(ship => ship.StarSystem, StringComparer.OrdinalIgnoreCase)
                             .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var ships = string.Join(", ", group.Select(ship => ship.Describe()));
                    report.AppendLine($"  {group.Key}: {ships}");
                }

                var inTransit = fleet.Ships.Where(ship => ship.InTransit).ToArray();

                if (inTransit.Length > 0)
                {
                    report.AppendLine(
                        "In transit: " + string.Join(", ", inTransit.Select(ship => ship.Describe())) + ".");
                }
            }
        }

        return report.ToString().TrimEnd();
    }

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

                // The top few, not all of them. A full list is 130 lines, which is not an
                // answer to a spoken question and is a great deal of context to spend.
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
