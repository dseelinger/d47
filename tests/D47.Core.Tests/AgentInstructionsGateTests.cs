using Xunit;

namespace D47.Core.Tests;

/// <summary>AGENTS.md holds the project rules and CLAUDE.md imports it.</summary>
[Trait("Category", "Gate")]
public sealed class AgentInstructionsGateTests
{
    [Fact]
    public void AgentsMdExistsAtTheRepositoryRoot()
    {
        Assert.True(File.Exists(Path.Combine(RepositoryFolder.Root(), "AGENTS.md")), "AGENTS.md is missing from the repository root.");
    }

    [Fact]
    public void ClaudeMdImportsAgentsMdOnItsFirstLine()
    {
        var path = Path.Combine(RepositoryFolder.Root(), "CLAUDE.md");
        Assert.True(File.Exists(path), "CLAUDE.md is missing from the repository root.");

        var first = File.ReadLines(path).FirstOrDefault(line => line.Trim().Length > 0)?.Trim();

        Assert.Equal("@AGENTS.md", first);
    }
}
