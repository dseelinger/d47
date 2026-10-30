using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every path written with " › " in the docs and the Exercise strings names a place the panel has.</summary>
[Trait("Category", "Gate")]
public sealed class EveryPanelPathTheHelpCitesIsARealPlaceTests
{
    /// <summary>Paths that do not resolve yet, by source (a docs page, or "exercise:" and a capability id) and path.</summary>
    private static readonly (string Source, string Path)[] Unresolved = [
        ("choosing-a-module.md", "Asset Mgmt › a ship › a slot › Module — the list of everything that fits"),
        ("capabilities/adventures.md", "Your core › Its voice"),
        ("capabilities/adventures.md", "Other voices › Narrator and stories"),
        ("capabilities/adventures.md", "Providers › Speech synthesis appear while a downloaded story's cast needs them and the model is not downloaded"),
        ("capabilities/crew.md", "Fleet › Crew has Change picture and Use the default"),
        ("capabilities/interface.md", "Settings › Your core › In flight › Flight callouts › Fuel and range"),
        ("capabilities/speech.md", "Your core › Its voice page each effect is one line"),
        ("exercise:learned-phrases", "Settings › Phrases"),
    ];

    [AvaloniaFact]
    public void EveryTabThePanelDrawsHasDestinations()
    {
        using var host = Furnished();

        var bare = host.Panel.TabWords.Keys.Where(tab => host.Panel.Nav.Destinations.All(d => d.Tab != tab)).ToList();

        Assert.True(bare.Count == 0, "No destinations on: " + string.Join(", ", bare));
    }

    [AvaloniaFact]
    public void EveryWrittenPathResolvesOrIsAllowed()
    {
        using var host = Furnished();
        var checker = new PanelPathChecker(host.Panel.Nav.Destinations, host.Panel.TabWords);
        var (_, _, _, registry, _) = TestSurface.CreateFull(coverage: () => "x");

        var written = Written(registry).ToList();
        var failing = written
            .Select(item => (item.Source, Key: checker.Key(item.Path), Why: checker.Failure(item.Path)))
            .Where(item => item.Why is not null)
            .ToList();

        var allowed = Unresolved.ToHashSet();
        var unexpected = failing.Where(item => !allowed.Contains((item.Source, item.Key))).ToList();
        var stale = Unresolved.Where(entry => !failing.Any(item => item.Source == entry.Source && item.Key == entry.Path)).ToList();

        Assert.True(written.Count(item => item.Source.StartsWith("exercise:", StringComparison.Ordinal)) > 100, "Few Exercise paths were read: " + written.Count(item => item.Source.StartsWith("exercise:", StringComparison.Ordinal)));
        Assert.True(written.Count(item => !item.Source.StartsWith("exercise:", StringComparison.Ordinal)) > 10, "Few docs paths were read");

        Assert.True(unexpected.Count == 0, "Not a place: " + string.Join(Environment.NewLine, unexpected.DistinctBy(item => (item.Source, item.Key)).Select(item => $"(\"{item.Source}\", \"{item.Key}\"), // {item.Why}")));
        Assert.True(stale.Count == 0, "Allowed but no longer failing or written: " + string.Join("; ", stale.Select(entry => $"{entry.Source}: {entry.Path}")));
    }

    private static IEnumerable<(string Source, WrittenPath Path)> Written(CapabilityRegistry registry)
    {
        var docs = Path.Combine(RepositoryRoot(), "docs");

        foreach (var file in Directory.EnumerateFiles(docs, "*.md").Concat(Directory.EnumerateFiles(Path.Combine(docs, "capabilities"), "*.md")))
        {
            var source = Path.GetRelativePath(docs, file).Replace(Path.DirectorySeparatorChar, '/');

            foreach (var path in PanelPathReader.ReadMarkdown(File.ReadAllText(file)))
            {
                yield return (source, path);
            }
        }

        foreach (var capability in registry.All)
        {
            var exercises = capability.Descriptor.Settings.Select(row => row.Exercise)
                .Concat(capability.Descriptor.Tools.Select(tool => tool.Exercise));

            foreach (var exercise in exercises.OfType<string>())
            {
                foreach (var path in PanelPathReader.Read(exercise))
                {
                    yield return ("exercise:" + capability.Descriptor.Id, path);
                }
            }
        }
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "d47.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("d47.slnx not found above " + AppContext.BaseDirectory);
    }

    private static PanelPathFixture Furnished() => PanelPathFixture.Open();
}
