using System.Globalization;
using System.Text;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The live mission board, read aloud in the order a Commander can act on it.</summary>
public static class MissionsCapability
{
    public const string Id = "missions";

    public const string Tool = "get_mission_board";

    private const int MaxNamed = 3;

    private static readonly TimeSpan SoonWindow = TimeSpan.FromHours(1);

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> Offered =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["offered"] = "true" };

    public static CapabilityDescriptor Create(Func<CommanderGameState?> state, Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Ship",
        Name = "Missions",
        Summary = "Read the accepted missions aloud: the most urgent three, the rest as a count, and the total reward.",
        Examples = ["mission board", "what missions do I have", "what am I hauling"],
        Display = new CapabilityDisplay { PanelTitle = "Missions", Order = 28, ShowOnPanel = false },
        Keywords =
        [
            "mission board",
            "what missions do i have",
            "read my missions",
            "what am i hauling",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = Tool,
                Description =
                    "Read the Commander's accepted missions: up to three, ranked by what can be acted on "
                    + "(expiring within the hour, then handed in at the docked station, then soonest expiry), "
                    + "each with its destination and time left, then a count of the rest and the total reward.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "offered",
                        Type = ToolParameterType.Boolean,
                        Description =
                            "Set when the Commander asks which missions to take; the answer then says that "
                            + "missions on offer at a station are not in the journal.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("what missions should i take", Offered),
                    new ToolCommandPhrase("which missions should i take", Offered),
                    new ToolCommandPhrase("what missions are on offer", Offered),
                    new ToolCommandPhrase("read the mission board", Nothing),
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(
                    Describe(state(), now(), arguments.TryGetBoolean("offered", out var offered) && offered))),
            },
        ],
    };

    public static string Describe(CommanderGameState? state, DateTimeOffset now, bool offered = false)
    {
        var missions = state?.Missions.Missions ?? [];
        var clock = now == DateTimeOffset.MinValue ? (DateTimeOffset?)null : now;
        var said = new StringBuilder();

        if (offered)
        {
            said.Append("I only see missions after you accept them. ");
        }

        if (missions.Count == 0)
        {
            return said.Append("There are no missions on your board.").ToString();
        }

        var ranked = Rank(missions, clock, state!.Location);

        said.Append(missions.Count == 1 ? "You have one mission. " : $"You have {Number(missions.Count)} missions. ");

        for (var index = 0; index < Math.Min(MaxNamed, ranked.Count); index++)
        {
            said.Append($"{Ordinal(index)}, {Name(ranked[index], clock)}. ");
        }

        if (ranked.Count > MaxNamed)
        {
            said.Append($"And {Number(ranked.Count - MaxNamed)} more. ");
        }

        var rewards = missions.Where(mission => mission.Reward is not null).ToList();

        if (rewards.Count > 0)
        {
            var total = rewards.Sum(mission => mission.Reward.GetValueOrDefault()).ToString("N0", CultureInfo.InvariantCulture);
            said.Append(rewards.Count < missions.Count
                ? $"At least {total} credits in rewards."
                : $"{total} credits in rewards.");
        }

        return said.ToString().TrimEnd();
    }

    /// <summary>Expiring within the hour, then handed in where the Commander is docked, then the rest; soonest expiry first in each.</summary>
    private static List<Mission> Rank(IReadOnlyList<Mission> missions, DateTimeOffset? now, JournalLocation location)
    {
        bool Soon(Mission mission) =>
            now is { } clock && mission.Expiry is { } expiry && expiry > clock && expiry - clock <= SoonWindow;

        bool Here(Mission mission) =>
            location.Docked
            && location.StationName is { Length: > 0 } station
            && string.Equals(mission.DestinationStation, station, StringComparison.OrdinalIgnoreCase);

        bool Expired(Mission mission) => now is { } clock && mission.Expiry <= clock;

        int Band(Mission mission) => Soon(mission) ? 0 : Here(mission) ? 1 : 2;

        return
        [
            .. missions
                .OrderBy(Band)
                .ThenBy(mission => Band(mission) == 2 && Expired(mission))
                .ThenBy(mission => mission.Expiry ?? DateTimeOffset.MaxValue)
                .ThenBy(mission => mission.Id),
        ];
    }

    private static string Name(Mission mission, DateTimeOffset? now)
    {
        if (!mission.HasDetail)
        {
            return mission.Title;
        }

        var said = new StringBuilder(mission.Title);

        if (mission.Destination is { } destination)
        {
            said.Append($", to {destination}");
        }

        if (now is { } clock && mission.Expiry is { } expiry)
        {
            said.Append($", {TimeLeft(expiry - clock)}");
        }

        return said.ToString();
    }

    private static string TimeLeft(TimeSpan left)
    {
        if (left <= TimeSpan.Zero)
        {
            return "expired";
        }

        var hours = (int)left.TotalHours;
        var minutes = Math.Max(1, left.Minutes);

        return hours switch
        {
            0 => $"{minutes} {Plural(minutes, "minute")} left",
            _ when left.Minutes == 0 => $"{hours} {Plural(hours, "hour")} left",
            _ => $"{hours} {Plural(hours, "hour")} {minutes} {Plural(minutes, "minute")} left",
        };
    }

    private static string Plural(int count, string unit) => count == 1 ? unit : unit + "s";

    private static string Ordinal(int index) => index switch
    {
        0 => "First",
        1 => "Second",
        _ => "Third",
    };

    private static string Number(int count) => count switch
    {
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        8 => "eight",
        9 => "nine",
        10 => "ten",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };
}
