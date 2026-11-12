using System.Text;
using D47.Core.Watches;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The background watches the Commander set up, listed with their condition and when each last fired.</summary>
public static class WatchesCapability
{
    public const string Id = "watches";

    public const string Tool = "list_watches";

    public static CapabilityDescriptor Create(Func<IReadOnlyList<Watch>> watches, Func<TimeZoneInfo>? zone = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Watches",
        Display = new CapabilityDisplay { PanelTitle = "Watches", Order = 47, ShowOnPanel = false },
        Summary = "List the background watches you set up, their conditions, and when each last fired.",
        Examples = ["list my watches", "what am I watching", "what watches do I have"],
        Tools =
        [
            new ToolDefinition
            {
                Name = Tool,
                Exercise =
                    "Say \"list my watches\". The reply names each watch with its condition and when it last fired, "
                    + "or says there are none.",
                Description =
                    "List the Commander's background watches: each one's name, its condition, whether it is on, "
                    + "and the BGS tick it last fired for. Watches are set up on the Watches page only.",
                Parameters = [],
                Commands =
                [
                    new ToolCommandPhrase("list my watches"),
                    new ToolCommandPhrase("what am i watching"),
                    new ToolCommandPhrase("what watches do i have"),
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(watches(), zone?.Invoke() ?? TimeZoneInfo.Local))),
            },
        ],
    };

    /// <summary>One line per watch.</summary>
    public static string Describe(IReadOnlyList<Watch> watches, TimeZoneInfo zone)
    {
        if (watches.Count == 0)
        {
            return "You have no watches. Add one under Commander, Watches.";
        }

        var text = new StringBuilder();
        text.Append(watches.Count).Append(watches.Count == 1 ? " watch." : " watches.");

        foreach (var watch in watches)
        {
            text.Append('\n').Append(watch.Name).Append(": ").Append(Condition(watch));

            if (!watch.Enabled)
            {
                text.Append(", off");
            }

            text.Append(". ").Append(WatchFired.Of(watch, zone)).Append('.');
        }

        return text.ToString();
    }

    /// <summary>
    /// "Soontill Relics above 200 at Cheranovsky City, Ngurii", "a new Community Goal is announced" or
    /// "a station with a Manufactured material trader, any pad, in the next system".
    /// </summary>
    public static string Condition(Watch watch) => watch.Kind switch
    {
        WatchKind.NewCommunityGoal => "a new Community Goal is announced",
        WatchKind.NextSystem => NextSystemWatch.Condition(watch),
        _ => $"{watch.Commodity} {(watch.Comparison == StockComparison.Below ? "below" : "above")} {watch.Threshold} "
            + $"at {watch.Station}, {watch.System}",
    };
}
