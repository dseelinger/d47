namespace D47.Core.Conversation;

/// <summary>
/// The system a nearest-first search last found, so "set a course" has something to mean
/// without the Commander repeating a name they just heard (#325).
/// </summary>
public sealed class LastFoundSystem
{
    /// <summary>The system last found, or null when nothing has been found yet.</summary>
    public string? System { get; private set; }

    /// <summary>The station the search found in <see cref="System"/>, when it found a station.</summary>
    public string? Station { get; private set; }

    /// <summary>Records what was found.</summary>
    public void Remember(string? system, string? station = null)
    {
        System = string.IsNullOrWhiteSpace(system) ? null : system;
        Station = System is null || string.IsNullOrWhiteSpace(station) ? null : station;
    }
}
