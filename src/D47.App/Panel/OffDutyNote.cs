using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The line a page shows in place of game-live data while another Commander is shown.</summary>
public static class OffDutyNote
{
    public const string Route = "route";

    public const string Position = "position";

    /// <summary>The line for what the game is reporting, or null while the shown Commander is the one in the game.</summary>
    public static Func<string, string?> For(GameStateStore store) => what =>
    {
        if (!store.IsOffDuty)
        {
            return null;
        }

        var who = store.InGame is { } inGame ? $"CMDR {inGame.Identity.Name}" : "Another Commander";

        return $"{who} is in the game; their {what} shows when they are the Commander shown.";
    };
}
