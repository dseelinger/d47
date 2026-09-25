using System.Text;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The carrier's own figures — fuel, cargo, balance, jump range, services (#184).</summary>
public static class CarrierCapability
{
    public const string Id = "carrier";

    public static CapabilityDescriptor Create(Func<CommanderGameState?> state, Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Ship",
        Name = "Carrier",
        Summary = "Report the fleet carrier's fuel, cargo, balance, jump range, docking access and services.",
        Examples = ["carrier report", "carrier status", "how is my carrier"],
        Keywords =
        [
            "carrier report",
            "carrier status",
            "how is my carrier",
            "carrier services",
            "carrier fuel",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Carrier", Order = 27, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = "describe_carrier",
                Description =
                    "Report the Commander's fleet carrier: the system it is in, a booked jump, fuel, cargo "
                    + "against capacity, balance, jump range, docking access, the decommission flag and every "
                    + "service with its state and crew.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(state(), now()))),
            },
        ],
    };

    public static string Describe(CommanderGameState? state, DateTimeOffset now)
    {
        var carrier = state?.Carrier ?? CarrierState.None;

        if (!carrier.IsKnown)
        {
            // Not "you have no carrier".
            return "I have not seen you own a fleet carrier this session. If you have, I would not know "
                   + "about its figures until the journal next reports them.";
        }

        var report = new StringBuilder();

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

            if (carrier.SeenAt is { } seen)
            {
                report.Append($", as of {seen:yyyy-MM-dd HH:mm} UTC");
            }
        }
        else
        {
            report.Append(" location unknown");
        }

        if (carrier.DestinationSystem is { } destination)
        {
            report.Append($", jumping to {destination}");

            if (carrier.DestinationBody is { Length: > 0 } body)
            {
                report.Append($" ({body})");
            }

            if (carrier.DepartureTime is { } departure)
            {
                report.Append($" at {departure:HH:mm} UTC");
            }
        }

        report.AppendLine(".");

        if (carrier.FuelLevel is { } fuel)
        {
            report.Append($"Fuel {fuel} t");
            AppendStatsAge(report, carrier);
            report.AppendLine(".");
        }

        if (carrier.Capacity is { } capacity)
        {
            report.Append($"Cargo {carrier.CargoTonnes ?? 0}/{capacity} t");

            if (carrier.HowFull is { } howFull)
            {
                report.Append($" ({howFull:P0} full)");
            }

            AppendStatsAge(report, carrier);
            report.AppendLine(".");
        }
        else if (carrier.CargoTonnes is { } cargo)
        {
            report.Append($"Cargo {cargo} t");
            AppendStatsAge(report, carrier);
            report.AppendLine(".");
        }

        if (CarrierUpkeep.Now(carrier, now) is { } balance)
        {
            report.AppendLine(balance.Adjusted
                ? $"Balance about {balance.Balance:N0} cr ({balance.Explained})."
                : $"Balance {balance.Balance:N0} cr.");

            if (balance.Weekly is { } weekly)
            {
                report.AppendLine(
                    $"Upkeep {weekly:N0} cr a week, which the balance covers for {balance.WeeksCovered} weeks.");
            }
        }
        else if (carrier.Balance is { } recorded)
        {
            report.AppendLine($"Balance {recorded:N0} cr.");
        }

        if (carrier.JumpRange is { } range)
        {
            report.AppendLine($"Jump range {range:0.##} ly.");
        }

        if (carrier.DockingAccess is { Length: > 0 } access)
        {
            report.AppendLine($"Docking access: {access}.");
        }

        if (carrier.PendingDecommission)
        {
            report.AppendLine("Booked for decommissioning.");
        }

        if (carrier.Services.Count > 0)
        {
            report.AppendLine("Services:");

            foreach (var service in carrier.Services)
            {
                var open = !service.Activated ? "not bought" : service.Enabled ? "open" : "closed";
                var staffedBy = service.Name is { Length: > 0 } crew ? $", staffed by {crew}" : string.Empty;

                report.AppendLine($"  {service.Role}: {open}{staffedBy}");
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>How old the <c>CarrierStats</c> figures are, since fuel and cargo are only as fresh as the
    /// last one read.</summary>
    private static void AppendStatsAge(StringBuilder report, CarrierState carrier)
    {
        if (carrier.StatsSeenAt is { } seen)
        {
            report.Append($", as of {seen:yyyy-MM-dd HH:mm} UTC");
        }
    }
}
