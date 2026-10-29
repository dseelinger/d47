using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Says what Elite's servers are doing, from the watch's last answer.</summary>
public static class ServerStatusCapability
{
    public const string Id = "server-status";

    public static CapabilityDescriptor Create(Func<ServerStatusWatch?> watch, Func<bool> enabled, Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Server status",
        Display = new CapabilityDisplay { PanelTitle = "Server status", Order = 69, ShowOnPanel = false },
        Summary = "Say whether Elite's servers are up, from the last check.",
        Examples = ["are the servers up", "is elite down"],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_server_status",
                Exercise =
                    "Say \"are the servers up\". The reply says whether the game servers are online, in "
                    + "maintenance, offline or unknown and how long ago that was checked; with the Server "
                    + "status setting off, it says so.",
                Description =
                    "Say whether Elite's game servers are online, in maintenance, offline or unknown, and how long ago "
                    + "that was checked. Makes no request; says so when the Server status setting is off.",
                Parameters = [],
                Commands =
                [
                    new ToolCommandPhrase("are the servers up"),
                    new ToolCommandPhrase("is elite down"),
                    new ToolCommandPhrase("server status"),
                ],
                Handler = (_, _) => Task.FromResult(Answer(watch(), enabled(), now())),
            },
        ],
    };

    private static ToolResult Answer(ServerStatusWatch? watch, bool enabled, DateTimeOffset now)
    {
        if (!enabled)
        {
            return ToolResult.Ok("Server status is off. Turn it on under Outside sources in Settings to check the servers.");
        }

        if (watch?.Shown is not { } state)
        {
            return ToolResult.Ok("Server status has not been checked yet.");
        }

        var word = state switch
        {
            GameServerState.Online => "Online",
            GameServerState.Maintenance => "In maintenance",
            GameServerState.Offline => "Offline",
            _ => "Unknown",
        };

        return ToolResult.Ok(watch.ShownAt is { } at ? $"{word}. Checked {Age(now - at)}." : $"{word}.");
    }

    private static string Age(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "less than a minute ago";
        }

        var minutes = (int)age.TotalMinutes;
        return minutes == 1 ? "a minute ago" : $"{minutes} minutes ago";
    }
}
