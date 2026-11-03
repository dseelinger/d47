using System.Text.RegularExpressions;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// tools/music/catalogue.py has a brief for every music folder d47 plays from, and every run writes the catalogue
/// before it generates, so a new brief is generated on the next run.
/// </summary>
[Trait("Category", "Gate")]
public class EveryMusicFolderHasAGeneratorBriefGateTests
{
    private static string Music(string file) =>
        File.ReadAllText(Path.Combine(RepositoryFolder.Root(), "tools", "music", file)).ReplaceLineEndings("\n");

    [Fact]
    public void TheBriefsNameExactlyTheMusicFolders()
    {
        var source = Music("catalogue.py");
        var start = source.IndexOf("BRIEFS = {", StringComparison.Ordinal);
        Assert.True(start >= 0, "catalogue.py has no BRIEFS table.");
        var end = source.IndexOf("\n}\n", start, StringComparison.Ordinal);
        var table = source[start..end];

        var briefs = Regex.Matches(table, @"^    ""([a-z-]+)"": \(", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Situations.All.Order(StringComparer.Ordinal), briefs);
    }

    [Theory]
    [InlineData("run.cmd")]
    [InlineData("weekly.cmd")]
    [InlineData("both.cmd")]
    public void ARunWritesTheCatalogueBeforeItGenerates(string script)
    {
        var text = Music(script);
        var catalogue = text.IndexOf("catalogue.py", StringComparison.Ordinal);
        var generate = new[] { "generate.py", "both.py" }
            .Select(name => text.IndexOf(name, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        Assert.True(catalogue >= 0, $"{script} never writes the catalogue.");
        Assert.True(generate >= 0, $"{script} never runs the generator.");
        Assert.True(catalogue < generate, $"{script} generates before it writes the catalogue.");
    }
}
