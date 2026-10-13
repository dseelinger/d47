using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Gate")]
public sealed class TheStoryBenchIsSwitchedOnInOnePlaceTests
{
    [Fact]
    public void OneLineOfTheAppReadsTheVariable()
    {
        var src = Path.Combine(AppSource.RepositoryRoot(), "src");
        var separator = Path.DirectorySeparatorChar;

        var reads = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select((text, index) => (Path: Path.GetRelativePath(src, path), Line: index + 1, Text: text)))
            .Where(line => line.Text.Contains("\"D47_STORY_BENCH\"", StringComparison.Ordinal))
            .ToList();

        var read = Assert.Single(reads);
        Assert.StartsWith($"D47.App{separator}", read.Path, StringComparison.Ordinal);
        Assert.Contains("Environment.GetEnvironmentVariable(", read.Text, StringComparison.Ordinal);
    }
}
