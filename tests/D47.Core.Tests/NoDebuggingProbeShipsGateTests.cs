using Xunit;

namespace D47.Core.Tests;

/// <summary>No source under src/ carries a probe marked for removal, or writes into a Claude session's scratchpad.</summary>
public class NoDebuggingProbeShipsGateTests
{
    private static readonly string[] Markers =
    [
        "remove before commit",
        "scratchpad",
        "C--dev-d47",
    ];

    [Fact]
    public void NoSourceFileCarriesAProbeMarker()
    {
        var src = Path.Combine(RepositoryRoot(), "src");

        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(hit => Markers.Any(marker => hit.line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(hit => $"{Path.GetRelativePath(src, hit.file)}:{hit.number}: {hit.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}
