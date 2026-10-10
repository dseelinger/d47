using D47.Core.Capabilities.Builtin;
using D47.Core.Storage;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Scenarios.Tests;

/// <summary>
/// The live services and seed data a <see cref="ScenarioWorld"/> runs against when it stands in for the app
/// in a model comparison, rather than the inert world the safety scenarios use.
/// </summary>
public sealed record ScenarioServices
{
    /// <summary>The data files a world may be seeded with. Never secrets or settings.</summary>
    public static readonly IReadOnlyList<string> SeedableFiles =
    [
        "checklist.json", "checklist-proposals.json", "ships.json", "on-foot.json", "goals.json", "memories.json",
        "route-plans.json",
    ];

    /// <summary>A <c>data/</c> folder whose <see cref="SeedableFiles"/> are copied in, or null for none.</summary>
    public string? SeedFrom { get; init; }

    public IGalaxyService? Galaxy { get; init; }

    public IRouteService? Routes { get; init; }

    public IStarSystemService? StarSystems { get; init; }

    public VisitedStarsBook? VisitedStars { get; init; }

    public IScreenCapture? Screen { get; init; }

    public Func<DateTimeOffset>? Now { get; init; }

    /// <summary>Copies the seed files into <paramref name="data"/> on <paramref name="files"/>.</summary>
    public void Seed(IFileSystem files, string data)
    {
        if (SeedFrom is null)
        {
            return;
        }

        foreach (var name in SeedableFiles)
        {
            var source = Path.Combine(SeedFrom, name);

            if (File.Exists(source))
            {
                files.WriteBytes(Path.Combine(data, name), File.ReadAllBytes(source));
            }
        }
    }

    /// <summary>A navigation surface whose clipboard works and whose plots are confirmed.</summary>
    public NavigationSurface Navigation(ActionSurface actions, IClipboard clipboard, Func<bool> autoPlot) => new()
    {
        Clipboard = clipboard,
        Actions = actions,
        AutoPlotEnabled = autoPlot,
        WatchRoute = () => new FixedPlotWatch(true),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(true),
        SpellSystem = _ => { },
        OfferSpelling = _ => { },
    };
}
