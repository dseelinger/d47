using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>A story aside for mission speech: the mission as the line may name it, and the excerpt of the story to tie it to.</summary>
public sealed record MissionAside(string Mission, string Excerpt);

/// <summary>
/// Hands out the running story's excerpt for speech about a mission, at most once per mission. Called from the tick
/// and from the pool.
/// </summary>
public sealed class StoryMissionAsides
{
    private readonly Lock _gate = new();

    private readonly HashSet<long> _told = [];

    /// <summary>The running story's excerpt, or null when no story is running, or it is paused or switched off.</summary>
    public Func<string?> Excerpt { get; set; } = () => null;

    /// <summary>The aside for this mission, or null when there is no running story or the mission has had its aside.</summary>
    public MissionAside? Take(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return Take([mission]);
    }

    /// <summary>
    /// One aside naming every mission here that has not had one, or null when there is none or no running story. Every
    /// mission it names counts as having had its aside.
    /// </summary>
    public MissionAside? Take(IReadOnlyList<Mission> missions)
    {
        ArgumentNullException.ThrowIfNull(missions);

        lock (_gate)
        {
            var untold = missions.Where(mission => !_told.Contains(mission.Id)).DistinctBy(mission => mission.Id).ToList();

            if (untold.Count == 0 || Excerpt() is not { Length: > 0 } excerpt)
            {
                return null;
            }

            foreach (var mission in untold)
            {
                _told.Add(mission.Id);
            }

            return new MissionAside(string.Join("; ", untold.Select(Describe)), excerpt);
        }
    }

    /// <summary>Whether this mission has had its aside.</summary>
    public bool Told(long missionId)
    {
        lock (_gate)
        {
            return _told.Contains(missionId);
        }
    }

    /// <summary>A mission by name, giver and destination where known.</summary>
    public static string Describe(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return mission.Title
            + (mission.Faction is { Length: > 0 } faction ? $" for {faction}" : string.Empty)
            + (mission.Destination is { Length: > 0 } destination ? $", to {destination}" : string.Empty);
    }
}
