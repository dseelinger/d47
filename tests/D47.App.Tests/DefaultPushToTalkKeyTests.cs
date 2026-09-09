using D47.App.Input;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>The push-to-talk key d47 ships bound to has to actually bind.</summary>
public class DefaultPushToTalkKeyTests
{
    [Fact]
    public void TheDefaultKeyBinds()
    {
        var gesture = D47Settings.Defaults.Listening.PushToTalkKey;

        Assert.False(
            string.IsNullOrWhiteSpace(gesture),
            "d47 ships with push-to-talk bound; an unbound default is a companion that cannot hear.");

        var key = new PushToTalkKey(NullLogger<PushToTalkKey>.Instance);

        Assert.True(key.Bind(gesture), $"the default push-to-talk gesture '{gesture}' did not bind.");
        Assert.Equal(gesture, key.Gesture);
    }

    /// <summary>And it is the right-hand one.</summary>
    [Fact]
    public void TheDefaultKeyIsTheRightShift()
    {
        Assert.Equal("RightShift", D47Settings.Defaults.Listening.PushToTalkKey);
        Assert.Equal(0xA1u, VirtualKeys.Of(Avalonia.Input.Key.RightShift));
        Assert.NotEqual(VirtualKeys.Of(Avalonia.Input.Key.LeftShift), VirtualKeys.Of(Avalonia.Input.Key.RightShift));
    }

    /// <summary>
    /// Clearing the row is still how a Commander asks d47 never to listen — the default moved, the
    /// choice did not go away.
    /// </summary>
    [Fact]
    public void ClearingItStillMeansNeverListening()
    {
        var key = new PushToTalkKey(NullLogger<PushToTalkKey>.Instance);

        Assert.False(key.Bind(null));
        Assert.Null(key.Gesture);
    }

    /// <summary>A combination is a real binding here, both halves of it.</summary>
    [Theory]
    [InlineData("Ctrl+D")]
    [InlineData("Ctrl+Alt+X")]
    [InlineData("Shift+F9")]
    [InlineData("RightShift")]
    [InlineData("F9")]
    public void ACombinationBindsAndKeepsItsModifiers(string gesture)
    {
        var key = new PushToTalkKey(NullLogger<PushToTalkKey>.Instance);

        Assert.True(key.Bind(gesture), $"'{gesture}' did not bind.");
        Assert.Equal(gesture, key.Gesture);

        // The modifiers are held privately, so what is asserted is the count that reached them — which is the
        // half a poll watching only the last key would have dropped.
        var modifiers = (uint[])typeof(PushToTalkKey)
            .GetField("_modifiers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(key)!;

        Assert.Equal(gesture.Count(character => character == '+'), modifiers.Length);
    }
}
