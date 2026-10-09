using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheCustomFilterShowsOnlyYourVoicesTests
{
    private static readonly VoiceInfo[] Voices =
    [
        new("marlow", "Marlow", "en", "female"),
        new("orson", "Orson", "en", "male"),
        new("own", "Your voice", "en") { Custom = true },
        new("my-0badf00d", "Ally", "en", "female") { Custom = true },
    ];

    private static SettingFacet? FacetOf(params VoiceInfo[] voices) =>
        SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Voices = _ => [.. voices.Select(voice => voice.Id)],
            VoiceGender = (_, id) => voices.First(voice => voice.Id == id).Gender,
            VoiceCustom = (_, id) => voices.First(voice => voice.Id == id).Custom,
        }).Settings.Single(row => row.Key == SpeechCapability.VoiceKey).Facet?.Invoke(D47Settings.Defaults);

    [Fact]
    public void CustomComesAfterUnlabelledAndMatchesCustomIdsAndOwnOnly()
    {
        var facet = FacetOf(Voices)!;

        Assert.Equal(["All", "Female", "Male", "Unlabelled", "Custom"], facet.Options.Select(option => option.Label));

        var custom = facet.Options.Single(option => option.Label == "Custom").Matches!;

        Assert.Equal(["own", "my-0badf00d"], Voices.Select(voice => voice.Id).Where(custom));
    }

    [Fact]
    public void WithoutACustomVoiceThereIsNoCustomOption() =>
        Assert.DoesNotContain(FacetOf(Voices[0], Voices[1])!.Options, option => option.Label == "Custom");
}
