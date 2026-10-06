using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests;

/// <summary>Every shipped core, and the Captain and Tower in both versions, has a portrait in <c>assets\portraits</c>.</summary>
public sealed class EveryCoreShipsAPortraitTests
{
    private const long MaxBytes = 200 * 1024;

    private static readonly string[] Fixed =
    [
        "captain.man", "captain.woman", "tower.man", "tower.woman", "narrator",
    ];

    [Fact]
    public void EveryShippedCoreHasAPortrait()
    {
        var missing = AllCores().Where(id => !File.Exists(Portrait($"core.{id}"))).ToList();

        Assert.True(missing.Count == 0, $"No portrait in assets/portraits for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void TheCaptainAndTheTowerHaveBothVersions()
    {
        var missing = Fixed.Where(name => name != "narrator" && !File.Exists(Portrait(name))).ToList();

        Assert.True(missing.Count == 0, $"No portrait in assets/portraits for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void NoPortraitIsOversizedOrNamedForNothing()
    {
        var known = AllCores().Select(id => $"core.{id}").Concat(Fixed).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Folder()))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            if (!string.Equals(Path.GetExtension(file), ".jpg", StringComparison.Ordinal) || !known.Contains(name))
            {
                problems.Add($"{Path.GetFileName(file)} is not the portrait of anything that exists");
            }
            else if (new FileInfo(file).Length > MaxBytes)
            {
                problems.Add($"{Path.GetFileName(file)} is over 200 KB");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<string> AllCores() => new[] { PersonaCatalog.Covas }.Concat(PersonaCatalog.Shipped).Select(core => core.Id);

    private static string Portrait(string name) => Path.Combine(Folder(), name + ".jpg");

    private static string Folder() => Path.Combine(RepositoryRoot(), "assets", "portraits");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }
}
