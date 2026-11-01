using Xunit;

namespace D47.Core.Tests;

/// <summary>No source under src/ carries a probe marked for removal, or writes into a Claude session's scratchpad.</summary>
[Trait("Category", "Gate")]
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
        var src = Path.Combine(RepositoryFolder.Root(), "src");

        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(hit => Markers.Any(marker => hit.line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(hit => $"{Path.GetRelativePath(src, hit.file)}:{hit.number}: {hit.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }
}
