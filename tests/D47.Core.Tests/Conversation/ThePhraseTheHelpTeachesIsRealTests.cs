using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The Learned phrases page teaches "wheels out" for "gear down", so the surface must have that phrase and both teach commands.</summary>
public class ThePhraseTheHelpTeachesIsRealTests
{
    private static KeywordRouter Router() => new(TestSurface.For(new MemoryInstall()).Registry);

    [Fact]
    public void GearDownIsAPhraseInTheBook()
    {
        Assert.Contains(Router().Book.Entries, entry => string.Equals(entry.Phrase, "gear down", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("teach a phrase")]
    [InlineData("new phrase")]
    public void TheTeachCommandsReachTheTeachTool(string said)
    {
        Assert.Equal(LearnedPhrasesCapability.TeachTool, Router().MatchToolCommand(said)?.ToolName);
    }
}
