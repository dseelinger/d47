using D47.Core.Capabilities;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A cross-reference in settings help points at a section that exists
/// (https://github.com/dseelinger/d47/issues/65).
/// </summary>
public class HelpCrossReferencesResolveTests
{
    private static IReadOnlyList<CapabilityDescriptor> Registered()
    {
        using var install = new TempInstall();

        return [.. TestSurface.For(install).Registry.All.Select(registered => registered.Descriptor)];
    }

    [Fact]
    public void EveryLinkInSettingsHelpNamesARegisteredCapability()
    {
        var capabilities = Registered();
        var ids = capabilities.Select(capability => capability.Id).ToHashSet(StringComparer.Ordinal);

        var broken = new List<string>();

        foreach (var capability in capabilities)
        {
            foreach (var row in capability.Settings)
            {
                foreach (var target in HelpLinks.TargetsIn(row.Help))
                {
                    if (!ids.Contains(target))
                    {
                        broken.Add($"{capability.Id}.{row.Key} -> ({target})");
                    }
                }
            }
        }

        Assert.True(
            broken.Count == 0,
            "these settings link to a section that is not a registered capability, so the link "
            + "would do nothing when clicked — " + string.Join("; ", broken));
    }

    /// <summary>And the links exist at all.</summary>
    [Fact]
    public void TheCrossReferencesAreStillWrittenAsLinks()
    {
        var linked = Registered()
            .SelectMany(capability => capability.Settings)
            .Count(row => HelpLinks.TargetsIn(row.Help).Count > 0);

        Assert.True(linked >= 4, $"only {linked} settings rows carry a cross-reference link");
    }

    /// <summary>
    /// The plain sentence is what a Commander reads, and it carries no markup — which matters beyond
    /// the settings page: <c>CoverageInventory</c> writes this text into markdown, the row filter
    /// matches against it, and neither should see a target the Commander never does.
    /// </summary>
    [Fact]
    public void ThePlainSentenceHasNoMarkupInIt()
    {
        foreach (var capability in Registered())
        {
            foreach (var row in capability.Settings)
            {
                var plain = HelpLinks.Plain(row.Help);

                Assert.DoesNotContain("](", plain);
            }
        }
    }

    [Theory]
    // Ordinary prose is one segment and comes back unchanged.
    [InlineData("Off by default.", "Off by default.", 0)]
    // The label is what is read; the target never appears.
    [InlineData("see [Privacy](privacy) for what is sent", "see Privacy for what is sent", 1)]
    [InlineData("[Privacy](privacy)", "Privacy", 1)]
    [InlineData("ends with [Privacy](privacy).", "ends with Privacy.", 1)]
    // Two links in one sentence, which nothing writes yet and which must not lose either.
    [InlineData("[A](privacy) and [B](speech)", "A and B", 2)]
    // Not links, and left exactly as written rather than half-eaten.
    [InlineData("a [bracketed] aside", "a [bracketed] aside", 0)]
    [InlineData("an unclosed [ bracket", "an unclosed [ bracket", 0)]
    [InlineData("[label](two words)", "[label](two words)", 0)]
    [InlineData("[](privacy)", "[](privacy)", 0)]
    public void TheParserReadsWhatIsWrittenAndNothingElse(string markup, string plain, int links)
    {
        Assert.Equal(plain, HelpLinks.Plain(markup));
        Assert.Equal(links, HelpLinks.TargetsIn(markup).Count);
    }

    /// <summary>
    /// Offsets into the plain sentence are what the view marks hits at, so the pieces have to
    /// reassemble into exactly it — a segment that dropped or duplicated a character would put every
    /// highlight after it in the wrong place.
    /// </summary>
    [Fact]
    public void TheSegmentsReassembleIntoThePlainSentence()
    {
        const string Markup = "Off by default; see [Privacy](privacy) for what is sent.";

        var rebuilt = string.Concat(HelpLinks.Parse(Markup).Select(segment => segment.Text));

        Assert.Equal(HelpLinks.Plain(Markup), rebuilt);
        Assert.Equal("Off by default; see Privacy for what is sent.", rebuilt);
    }
}
