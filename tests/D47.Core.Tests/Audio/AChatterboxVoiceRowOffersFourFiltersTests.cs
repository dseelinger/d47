using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AChatterboxVoiceRowOffersFourFiltersTests
{
    private static readonly VoiceInfo[] Voices =
    [
        new("f-low-even", "Ada", "en", "female") { Pitch = "low", Pace = "even" },
        new("f-low-slow", "Bea", "en", "female") { Pitch = "low", Pace = "slow" },
        new("f-high-even", "Cy", "en", "female") { Pitch = "high", Pace = "even" },
        new("m-low-even", "Dev", "en", "male") { Pitch = "low", Pace = "even" },
        new("m-mid-brisk", "Eli", "en", "male") { Pitch = "mid", Pace = "brisk" },
        new("fetched-f-low-even", "Fay", "en", "female") { Pitch = "low", Pace = "even" },
        new("my-0badf00d", "Gale", "en", "female") { Custom = true, Pitch = "low", Pace = "even" },
        new("my-0ddba11", "Hal", "en") { Custom = true, Pitch = "low", Pace = "even" },
        new("own", "Your voice", "en") { Custom = true },
    ];

    private static readonly D47Settings Chatterbox = D47Settings.Defaults with
    {
        Speech = D47Settings.Defaults.Speech with { Provider = TtsProviderCatalog.ChatterboxId },
    };

    private static SettingRow Row(string key) =>
        SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Voices = _ => [.. Voices.Select(voice => voice.Id)],
            VoiceGender = (_, id) => Find(id).Gender,
            VoicePitch = (_, id) => Find(id).Pitch,
            VoicePace = (_, id) => Find(id).Pace,
            VoiceCustom = (_, id) => Find(id).Custom,
            VoiceClip = (_, id) => Find(id) switch
            {
                { Custom: true } => new ChatterboxClipState(ChatterboxClip.Custom, 0),
                { Id: "fetched-f-low-even" } => new ChatterboxClipState(ChatterboxClip.NotFetched, 100),
                _ => new ChatterboxClipState(ChatterboxClip.Shipped, 100),
            },
        }).Settings.Single(row => row.Key == key);

    private static VoiceInfo Find(string id) => Voices.Single(voice => voice.Id == id);

    private static IReadOnlyList<SettingFacet> Facets(string key = SpeechCapability.VoiceKey) =>
        Row(key).Facets!.Invoke(Chatterbox)!;

    private static string[] Listed(IReadOnlyList<SettingFacet> facets, params string[] chosen)
    {
        var predicates = facets
            .Zip(chosen, (facet, label) => facet.Options.Single(option => option.Label == label).Matches)
            .OfType<Func<string, bool>>()
            .ToArray();

        return [.. Voices.Select(voice => voice.Id).Where(id => predicates.All(matches => matches(id)))];
    }

    [Fact]
    public void TheRowOffersGenderPitchPaceAndShow()
    {
        var facets = Facets();

        Assert.Equal(["Gender", "Pitch", "Pace", "Show"], facets.Select(facet => facet.Label));
        Assert.Equal(["Any", "Female", "Male"], facets[0].Options.Select(option => option.Label));
        Assert.Equal(["Any", "Low", "Mid", "High"], facets[1].Options.Select(option => option.Label));
        Assert.Equal(["Any", "Slow", "Even", "Brisk"], facets[2].Options.Select(option => option.Label));
        Assert.Equal(["All", "On this PC", "Custom"], facets[3].Options.Select(option => option.Label));
    }

    [Fact]
    public void TheFacetsCombineWithEachOther()
    {
        var facets = Facets();

        Assert.Equal(
            ["f-low-even", "fetched-f-low-even", "my-0badf00d"],
            Listed(facets, "Female", "Low", "Even", "All"));

        Assert.Equal(["f-low-slow"], Listed(facets, "Female", "Low", "Slow", "All"));
        Assert.Equal(["f-low-even", "my-0badf00d"], Listed(facets, "Female", "Low", "Even", "On this PC"));
        Assert.Equal(["my-0badf00d"], Listed(facets, "Female", "Low", "Even", "Custom"));
        Assert.Equal(["m-mid-brisk"], Listed(facets, "Male", "Mid", "Brisk", "All"));
    }

    [Fact]
    public void ACustomVoiceIsMatchedByItsOwnPitchAndPace()
    {
        var facets = Facets();

        Assert.Contains("my-0badf00d", Listed(facets, "Female", "Low", "Even", "All"));
        Assert.DoesNotContain("my-0badf00d", Listed(facets, "Female", "High", "Even", "All"));
        Assert.DoesNotContain("my-0badf00d", Listed(facets, "Female", "Low", "Brisk", "All"));
    }

    [Fact]
    public void ACustomVoiceSavedWithNoGenderAppearsUnderAnyOnly()
    {
        var facets = Facets();

        Assert.Contains("my-0ddba11", Listed(facets, "Any", "Low", "Even", "All"));
        Assert.DoesNotContain("my-0ddba11", Listed(facets, "Female", "Low", "Even", "All"));
        Assert.DoesNotContain("my-0ddba11", Listed(facets, "Male", "Low", "Even", "All"));
    }

    [Fact]
    public void OnThisPcLeavesOutAVoiceNotFetched()
    {
        var onThisPc = Listed(Facets(), "Any", "Any", "Any", "On this PC");

        Assert.DoesNotContain("fetched-f-low-even", onThisPc);
        Assert.Contains("own", onThisPc);
        Assert.Contains("f-low-even", onThisPc);
    }

    [Theory]
    [InlineData(SpeechCapability.VoiceKey)]
    [InlineData(SpeechCapability.CarrierCaptainVoiceKey)]
    [InlineData(SpeechCapability.TowerVoiceKey)]
    [InlineData(SpeechCapability.NarratorVoiceKey)]
    public void EveryVoiceRowOnChatterboxOffersThem(string key) =>
        Assert.Equal(4, Facets(key).Count);

    [Theory]
    [InlineData(TtsProviderCatalog.KokoroId)]
    [InlineData(TtsProviderCatalog.OpenAiId)]
    public void ARowWhoseSlotSpeaksThroughAnotherProviderOffersOnlyGender(string provider)
    {
        var settings = D47Settings.Defaults with { Speech = D47Settings.Defaults.Speech with { Provider = provider } };
        var row = Row(SpeechCapability.VoiceKey);

        Assert.Null(row.Facets!.Invoke(settings));
        Assert.Equal("Gender", row.Facet!.Invoke(settings)!.Label);
    }
}
