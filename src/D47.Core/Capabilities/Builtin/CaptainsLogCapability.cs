using D47.Core.CaptainsLog;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Spoken log entries, kept as heard with the place and ship they were said in.</summary>
public static class CaptainsLogCapability
{
    public const string Id = "captains-log";

    public const string AddTool = "add_log_entry";

    /// <summary>What the exchange is recorded as, in place of the entry's words.</summary>
    public const string Asked = "captain's log entry";

    public const string NoCommander = "Nobody is flying, so there is nobody to log for.";

    public static CapabilityDescriptor Create(
        CaptainsLogStore? store,
        Func<string> frontierId,
        Func<CommanderGameState?> commander,
        Func<GameStatus>? status,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Conversation",
        Name = "Captain's log",
        Summary = "Say \"captain's log\" and an entry, and it is kept word for word with where you were.",
        Examples = ["captain's log, the Guardian site is two kilometres north of the landing pad", "log entry: fuel is low"],
        Display = new CapabilityDisplay { PanelTitle = "Captain's log", Order = 45, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = AddTool,
                Description =
                    "Save a log entry in the Commander's own words, with the system, body, station, ship and "
                    + "position known now. The Commander's own act: reached only by saying \"captain's log\", "
                    + "not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "text",
                        Type = ToolParameterType.String,
                        Description = "The entry, as the Commander said it.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(Add(store, frontierId(), commander(), status?.Invoke(), now(), arguments)),
            },
        ],
    };

    private static ToolResult Add(
        CaptainsLogStore? store,
        string frontierId,
        CommanderGameState? commander,
        GameStatus? status,
        DateTimeOffset now,
        ToolArguments arguments)
    {
        if (store is null)
        {
            return ToolResult.Error("Nothing here keeps a captain's log.");
        }

        if (string.IsNullOrWhiteSpace(frontierId))
        {
            return ToolResult.Error(NoCommander);
        }

        if (!arguments.TryGetString("text", out var text) || string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Error(CaptainsLogPhrase.NoEntry);
        }

        var location = commander?.Location;
        var ship = commander?.FlownShip;

        var entry = new CaptainsLogEntry(Guid.NewGuid().ToString("N"), text.Trim(), now.ToUniversalTime())
        {
            System = Known(location?.StarSystem),
            Body = Known(location?.Body) ?? Known(status?.BodyName),
            Station = location is { Docked: true } ? Known(location.StationName) : null,
            ShipType = Known(ship?.TypeName) ?? Known(ship?.Type),
            ShipName = Known(ship?.Name),
            Latitude = status is { HasPosition: true } ? status.Latitude : null,
            Longitude = status is { HasPosition: true } ? status.Longitude : null,
        };

        return store.Add(frontierId, entry)
            ? ToolResult.Ok("Logged.")
            : ToolResult.Error("I couldn't keep that entry. The log file may need fixing by hand.");
    }

    private static string? Known(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
