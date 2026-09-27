using System.Text.RegularExpressions;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The gate that keeps the docs figures matching the design's zero-radius ruling (#519).</summary>
public partial class FigureCornersAreSquareAndCapsAreFlatTests
{
    [Fact]
    public void NoInlineSvgInDocsHasARoundedCornerOrARoundCapOrJoin()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "docs"), "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepositoryRoot(), file).Replace('\\', '/');

            foreach (Match match in RoundedRadius().Matches(text))
            {
                offenders.Add($"{relative}: {match.Value}");
            }

            foreach (Match match in RoundCapOrJoin().Matches(text))
            {
                offenders.Add($"{relative}: {match.Value}");
            }
        }

        Assert.True(offenders.Count == 0, $"Figures with a non-zero radius or a round cap/join: {string.Join(", ", offenders)}");
    }

    [GeneratedRegex(@"r[xy]=""(?!0(?:\.0+)?"")[0-9.]+""")]
    private static partial Regex RoundedRadius();

    [GeneratedRegex(@"stroke-linecap=""round""|stroke-linejoin=""round""")]
    private static partial Regex RoundCapOrJoin();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
