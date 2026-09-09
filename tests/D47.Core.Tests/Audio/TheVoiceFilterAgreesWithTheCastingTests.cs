using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The voice picker's gender filter, and the rule it must not disagree with.</summary>
public class TheVoiceFilterAgreesWithTheCastingTests
{
    /// <summary>
    /// Both spellings of every tag, because the two providers disagree and only on capitalisation —
    /// Edge writes "Female" and ElevenLabs writes "female".
    /// </summary>
    private static readonly VoiceInfo[] Catalogue =
    [
        new("ava", "Ava", "en-US", "Female"),
        new("emma", "Emma", "en-GB", "female"),
        new("george", "George", "en-GB", "male"),
        new("guy", "Guy", "en-US", "Male"),
        new("nova", "Nova", "en-US"),
        new("sol", "Sol", "en-AU", "neutral"),
    ];

    private static SpeechCapability.SpeechSurface Surface(params VoiceInfo[] voices) => new()
    {
        Silence = () => { },
        Beds = () => [],
        Voices = _ => [.. voices.Select(voice => voice.Id)],
        VoiceGender = (_, id) => voices.FirstOrDefault(voice =>
            string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))?.Gender,
    };

    private static SettingFacet? FacetOf(params VoiceInfo[] voices) =>
        SpeechCapability.Create(Surface(voices)).Settings
            .Single(row => row.Key == SpeechCapability.VoiceKey)
            .Facet?.Invoke(D47Settings.Defaults);

    private static IReadOnlySet<string> Under(SettingFacet facet, string option, IEnumerable<VoiceInfo> voices)
    {
        var matches = facet.Options.Single(o => o.Label == option).Matches!;

        return new HashSet<string>(
            voices.Select(voice => voice.Id).Where(matches),
            StringComparer.OrdinalIgnoreCase);
    }

    // ---- Agreeing with the casting ----------------------------------------------------------

    /// <summary>The criterion, asserted against one source rather than compared by eye.</summary>
    [Fact]
    public void TheFilterAndTheCastingAgreeAboutEveryVoice()
    {
        var facet = FacetOf(Catalogue);

        Assert.NotNull(facet);
        Assert.Equal(VoicePool.Feminine(Catalogue), Under(facet, "Female", Catalogue));
    }

    /// <summary>
    /// And that holds because they are the same comparison, not because they happen to agree on this
    /// list — both spellings of both tags, and a tag that is neither.
    /// </summary>
    [Theory]
    [InlineData("female", VoiceGender.Feminine)]
    [InlineData("Female", VoiceGender.Feminine)]
    [InlineData("FEMALE", VoiceGender.Feminine)]
    [InlineData("male", VoiceGender.Masculine)]
    [InlineData("Male", VoiceGender.Masculine)]
    [InlineData("neutral", VoiceGender.Unlabelled)]
    [InlineData("", VoiceGender.Unlabelled)]
    [InlineData(null, VoiceGender.Unlabelled)]
    public void TheTagIsReadTheSameWayEverywhere(string? tag, VoiceGender expected) =>
        Assert.Equal(expected, VoicePool.GenderOf(tag));

    /// <summary>"male" is equality on the whole tag, which is the bug in one line.</summary>
    [Fact]
    public void FemaleIsNotReadAsMale()
    {
        var facet = FacetOf(Catalogue)!;
        var men = Under(facet, "Male", Catalogue);

        Assert.Equal<string[]>(["george", "guy"], [.. men.Order(StringComparer.Ordinal)]);
        Assert.DoesNotContain("ava", men);
        Assert.DoesNotContain("emma", men);
    }

    // ---- Untagged, which is a decision rather than a leftover -------------------------------

    /// <summary>Untagged voices are a third state and are never silently dropped.</summary>
    [Fact]
    public void AnUntaggedVoiceIsItsOwnAnswerAndNotAMan()
    {
        var facet = FacetOf(Catalogue)!;

        var women = Under(facet, "Female", Catalogue);
        var men = Under(facet, "Male", Catalogue);
        var unlabelled = Under(facet, "Unlabelled", Catalogue);

        // "neutral" is a tag that is neither, and it belongs with the untagged rather than nowhere.
        Assert.Equal<string[]>(["nova", "sol"], [.. unlabelled.Order(StringComparer.Ordinal)]);

        // Every voice is under exactly one, so nothing can be lost between the options.
        foreach (var voice in Catalogue)
        {
            var found = new[] { women, men, unlabelled }.Count(set => set.Contains(voice.Id));

            Assert.True(found == 1, $"{voice.Id} is under {found} options rather than one.");
        }
    }

    /// <summary>
    /// The first option hides nothing, which is where the picker opens: a list that arrives
    /// pre-narrowed looks like a list with things missing from it.
    /// </summary>
    [Fact]
    public void TheFirstOptionTakesEverything()
    {
        var facet = FacetOf(Catalogue)!;

        Assert.Equal("All", facet.Options[0].Label);
        Assert.Null(facet.Options[0].Matches);
    }

    // ---- Offered where it means something ---------------------------------------------------

    /// <summary>
    /// Offered where the choices carry a gender and absent where they do not, the way the engineer
    /// filter is absent where there is no engineer.
    /// </summary>
    [Fact]
    public void TheFilterIsAbsentWhereNothingCarriesAGender()
    {
        Assert.Null(FacetOf(new VoiceInfo("nova", "Nova", "en-US"), new VoiceInfo("sol", "Sol", "en-GB")));
        Assert.NotNull(FacetOf(new VoiceInfo("ava", "Ava", "en-US", "Female")));
    }

    /// <summary>And absent with no voices at all, rather than a control over an empty list.</summary>
    [Fact]
    public void TheFilterIsAbsentWithNothingToFilter() => Assert.Null(FacetOf());

    /// <summary>
    /// All three voice rows get it, because a Commander casting the carrier's tower is choosing from
    /// the same catalogue as one casting the core aboard.
    /// </summary>
    [Theory]
    [InlineData(SpeechCapability.VoiceKey)]
    [InlineData(SpeechCapability.CarrierCaptainVoiceKey)]
    [InlineData(SpeechCapability.TowerVoiceKey)]
    public void EveryVoiceRowOffersIt(string key)
    {
        var row = SpeechCapability.Create(Surface(Catalogue)).Settings.Single(r => r.Key == key);

        Assert.NotNull(row.Facet?.Invoke(D47Settings.Defaults));
    }

    /// <summary>And nothing else does.</summary>
    [Fact]
    public void TheDeviceAndProviderRowsDoNotOfferIt()
    {
        var rows = SpeechCapability.Create(Surface(Catalogue)).Settings;

        Assert.Null(rows.Single(r => r.Key == SpeechCapability.OutputDeviceKey).Facet);
        Assert.Null(rows.Single(r => r.Key == SpeechCapability.ProviderKey).Facet);
    }
}
