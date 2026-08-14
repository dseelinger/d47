using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Rows disagree about whether their default reads as an aside — "(the provider's default)" —
/// or as a value, like a model name, and they always will. Every surface then frames it its own
/// way, which is how "Use the default ((the provider's default))" and "(the provider's default)
/// (default)" both reached the screen.
/// </summary>
public class DefaultDisplayTests
{
    [Fact]
    public void ADefaultDeclaredAsAnAsideComesBackWithoutItsBrackets()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var voice = surface.Settings.Find(SpeechCapability.VoiceKey);
        Assert.NotNull(voice);

        // Named as far as it can be: d47 cannot know which voice a provider picks when asked
        // for nothing, but it can say whose default is being talked about.
        Assert.Equal("(Edge Neural's own default voice)", voice.DefaultDisplayFor(surface.Settings.Current));
        Assert.Equal("Edge Neural's own default voice", voice.BareDefaultFor(surface.Settings.Current));
    }

    [Fact]
    public void ADefaultThatIsAValueIsLeftAlone()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var model = surface.Settings.Find(ConversationCapability.ModelKey);
        Assert.NotNull(model);

        Assert.Equal("claude-sonnet-5", model.BareDefaultFor(surface.Settings.Current));
    }

    /// <summary>
    /// The property every surface reads, checked across the whole registered surface so a row
    /// added later cannot reintroduce the doubling by being written the other way.
    /// </summary>
    [Fact]
    public void NoRowOffersABareDefaultThatIsStillBracketed()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        foreach (var row in surface.Registry.All.SelectMany(c => c.Descriptor.Settings))
        {
            if (row.BareDefaultFor(surface.Settings.Current) is not { Length: > 0 } bare)
            {
                continue;
            }

            Assert.False(
                bare[0] == '(' && bare[^1] == ')',
                $"the default for '{row.Key}' is still bracketed: {bare}");
        }
    }
}
