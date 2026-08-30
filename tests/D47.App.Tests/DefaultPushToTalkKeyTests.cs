using D47.App.Input;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// The push-to-talk key d47 ships bound to has to actually bind.
/// <para>
/// Two things can go wrong silently between the settings default and an open microphone, and
/// both fail the same way — a warning in a log nobody is reading and a companion that never
/// hears anything. The gesture has to parse, and the key it parses to has to be in the
/// virtual-key table; a key that is missing from that table is reported as unbindable, which is
/// the right behaviour and a terrible default.
/// </para>
/// <para>
/// Asserted against <see cref="D47Settings.Defaults"/> rather than a literal, so this covers
/// whatever the shipped default is rather than the one it was when this was written.
/// </para>
/// </summary>
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

    /// <summary>
    /// And it is the right-hand one. The sided virtual-key codes are the reason this can be
    /// stated at all: bound to the generic shift, the left one a Commander is already using in
    /// the game would open the microphone too.
    /// </summary>
    [Fact]
    public void TheDefaultKeyIsTheRightShift()
    {
        Assert.Equal("RightShift", D47Settings.Defaults.Listening.PushToTalkKey);
        Assert.Equal(0xA1u, VirtualKeys.Of(Avalonia.Input.Key.RightShift));
        Assert.NotEqual(VirtualKeys.Of(Avalonia.Input.Key.LeftShift), VirtualKeys.Of(Avalonia.Input.Key.RightShift));
    }

    /// <summary>
    /// Clearing the row is still how a Commander asks d47 never to listen — the default moved,
    /// the choice did not go away.
    /// </summary>
    [Fact]
    public void ClearingItStillMeansNeverListening()
    {
        var key = new PushToTalkKey(NullLogger<PushToTalkKey>.Instance);

        Assert.False(key.Bind(null));
        Assert.Null(key.Gesture);
    }

    /// <summary>
    /// <b>A combination is a real binding here, both halves of it.</b> The row accepts one and the
    /// poll honours it: <c>Poll</c> reads the bound virtual-key code <em>and</em> every modifier
    /// the gesture declared, so <c>Ctrl+D</c> opens the microphone only while both are held. Worth
    /// pinning because the failure would be silent and one-sided — a control that happily stores
    /// <c>Ctrl+D</c> over a poll watching only D would open the microphone on every D typed
    /// anywhere, which is the opposite of what binding a modifier is for.
    /// </summary>
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

        // The modifiers are held privately, so what is asserted is the count that reached them —
        // which is the half a poll watching only the last key would have dropped.
        var modifiers = (uint[])typeof(PushToTalkKey)
            .GetField("_modifiers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(key)!;

        Assert.Equal(gesture.Count(character => character == '+'), modifiers.Length);
    }
}
