using System.Globalization;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The Commanders found in the journals, which one d47 shows, and a way to pick another.</summary>
/// <param name="sightings">Null until the history walk has listed them.</param>
/// <param name="shown">The Frontier id of the Commander d47 shows.</param>
/// <param name="pick">Must hand the pick to the tick thread; game state is written nowhere else.</param>
public sealed class CommanderRoster(
    Func<IReadOnlyDictionary<string, CommanderSighting>?> sightings,
    Func<int> filesExamined,
    Func<string?> shown,
    Action<CommanderIdentity> pick)
{
    /// <summary>Every Commander found, most recently detected first.</summary>
    public IReadOnlyList<CommanderSighting> Commanders =>
        sightings() is { } found
            ? [.. found.Values.OrderByDescending(sighting => sighting.LastSeen).ThenBy(sighting => sighting.Name, StringComparer.Ordinal)]
            : [];

    public int FilesExamined => filesExamined();

    public string? ShownFrontierId => shown();

    /// <summary>Whether there is anyone to switch to.</summary>
    public bool CanSwitch => (sightings()?.Count ?? 0) >= 2;

    /// <summary>Changes whenever what a page draws from this would change.</summary>
    public (object? Sightings, string? Shown, int Files) Stamp => (sightings(), shown(), filesExamined());

    public void Pick(CommanderSighting sighting) => pick(new CommanderIdentity(sighting.FrontierId, sighting.Name));

    /// <summary>The name as the switcher and the list print it: <c>CMDR JOHN DEPARAGON</c>.</summary>
    public static string Called(string name) => "CMDR " + name.ToUpperInvariant();

    /// <summary>A last-detected time, in local time: <c>2026-10-05 · 19:40</c>.</summary>
    public static string Detected(DateTimeOffset at) =>
        at.ToLocalTime().ToString("yyyy-MM-dd · HH:mm", CultureInfo.InvariantCulture);
}
