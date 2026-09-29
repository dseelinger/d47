using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AnInventedSpeakerHasAMannerNotAnOriginTests
{
    private static readonly VoiceInfo[] Listed =
    [
        new("gb-woman", "Sonia", "en-GB", "Female"),
        new("us-man", "Guy", "en-US", "Male"),
        new("ie-woman", "Emily", "en-IE", "Female"),
        new("au-man", "William", "en-AU", "Male"),
    ];

    private static VoiceCast Cast() => new()
    {
        Pool = VoicePool.From(Listed),
        Feminine = VoicePool.Feminine(Listed),
        British = VoicePool.British(Listed),
        Voices = Listed.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
        DefaultVoice = "core",
    };

    /// <summary>Words that describe where someone is from or how well they speak, rather than how they talk.</summary>
    private static readonly string[] Origins =
    [
        "accent", "accented", "native", "foreign", "foreigner", "fluent", "fluency", "broken", "grammar",
        "dialect", "language", "english", "tongue", "immigrant", "outworlder", "offworlder", "regional",
        "european", "asian", "african", "slavic", "latin", "nordic", "arab", "russian", "chinese",
        "german", "french", "spanish", "italian", "japanese", "korean", "scottish", "welsh", "cockney",
        "texan", "southern", "northern", "eastern", "western", "yankee", "empire", "imperial",
        "federal", "federation", "alliance", "colonial", "colonia", "sol", "earth", "terran",
    ];

    private static IEnumerable<string> Vocabulary()
    {
        foreach (var word in Origins)
        {
            yield return word;
        }

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures).Where(c => c.Name.Length > 0))
        {
            yield return culture.EnglishName;
        }

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            var region = new RegionInfo(culture.Name);

            yield return region.EnglishName;

            // Every accent the voice listings can name, as the prompt would say it.
            if (VoicePool.AccentOf(new VoiceInfo("probe", "Probe", "en-" + region.TwoLetterISORegionName, "Female")) is { } accent)
            {
                yield return accent;
            }
        }
    }

    [Fact]
    public void NoMannerNamesANationalityALanguageARegionOrAFluency()
    {
        var words = Vocabulary()
            .Where(word => word.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var manner in NpcChatter.Manners)
        {
            foreach (var word in words)
            {
                Assert.False(
                    Regex.IsMatch(manner, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase),
                    $"The manner \"{manner}\" names \"{word}\".");
            }
        }
    }

    [Fact]
    public void TheSpeakerPromptForbidsAccentSpellingAndNonNativeGrammar()
    {
        Assert.Contains("never where they are from", NpcChatter.Speaker, StringComparison.Ordinal);
        Assert.Contains("standard English grammar", NpcChatter.Speaker, StringComparison.Ordinal);
        Assert.Contains("phonetic spelling", NpcChatter.Speaker, StringComparison.Ordinal);
        Assert.Contains("dropped articles", NpcChatter.Speaker, StringComparison.Ordinal);
        Assert.Contains("non-native grammar", NpcChatter.Speaker, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoCastSpeakersInOneExchangeAreGivenDifferentManners()
    {
        for (var exchange = 0; exchange < 50; exchange++)
        {
            var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, exchange, "Sol");
            var prompt = NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: exchange, roster: roster);

            var given = NpcChatter.Manners.Where(manner => prompt.Contains($"manner: {manner}", StringComparison.Ordinal)).ToList();

            Assert.Equal(2, given.Count);
        }
    }

    [Fact]
    public void TwoUncastSpeakersInOneExchangeAreGivenDifferentManners()
    {
        for (var exchange = 0; exchange < 50; exchange++)
        {
            var prompt = NpcChatter.Instruction(NpcChatterKind.Controller, docked: true, exchangeIndex: exchange);
            var given = NpcChatter.Manners.Where(manner => prompt.Contains(manner, StringComparison.Ordinal)).ToList();

            Assert.Equal(2, given.Count);
        }
    }

    [Fact]
    public void AHailHasOneSpeakerAndOneManner()
    {
        var prompt = NpcChatter.Instruction(NpcChatterKind.Hail, exchangeIndex: 9);

        Assert.Contains("The speaker's manner: ", prompt, StringComparison.Ordinal);
        Assert.Single(NpcChatter.Manners, manner => prompt.Contains(manner, StringComparison.Ordinal));
    }

    [Fact]
    public void EverySlotAnExchangeCanCastGetsItsOwnManner()
    {
        var most = 2 + NpcChatterRoster.MostMet;

        for (var exchange = 0; exchange < 200; exchange++)
        {
            var manners = NpcChatter.MannersFor(exchange, most);

            Assert.Equal(most, manners.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void AcrossARunOfExchangesTheMannersAreSpreadRatherThanStuck()
    {
        const int exchanges = 240;

        var drawn = Enumerable.Range(0, exchanges)
            .SelectMany(exchange => NpcChatter.MannersFor(exchange, 2))
            .GroupBy(manner => manner, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(NpcChatter.Manners.Count, drawn.Count);

        var even = exchanges * 2 / NpcChatter.Manners.Count;

        Assert.All(drawn.Values, count => Assert.InRange(count, even / 2, even * 2));
    }

    [Fact]
    public void TheSameExchangeDrawsTheSameMannersEveryTime()
    {
        Assert.Equal(NpcChatter.MannersFor(17, 2), NpcChatter.MannersFor(17, 2));
    }
}
