using System.Collections.Concurrent;
using System.Globalization;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Callouts;

/// <summary>Missions that hand in at the station docked at or left, and a mission about to expire (#662).</summary>
public sealed class MissionCallout : ICallout
{
    public string Id => "missions";

    public const string HandInKey = "missions.hand-in";

    public const string UnclaimedKey = "missions.unclaimed";

    public const string ExpiryKey = "missions.expiry";

    public const string RedirectedKey = "missions.redirected";

    public const string AcceptedKey = "missions.accepted";

    public const string TripKey = "missions.trip";

    /// <summary>Time allowed per jump, including scooping and the run to the station, when judging a trip against its expiry.</summary>
    public static readonly TimeSpan TimePerJump = TimeSpan.FromMinutes(5);

    /// <summary>The markets the Commander has seen; without it a collect mission says nothing about supply.</summary>
    public MarketBook? Markets { get; init; }

    /// <summary>Opened on a redirect, withdrawn once the mission is off the board.</summary>
    public HandInOffer? Offer { get; init; }

    /// <summary>The warnings, furthest first.</summary>
    private static readonly (string Name, TimeSpan Before, string Said)[] Warnings =
    [
        ("hour", TimeSpan.FromHours(1), "an hour"),
        ("ten", TimeSpan.FromMinutes(10), "ten minutes"),
    ];

    /// <summary>How far past its mark a warning can be and still be said.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    /// <summary>The running story's asides, or null when stories are not wired.</summary>
    public StoryMissionAsides? StoryAsides { get; set; }

    /// <summary>The service to ask, or null when galaxy search is off.</summary>
    public Func<IGalaxyService?> Galaxy { get; set; } = () => null;

    /// <summary>Starts the lookup off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; init; } = work => _ = Task.Run(work);

    public ILogger Log { get; init; } = NullLogger.Instance;

    private readonly HashSet<(long Id, string Warning)> _warned = [];

    private readonly ConcurrentQueue<Trip> _trips = new();

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var board = context.State?.Missions ?? MissionBoard.Empty;

        while (_trips.TryDequeue(out var trip))
        {
            if (board.For(trip.MissionId) is not null)
            {
                yield return new Announcement($"{TripKey}.{trip.MissionId}", trip.Said);
            }
        }

        if (context.IsPriming)
        {
            Prime(board, context.Now);
            yield break;
        }

        if (Offer is { MissionId: { } offered } && board.For(offered) is null)
        {
            Offer.Withdraw();
        }

        foreach (var journalEvent in context.Events)
        {
            var station = journalEvent.String("StationName");

            if (journalEvent.Kind == "MissionAccepted")
            {
                StartTripCheck(journalEvent, context);
            }

            switch (journalEvent.Kind)
            {
                case "MissionRedirected" when Redirect(journalEvent, board) is { } redirect:
                    yield return redirect;
                    break;

                case "MissionAccepted" when Accepted(journalEvent, context.State) is { } accepted:
                    yield return accepted;
                    break;

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

    /// <summary>
    /// A mission's cargo against the hold and a collect mission's commodity against the docked market, with the running
    /// story's aside. With an aside and no facts the line has no text, for the model to write.
    /// </summary>
    private Announcement? Accepted(JournalEvent journalEvent, CommanderGameState? state)
    {
        if (Mission.Of(journalEvent) is not { } mission)
        {
            return null;
        }

        var facts = Facts(mission, state);
        var aside = StoryAsides?.Take(mission);

        return facts is not null || aside is not null
            ? new Announcement($"{AcceptedKey}.{mission.Id}", facts ?? string.Empty) { StoryAside = aside }
            : null;
    }

    private string? Facts(Mission mission, CommanderGameState? state)
    {
        if (mission is not { CommodityLocalised: { } commodity, Count: > 0 and var count } || mission.PassengerMission)
        {
            return null;
        }

        var said = new List<string>();

        if (state?.Ship.CargoCapacity is > 0 and var capacity && count > capacity)
        {
            var trips = (count + capacity - 1) / capacity;
            said.Add($"That's {count} tons against a {capacity}-ton hold. {Words(trips)} trips, or a bigger ship.");
        }

        if (mission.Name.StartsWith("Mission_Collect", StringComparison.OrdinalIgnoreCase)
            && state?.Location is { Docked: true } here
            && Markets?.At(here.StarSystem, here.StationName)?.Quote(commodity) is { Supply: > 0 })
        {
            said.Add($"They sell {commodity} here.");
        }

        return said.Count > 0 ? string.Join(' ', said) : null;
    }

    /// <summary>Queues a distance lookup to the mission's destination; the answer is said on a later tick.</summary>
    private void StartTripCheck(JournalEvent journalEvent, CalloutContext context)
    {
        if (Mission.Of(journalEvent) is not { DestinationSystem: { Length: > 0 } destination, Expiry: { } expiry } mission
            || context.State?.Location.StarSystem is not { Length: > 0 } from
            || context.State.Ship.MaxJumpRange is not > 0
            || Galaxy() is not { } galaxy)
        {
            return;
        }

        var range = context.State.Ship.MaxJumpRange.GetValueOrDefault();
        var left = expiry - context.Now;

        if (left <= TimeSpan.Zero)
        {
            return;
        }

        Dispatch(() => CheckTrip(galaxy, mission.Id, from, destination, range, left));
    }

    private async Task CheckTrip(IGalaxyService galaxy, long id, string from, string destination, double range, TimeSpan left)
    {
        try
        {
            if (await galaxy.DistanceAsync(from, destination, CancellationToken.None).ConfigureAwait(false) is not { } distance)
            {
                return;
            }

            var jumps = (int)Math.Ceiling(distance / range);

            if (jumps > 0 && jumps * TimePerJump.Ticks > left.Ticks)
            {
                _trips.Enqueue(new Trip(id, $"{destination} is about {jumps} jumps. That's tight for {Deadline(left)} deadline."));
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "Could not measure the trip to {Destination} for mission {MissionId}", destination, id);
        }
    }

    private static string Deadline(TimeSpan left) =>
        left.TotalHours >= 1
            ? $"a {Math.Round(left.TotalHours).ToString(CultureInfo.InvariantCulture)}-hour"
            : $"a {Math.Max(1, Math.Round(left.TotalMinutes)).ToString(CultureInfo.InvariantCulture)}-minute";

    private readonly record struct Trip(long MissionId, string Said);

    private Announcement? Redirect(JournalEvent journalEvent, MissionBoard board)
    {
        if (journalEvent.Long("MissionID") is not { } id
            || board.For(id) is null
            || journalEvent.String("NewDestinationSystem") is not { Length: > 0 } system)
        {
            return null;
        }

        var title = journalEvent.String("LocalisedName") is { Length: > 0 } named ? named : board.For(id)!.Title;
        var station = journalEvent.String("NewDestinationStation");
        Offer?.Open(id, system);

        var moved = station is { Length: > 0 } ? $"{station} in {system}" : system;
        return new Announcement(
            $"{RedirectedKey}.{id}", $"That's {title} done. Hand-in moved to {moved}. Say plot it to set the course.");
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
