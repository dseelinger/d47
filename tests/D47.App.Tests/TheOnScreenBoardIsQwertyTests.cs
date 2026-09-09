using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The drawn keyboard, and there is one of it.</summary>
public class TheOnScreenBoardIsQwertyTests
{
    [Fact]
    public void TheLettersAreWhereAKeyboardPutsThem()
    {
        Assert.Equal(["1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm-_.", " "], PanelPrompts.Keys);
    }

    /// <summary>Nothing is lost in the rearrangement.</summary>
    [Fact]
    public void EverythingThatWasTypeableStillIs()
    {
        var reachable = string.Concat(PanelPrompts.Keys);

        foreach (var key in "abcdefghijklmnopqrstuvwxyz0123456789-_. ")
        {
            Assert.Contains(key, reachable);
        }

        // And nothing arrived twice on the way — a duplicated key is a row that silently got longer and a
        // letter that has two places to be hunted for.
        Assert.Equal(reachable.Length, reachable.Distinct().Count());
    }

    /// <summary>The headset's board is the window's board.</summary>
    [Fact]
    public void TheHeadsetDrawsTheSameBoard()
    {
        var offscreen = typeof(OffscreenSurface)
            .GetProperty("Keys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(offscreen);
        Assert.Same(PanelPrompts.Keys, offscreen.GetValue(null));
    }
}
