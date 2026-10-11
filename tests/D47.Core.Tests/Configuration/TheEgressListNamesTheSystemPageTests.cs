using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The galaxy entry says that following the Commander on the System page sends each arrival (#824), and that
/// typing into a field that suggests system names sends the letters (#1087).
/// </summary>
public class TheEgressListNamesTheSystemPageTests
{
    private const string Sentence =
        "While the Search tab's System page is open and following you, arriving in a system sends that system's "
        + "address to spansh.co.uk, to read its record.";

    private const string Typing =
        "Typing three or more letters into a field that suggests system names sends those letters to "
        + "spansh.co.uk as you type.";

    private static string What(D47Settings settings) =>
        EgressDisclosure.Entry(EgressDisclosure.GalaxySearch, settings, llmKeyPresent: false).What;

    private static D47Settings GalaxyOn(bool callouts)
    {
        var settings = new D47Settings();

        return settings with
        {
            Knowledge = settings.Knowledge with { GalaxySearch = true },
            Callouts = settings.Callouts with { Enabled = callouts },
        };
    }

    [Fact]
    public void TheGalaxyEntryNamesIt() =>
        Assert.Contains(Sentence, What(GalaxyOn(callouts: true)), StringComparison.Ordinal);

    [Fact]
    public void TheGalaxyEntryNamesTheLettersSentAsYouType() =>
        Assert.Contains(Typing, What(GalaxyOn(callouts: true)), StringComparison.Ordinal);

    [Fact]
    public void WithGalaxySearchOffNothingIsSentAsYouType()
    {
        var settings = new D47Settings();

        Assert.DoesNotContain(Typing, What(settings with { Knowledge = settings.Knowledge with { GalaxySearch = false } }), StringComparison.Ordinal);
    }

    [Fact]
    public void ItIsNamedWhateverTheCalloutsAre() =>
        Assert.Contains(Sentence, What(GalaxyOn(callouts: false)), StringComparison.Ordinal);
}
