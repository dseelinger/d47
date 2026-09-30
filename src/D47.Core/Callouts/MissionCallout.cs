using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Missions that hand in at the station docked at or left, and a mission about to expire (#662).</summary>
public sealed class MissionCallout : ICallout
{
    public string Id => "missions";

    public const string HandInKey = "missions.hand-in";

    public const string UnclaimedKey = "missions.unclaimed";

    public const string ExpiryKey = "missions.expiry";

    /// <summary>The warnings, furthest first.</summary>
    private static readonly (string Name, TimeSpan Before, string Said)[] Warnings =
    [
        ("hour", TimeSpan.FromHours(1), "an hour"),
        ("ten", TimeSpan.FromMinutes(10), "ten minutes"),
    ];

    /// <summary>How far past its mark a warning can be and still be said.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    private readonly HashSet<(long Id, string Warning)> _warned = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var board = context.State?.Missions ?? MissionBoard.Empty;

        if (context.IsPriming)
        {
            Prime(board, context.Now);
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            var station = journalEvent.String("StationName");

            switch (journalEvent.Kind)
            {
                case "Docked" when HandIns(board, station) is { Count: > 0 } here:
                    yield return new Announcement(HandInKey, HandInSentence(here));
                    break;

                case "Undocked" when HandIns(board, station).Count > 0:
                    yield return new Announcement(UnclaimedKey, $"You're leaving with a hand-in unclaimed at {station}.");
                    break;
            }
        }

        _warned.RemoveWhere(warned => board.For(warned.Id) is null);

        foreach (var mission in board.BySoonest())
        {
            if (Warn(mission, context.Now) is { } warning)
            {
                yield return new Announcement($"{ExpiryKey}.{mission.Id}.{warning.Name}", $"{mission.Title} expires in {warning.Said}.");
            }
        }
    }

    /// <summary>Marks every warning already behind a mission as said, so a restart does not repeat it.</summary>
    private void Prime(MissionBoard board, DateTimeOffset now)
    {
        _warned.Clear();

        foreach (var mission in board.Missions)
        {
            if (mission.Expiry is not { } expiry)
            {
                continue;
            }

            var left = expiry - now;

            foreach (var (name, before, _) in Warnings)
            {
                // The hour warning is not repeated anywhere inside the hour; the ten-minute one is said
                // again only if it fell due within the last minute.
                if (name == "hour" ? left <= before : left < before - Grace)
                {
                    _warned.Add((mission.Id, name));
                }
            }
        }
    }

    /// <summary>The nearest fresh warning now due for the mission; every due one is marked as said.</summary>
    private (string Name, string Said)? Warn(Mission mission, DateTimeOffset now)
    {
        if (mission.Expiry is not { } expiry)
        {
            return null;
        }

        var left = expiry - now;
        (string, string)? nearest = null;

        foreach (var (name, before, said) in Warnings)
        {
            if (left > before || !_warned.Add((mission.Id, name)))
            {
                continue;
            }

            if (left >= before - Grace)
            {
                nearest = (name, said);
            }
        }

        return nearest;
    }

    private static IReadOnlyList<Mission> HandIns(MissionBoard board, string? station) =>
        station is { Length: > 0 }
            ? [.. board.Missions.Where(mission => string.Equals(mission.DestinationStation, station, StringComparison.OrdinalIgnoreCase))]
            : [];

    private static string HandInSentence(IReadOnlyList<Mission> missions)
    {
        var count = missions.Count == 1 ? "One mission concludes" : $"{Words(missions.Count)} missions conclude";
        var reward = missions.Sum(mission => mission.Reward ?? 0);

        return reward > 0
            ? $"{count} here. {Credits(reward)} waiting."
            : $"{count} here.";
    }

    private static string Credits(long amount) =>
        amount < 1_000_000 ? $"{SpokenCredits.Band(amount)} credits" : SpokenCredits.Band(amount);

    private static string Words(int count) => count switch
    {
        2 => "Two",
        3 => "Three",
        4 => "Four",
        5 => "Five",
        6 => "Six",
        7 => "Seven",
        8 => "Eight",
        9 => "Nine",
        10 => "Ten",
        _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
