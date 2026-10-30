using System.Text.RegularExpressions;
using D47.Core.Help;
using Xunit;

namespace D47.Core.Tests;

public partial class DocumentationGateTests
{
    private const int MostSectionsInABand = 4;
    private const int LongestIntro = 25;
    private const int LongestBody = 50;
    private const int LongestHeading = 15;

    // Each list names pages that fail its fact today; an entry that stops failing is stale and fails.
    private static readonly HashSet<string> PagesWithoutAHowItWorksBand = new(StringComparer.Ordinal)
    {
        "about",
        "bookmarks",
        "learned-phrases",
    };

    private static readonly HashSet<string> PagesWithALongHowItWorksBand = new(StringComparer.Ordinal)
    {
        "adventures",
        "audio",
        "autonomous-actions",
        "crew",
        "exobiology",
        "flight-controls",
        "general-conversation",
        "general-trade-run",
        "goals",
        "listening",
        "logbook",
        "lore",
        "navigation",
        "on-foot",
        "persona",
        "ships",
        "system-names",
        "vr",
    };

    private static readonly HashSet<string> PagesWhoseHowItWorksBandCarriesAFigure = new(StringComparer.Ordinal)
    {
        "goals",
        "specifications",
    };

    [Trait("Category", "Gate")]
    [Fact]
    public void EveryCapabilityPageHasAHowItWorksBand()
    {
        var without = Registry().All
            .Select(c => c.Descriptor.Id)
            .Where(id => HelpLibrary.For(id) is null);

        AssertMatchesAllowlist("have no How it works band", without, PagesWithoutAHowItWorksBand);
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void EveryHowItWorksBandIsShort()
    {
        var overLimits = BandedPages().Where(a => LengthFaults(a).Count > 0).Select(a => a.CapabilityId);

        AssertMatchesAllowlist("have a How it works band over the length limits", overLimits, PagesWithALongHowItWorksBand);
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void NoHowItWorksBandProseCarriesAFigure()
    {
        var figures = BandedPages().Where(a => Figures(a).Count > 0).Select(a => a.CapabilityId);

        AssertMatchesAllowlist("carry a figure in their How it works band prose", figures, PagesWhoseHowItWorksBandCarriesAFigure);
    }

    [Fact]
    public void ABandBreakingEachLengthLimitIsReported()
    {
        var crowded = Band("one two", string.Concat(Enumerable.Range(0, 5).Select(i => Section($"Step {i}", "A line."))));
        var longIntro = Band(Words(LongestIntro + 1), Section("Step", "A line."));
        var longBody = Band("Short.", Section("Step", Words(LongestBody + 1)));
        var longHeading = Band("Short.", Section(Words(LongestHeading + 1), "A line."));
        var empty = Band("Short.", string.Empty);
        var fine = Band(Words(LongestIntro), string.Concat(Enumerable.Range(0, 4).Select(i =>
            Section(Words(LongestHeading), Words(LongestBody)))));

        Assert.Contains(LengthFaults(crowded), f => f.Contains("sections"));
        Assert.Contains(LengthFaults(longIntro), f => f.Contains("intro"));
        Assert.Contains(LengthFaults(longBody), f => f.Contains("body"));
        Assert.Contains(LengthFaults(longHeading), f => f.Contains("heading"));
        Assert.Contains(LengthFaults(empty), f => f.Contains("sections"));
        Assert.Empty(LengthFaults(fine));
    }

    [Fact]
    public void WordsAreCountedWithoutTheMarkupAroundThem()
    {
        var band = Band("Short.", Section("Step", "Say <code>stop now</code> and <strong>wait here</strong>."));

        Assert.Equal(6, WordCount(band.Sections[0].Body!));
        Assert.Equal(2, WordCount("<code>stop</code> <strong>now</strong>"));
    }

    [Fact]
    public void AFigureInBandProseIsReported()
    {
        foreach (var figure in new[] { "about 40% of calls", "a 120 ms wait", "1,200 jumps", "10,000" })
        {
            Assert.NotEmpty(Figures(Band("Short.", Section("Step", figure))));
            Assert.NotEmpty(Figures(Band(figure, Section("Step", "A line."))));
            Assert.NotEmpty(Figures(Band("Short.", Section(figure, "A line."))));
        }

        Assert.Empty(Figures(Band("Short.", Section("Step 12", "Pick one of 12 rows, then 1, 2, 3."))));
    }

    private static IEnumerable<HelpArticle> BandedPages() =>
        HelpLibrary.Pages
            .Select(id => HelpLibrary.For(id))
            .OfType<HelpArticle>();

    private static List<string> LengthFaults(HelpArticle band)
    {
        var faults = new List<string>();

        if (band.Sections.Count is < 1 or > MostSectionsInABand)
        {
            faults.Add($"{band.Sections.Count} sections");
        }

        if (WordCount(band.Intro) > LongestIntro)
        {
            faults.Add($"intro of {WordCount(band.Intro)} words");
        }

        foreach (var section in band.Sections)
        {
            if (WordCount(section.Heading) > LongestHeading)
            {
                faults.Add($"heading '{section.Heading}' of {WordCount(section.Heading)} words");
            }

            if (WordCount(section.Body ?? string.Empty) > LongestBody)
            {
                faults.Add($"body of {WordCount(section.Body!)} words under '{section.Heading}'");
            }
        }

        return faults;
    }

    private static List<string> Figures(HelpArticle band) =>
        band.Sections
            .SelectMany(s => new[] { s.Heading, s.Body ?? string.Empty })
            .Prepend(band.Intro)
            .SelectMany(text => FigurePattern().Matches(text).Select(m => m.Value))
            .ToList();

    private static int WordCount(string text) =>
        Regex.Matches(MarkupPattern().Replace(text, " "), @"\S+").Count;

    private static void AssertMatchesAllowlist(string what, IEnumerable<string> failing, HashSet<string> allowed)
    {
        var failures = failing.ToHashSet(StringComparer.Ordinal);
        var unexpected = failures.Except(allowed).Order(StringComparer.Ordinal).ToArray();
        var stale = allowed.Except(failures).Order(StringComparer.Ordinal).ToArray();

        Assert.True(unexpected.Length == 0, $"Pages that {what}: {string.Join(", ", unexpected)}");
        Assert.True(stale.Length == 0, $"Allowlist entries that no longer fail ({what}): {string.Join(", ", stale)}");
    }

    private static HelpArticle Band(string intro, string sections) =>
        HelpLibrary.Parse(
            $"""<div class="d47-eli5"><div class="d47-frame"><p class="intro">{intro}</p>{sections}</div></div>""",
            "planted")!;

    private static string Section(string heading, string body) =>
        $"""<section><h2><span class="num">1</span> {heading}</h2><p class="body">{body}</p></section>""";

    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", count));

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex MarkupPattern();

    [GeneratedRegex(@"%|\d{3,}|\d{1,3}(?:,\d{3})+")]
    private static partial Regex FigurePattern();
}
