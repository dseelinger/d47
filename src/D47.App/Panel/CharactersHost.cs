using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The running story and the cast it has introduced, in the order they were introduced.</summary>
public sealed record RunningCast(string StoryId, string Title, IReadOnlyList<StoryCastMember> Members);

/// <summary>What Asset Mgmt › Characters reads from the app, shared by the window and the headset.</summary>
/// <param name="Story">The running story's introduced cast for a Commander, or null when none is running.</param>
/// <param name="Distance">Light years between two systems, or null when unknown.</param>
public sealed record CharactersHost(
    Func<CharacterStore> Store,
    SettingsService Settings,
    Func<string?, RunningCast?> Story,
    Func<string, string, CancellationToken, Task<double?>> Distance);
