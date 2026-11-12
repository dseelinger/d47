using System.Text;
using D47.Core.Watches;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The background watches the Commander set up, listed with their condition and when each last fired.</summary>
public static class WatchesCapability
{
    public const string Id = "watches";

    public const string Tool = "list_watches";

    public const string AddTool = "add_next_system_watch";

    public const string DeleteTool = "delete_next_system_watch";

    private const string NoStore = "Nothing here keeps watches.";

    private const string KindHelp =
        "Raw, Manufactured or Encoded for a material trader; Human or Guardian for a technology broker; left out for any.";

    private static readonly string[] VoiceServices =
    [
        "Black Market",
        "Interstellar Factors",
        NextSystemWatch.MaterialTrader,
        "Outfitting",
        "Refuel",
        "Repair",
        "Shipyard",
        NextSystemWatch.TechnologyBroker,
        "Universal Cartographics",
        "Vista Genomics",
    ];

    private static readonly string[] VoiceKinds = [.. Knowledge.StationQuery.TraderTypes, .. Knowledge.StationQuery.BrokerTypes];

    /// <summary>The arguments <see cref="AddTool"/> or <see cref="DeleteTool"/> takes for one reading of the Commander's words.</summary>
    public static ToolArguments ArgumentsFor(WatchReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (reading)
        {
            case WatchReading.Add add:
                values["service"] = add.Service;
                Put(values, "kind", add.Kind);
                Put(values, "pad", add.Pad?.ToString());
                break;
            case WatchReading.Delete delete:
                values["service"] = delete.Service;
                Put(values, "kind", delete.Kind);
                break;
        }

        return new ToolArguments(values);
    }

    private static void Put(Dictionary<string, string> values, string name, string? value)
    {
        if (value is not null)
        {
            values[name] = value;
        }
    }

    public static CapabilityDescriptor Create(
        Func<IReadOnlyList<Watch>> watches,
        Func<TimeZoneInfo>? zone = null,
        WatchVoice? voice = null) => new()
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
                    + "and the BGS tick it last fired for. Stock and Community Goal watches are set up on the Watches page.",
                Parameters = [],
                Commands =
                [
                    new ToolCommandPhrase("list my watches"),
                    new ToolCommandPhrase("what am i watching"),
                    new ToolCommandPhrase("what watches do i have"),
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(watches(), zone?.Invoke() ?? TimeZoneInfo.Local))),
            },

            new ToolDefinition
            {
                Name = AddTool,
                Exercise =
                    "Say \"tell me the next time we're in a system with a material trader\". D47 says it is watching, "
                    + "and speaks when you arrive in such a system. The model cannot add one: it is refused if it asks.",
                Description =
                    "Add a next-system watch for a station service. The Commander's own act: reached only by saying "
                    + "\"tell me the next time we're in a system with\" a service, not offered to the model, and "
                    + "refused if it asks.",
                RefusalExample = "tell me the next time we're in a system with a material trader",
                Protected = true,
                Parameters =
                [
                    ToolParameter.OneOf("service", "The station service to watch for.", VoiceServices, required: true),
                    ToolParameter.OneOf("kind", KindHelp, VoiceKinds),
                    ToolParameter.OneOf("pad", "The smallest pad the station must have; left out for any.", Knowledge.StationQuery.MinPads),
                ],
                Handler = (arguments, cancellationToken) => voice is null
                    ? Task.FromResult(ToolResult.Error(NoStore))
                    : voice.AddAsync(arguments.Text("service"), arguments.Text("kind"), arguments.Text("pad"), cancellationToken),
            },

            new ToolDefinition
            {
                Name = DeleteTool,
                Exercise =
                    "With a next-system watch set, say \"stop watching for a material trader\". The reply says the "
                    + "watch is deleted. The model cannot do this: it is refused if it asks.",
                Description =
                    "Delete a next-system watch by its service and kind. The Commander's own act: reached only by "
                    + "saying \"stop watching for\" a service, not offered to the model, and refused if it asks.",
                RefusalExample = "stop watching for a material trader",
                Protected = true,
                Parameters =
                [
                    ToolParameter.OneOf("service", "The station service of the watch to delete.", VoiceServices, required: true),
                    ToolParameter.OneOf("kind", KindHelp, VoiceKinds),
                ],
                Handler = (arguments, _) => Task.FromResult(
                    voice is null
                        ? ToolResult.Error(NoStore)
                        : voice.Delete(arguments.Text("service"), arguments.Text("kind"))),
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
