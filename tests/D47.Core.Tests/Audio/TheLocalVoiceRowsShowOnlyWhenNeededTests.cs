using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheLocalVoiceRowsShowOnlyWhenNeededTests
{
    private static readonly string[] Providers = [TtsProviderCatalog.ChatterboxId, TtsProviderCatalog.KokoroId];

    private static SettingRow Row(string providerId, bool installed, bool storyUses)
    {
        var surface = new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            ChatterboxInstalled = () => installed,
            LocalVoiceInstalled = () => installed,
            StoryCastUses = id => storyUses && id == providerId,
        };
        var key = providerId == TtsProviderCatalog.ChatterboxId
            ? SpeechCapability.ChatterboxVoiceKey
            : SpeechCapability.LocalVoiceKey;
        return SpeechCapability.Create(surface).Settings.Single(r => r.Key == key);
    }

    private static D47Settings Aboard(string providerId) =>
        new() { Speech = new SpeechSettings { Provider = providerId } };

    private static D47Settings AboardElevenLabs() =>
        new() { Speech = new SpeechSettings { Provider = TtsProviderCatalog.ElevenLabsId } };

    private static D47Settings CarrierOn(string providerId) =>
        new()
        {
            Speech = new SpeechSettings
            {
                Provider = TtsProviderCatalog.ElevenLabsId,
                GroupProviders = new Dictionary<string, string> { ["carrier"] = providerId },
            },
        };

    public static TheoryData<string> Each() => new(Providers);

    [Theory, MemberData(nameof(Each))]
    public void TheVoiceProviderShowsTheRowWhetherOrNotItIsDownloaded(string id)
    {
        Assert.True(Row(id, installed: true, storyUses: false).AppliesWhen!(Aboard(id)));
        Assert.True(Row(id, installed: false, storyUses: false).AppliesWhen!(Aboard(id)));
    }

    [Theory, MemberData(nameof(Each))]
    public void AnotherSlotShowsTheRowWhileTheModelIsMissing(string id) =>
        Assert.True(Row(id, installed: false, storyUses: false).AppliesWhen!(CarrierOn(id)));

    [Theory, MemberData(nameof(Each))]
    public void AnotherSlotDoesNotShowTheRowOnceTheModelIsDownloaded(string id) =>
        Assert.False(Row(id, installed: true, storyUses: false).AppliesWhen!(CarrierOn(id)));

    [Theory, MemberData(nameof(Each))]
    public void AStoryCastShowsTheRowWhileTheModelIsMissing(string id) =>
        Assert.True(Row(id, installed: false, storyUses: true).AppliesWhen!(AboardElevenLabs()));

    [Theory, MemberData(nameof(Each))]
    public void AStoryCastDoesNotShowTheRowOnceTheModelIsDownloaded(string id) =>
        Assert.False(Row(id, installed: true, storyUses: true).AppliesWhen!(AboardElevenLabs()));

    [Theory, MemberData(nameof(Each))]
    public void NothingUsingTheProviderHidesTheRowDownloadedOrNot(string id)
    {
        Assert.False(Row(id, installed: false, storyUses: false).AppliesWhen!(AboardElevenLabs()));
        Assert.False(Row(id, installed: true, storyUses: false).AppliesWhen!(AboardElevenLabs()));
    }

    [Theory, MemberData(nameof(Each))]
    public void NoInstallerCountsAsNotDownloaded(string id)
    {
        var key = id == TtsProviderCatalog.ChatterboxId
            ? SpeechCapability.ChatterboxVoiceKey
            : SpeechCapability.LocalVoiceKey;
        var row = SpeechCapability.Create(new SpeechCapability.SpeechSurface { Silence = () => { } })
            .Settings.Single(r => r.Key == key);

        Assert.True(row.AppliesWhen!(CarrierOn(id)));
    }
}
