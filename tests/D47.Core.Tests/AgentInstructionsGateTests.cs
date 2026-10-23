using Xunit;

namespace D47.Core.Tests;

/// <summary>AGENTS.md holds the project rules and CLAUDE.md imports it.</summary>
[Trait("Category", "Gate")]
public sealed class AgentInstructionsGateTests
{
    [Fact]
    public void AgentsMdExistsAtTheRepositoryRoot()
    {
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "AGENTS.md")), "AGENTS.md is missing from the repository root.");
    }

    [Fact]
    public void ClaudeMdImportsAgentsMdOnItsFirstLine()
    {
        var path = Path.Combine(RepositoryRoot(), "CLAUDE.md");
        Assert.True(File.Exists(path), "CLAUDE.md is missing from the repository root.");

        var first = File.ReadLines(path).FirstOrDefault(line => line.Trim().Length > 0)?.Trim();

        Assert.Equal("@AGENTS.md", first);
    }

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
