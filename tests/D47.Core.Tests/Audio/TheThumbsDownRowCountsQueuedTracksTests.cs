using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The Thumbs-down tracks row in the Ambient music group (#2106).</summary>
public class TheThumbsDownRowCountsQueuedTracksTests
{
    private int _queued;
    private bool _maintainer = true;
    private bool _running;
    private int _pressed;

    private SettingRow Row() => AudioCapability.Create(
            maintainer: () => _maintainer,
            rejectCount: () => _queued,
            rejectsRunning: () => _running,
            deleteRejectsNow: () => _pressed++,
            watchRejects: _ => () => { })
        .Settings.Single(row => row.Key == AudioCapability.MusicRejectsKey);

    [Fact]
    public void WithoutATokenTheRowIsNotDrawn()
    {
        _maintainer = false;
        Assert.False(Row().Applies(D47Settings.Defaults));
    }

    [Fact]
    public void WithATokenTheRowIsDrawnAfterTheTokenRow()
    {
        var keys = AudioCapability.Create(rejectCount: () => 0).Settings.Select(row => row.Key).ToList();
        Assert.Equal(keys.IndexOf(AudioCapability.MusicPublishTokenKey) + 1, keys.IndexOf(AudioCapability.MusicRejectsKey));
        Assert.True(Row().Applies(D47Settings.Defaults));
    }

    [Theory]
    [InlineData(0, "None queued")]
    [InlineData(1, "1 track queued for deletion")]
    [InlineData(3, "3 tracks queued for deletion")]
    public void TheValueSaysHowManyAreQueued(int queued, string text)
    {
        _queued = queued;
        Assert.Equal(text, Row().Binding!.Read(D47Settings.Defaults));
    }

    [Fact]
    public void DeleteIsDestructiveAndAsksTwice()
    {
        var row = Row();
        Assert.Equal("Delete", row.PressLabel);
        Assert.True(row.Destructive);
        Assert.True(row.ConfirmPress);
    }

    [Fact]
    public void DeleteIsGreyedWhenNothingIsQueuedOrARunIsGoing()
    {
        var row = Row();
        Assert.False(row.PressEnabled!(D47Settings.Defaults));

        _queued = 2;
        Assert.True(row.PressEnabled!(D47Settings.Defaults));

        _running = true;
        Assert.False(row.PressEnabled!(D47Settings.Defaults));
    }

    [Fact]
    public void PressingDeleteRunsTheDeletion()
    {
        Row().Press!();
        Assert.Equal(1, _pressed);
    }
}
