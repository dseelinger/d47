using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Find is bound to Ctrl+K and its row sits after the ask box's under Look and keys (#1474).</summary>
public sealed class FindHasItsOwnKeyTests
{
    [Fact]
    public void FindDefaultsToCtrlK() => Assert.Equal("Ctrl+K", new HotkeySettings().Find);

    [Fact]
    public void ItsRowFollowsTheAskBoxRowInLookAndKeys()
    {
        var keys = SettingsLayout.Areas
            .SelectMany(a => a.Places)
            .SelectMany(p => p.Groups)
            .Single(g => g.Title == "Look and keys")
            .Entries
            .Select(e => e.ToString())
            .ToList();

        Assert.Contains(InterfaceCapability.FindHotkeyKey, string.Join('|', keys));
        Assert.True(string.Join('|', keys).IndexOf(InterfaceCapability.FocusAskHotkeyKey, StringComparison.Ordinal)
            < string.Join('|', keys).IndexOf(InterfaceCapability.FindHotkeyKey, StringComparison.Ordinal));
    }
}
