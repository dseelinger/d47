using D47.Core.Capabilities.Builtin;

namespace D47.Core.Stories;

/// <summary>Resolves a spoken story title to one of the Commander's runs.</summary>
public static class StoryRunVoice
{
    /// <summary>One entry per title: whether its newest run is shelved, and whether its newest recorded run has lines.</summary>
    public static IReadOnlyList<AdventureCapability.StoryRunTitle> Titles(StoryDirector director, string? commander)
    {
        ArgumentNullException.ThrowIfNull(director);

        return
        [
            .. director.Stories.Runs(commander)
                .GroupBy(run => run.Title, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AdventureCapability.StoryRunTitle(
                    group.First().Title,
                    group.First().State == StoryState.Shelved,
                    group.Any(run => Recorded(director, commander, run)))),
        ];
    }

    /// <summary>The newest shelved run with this title.</summary>
    public static Story? Shelved(StoryDirector director, string? commander, string title) =>
        director.Stories.Runs(commander).FirstOrDefault(run => run.State == StoryState.Shelved && Named(run, title));

    /// <summary>The newest run with this title that has kept lines.</summary>
    public static Story? Recorded(StoryDirector director, string? commander, string title) =>
        director.Stories.Runs(commander).FirstOrDefault(run => Named(run, title) && Recorded(director, commander, run));

    /// <summary>Switches to the newest shelved run with this title, returning a refusal or null.</summary>
    public static Task<string?> SwitchAsync(
        StoryDirector director, string? commander, string title, DateTimeOffset now, CancellationToken cancellationToken) =>
        Shelved(director, commander, title) is { } run
            ? director.SwitchAsync(commander, run.Id, now, cancellationToken)
            : Task.FromResult<string?>($"No shelved story is called {title}.");

    private static bool Named(Story run, string title) =>
        string.Equals(run.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Recorded(StoryDirector director, string? commander, Story run) =>
        director.Records?.Read(commander, run).Count > 0;
}
