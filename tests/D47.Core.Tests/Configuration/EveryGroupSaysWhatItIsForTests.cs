using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Every group on the settings page has a title and a one-line description, and resets alone (#436).</summary>
public class EveryGroupSaysWhatItIsForTests
{
    [Fact]
    public void EveryGroupHasATitleAndADescription()
    {
        var bare = SettingsLayout.Areas
            .SelectMany(area => area.Places)
            .SelectMany(place => place.Groups.Select((group, i) => (place.Id, i, group)))
            .Where(g => string.IsNullOrWhiteSpace(g.group.Title) || string.IsNullOrWhiteSpace(g.group.Help))
            .Select(g => $"{g.Id} group {g.i}")
            .ToArray();

        Assert.True(bare.Length == 0, $"Untitled or undescribed: {string.Join(", ", bare)}");
    }

    [Fact]
    public void EachPlacementGroupIsTheOneItsSurfacesResetClears()
    {
        var headset = SettingsLayout.Areas.SelectMany(a => a.Places).Single(p => p.Id == "headset");

        var slots = headset.Groups
            .Select(group => VrCapability.SlotForPlacementGroup(group.Title))
            .OfType<string>()
            .ToArray();

        Assert.Equal([VrCapability.CurrentSlot, VrCapability.PanelSlot, VrCapability.MiniSlot], slots);
    }

    [Fact]
    public void ResettingAGroupLeavesTheOtherGroupsInItsPlaceAlone()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var place = SettingsLayout.Areas.SelectMany(a => a.Places).Single(p => p.Id == "voice-input");
        var microphone = place.Groups.ToList().FindIndex(g => g.Title == "Microphone");

        surface.Settings.Apply(ListeningCapability.PushToTalkKeyKey, "F9", SettingsCaller.Panel);
        surface.Settings.Apply(ListeningCapability.ModeKey, ListeningCapability.WakeMode, SettingsCaller.Panel);
        surface.Settings.Apply("listening.wakeWindow", "20", SettingsCaller.Panel);

        var moved = surface.Settings.ResetGroup("voice-input", microphone, SettingsCaller.Panel);

        Assert.Equal(2, moved);
        Assert.False(surface.Settings.IsChanged(ListeningCapability.PushToTalkKeyKey));
        Assert.False(surface.Settings.IsChanged(ListeningCapability.ModeKey));
        Assert.True(surface.Settings.IsChanged("listening.wakeWindow"));
    }

    [Fact]
    public void ResettingThePanelOnScreenGivesTheBigPanelItsOwnDefaultSize()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var headset = SettingsLayout.Areas.SelectMany(a => a.Places).Single(p => p.Id == "headset");
        var current = headset.Groups.ToList().FindIndex(g => g.Title == "Panel you are looking at placement");

        surface.Settings.Apply(VrCapability.EnabledKey, "true", SettingsCaller.Panel);
        surface.Settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        Assert.False(surface.Settings.IsChanged("vr.current.size"));

        surface.Settings.Apply("vr.current.size", "2", SettingsCaller.Panel);
        surface.Settings.ResetGroup("headset", current, SettingsCaller.Panel);

        Assert.Equal(D47Settings.Defaults.Vr.Panel.Width, surface.Settings.Current.Vr.Panel.Width);
        Assert.Equal(D47Settings.Defaults.Vr.Mini.Width, surface.Settings.Current.Vr.Mini.Width);
        Assert.False(surface.Settings.IsChanged("vr.current.size"));
    }

    [Fact]
    public void ResettingPushToTalkBindsItsDefaultKeyRatherThanNone()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ListeningCapability.PushToTalkKeyKey, "F9", SettingsCaller.Panel);
        surface.Settings.Reset(ListeningCapability.PushToTalkKeyKey, SettingsCaller.Panel);

        Assert.Equal(D47Settings.Defaults.Listening.PushToTalkKey, surface.Settings.Current.Listening.PushToTalkKey);
        Assert.False(surface.Settings.IsChanged(ListeningCapability.PushToTalkKeyKey));
    }
}
