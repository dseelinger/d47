using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The galaxy entry says what the Galaxy map sends while it is open, and only while galaxy search is on (#1409).</summary>
public class TheEgressListNamesTheGalaxyMapTests
{
    private const string Sentence =
        "While the Navigation tab's Galaxy map page is open, opening it and arriving in a system send that system's "
        + "name to spansh.co.uk, to ask for the hundred systems nearest it; and opening it sends the names of systems "
        + "holding your bookmarks, your carrier or your stored ships that you have never visited, one at a time, to "
        + "find where they are.";

    private static EgressEntry Entry(bool galaxySearch)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.GalaxySearch,
            settings with { Knowledge = settings.Knowledge with { GalaxySearch = galaxySearch } },
            llmKeyPresent: false);
    }

    [Fact]
    public void WithGalaxySearchOnTheEntryNamesIt() =>
        Assert.Contains(Sentence, Entry(galaxySearch: true).What, StringComparison.Ordinal);

    [Fact]
    public void ItComesAfterTheSystemPages()
    {
        var what = Entry(galaxySearch: true).What;

        Assert.True(
            what.IndexOf("Search tab's System page", StringComparison.Ordinal) < what.IndexOf(Sentence, StringComparison.Ordinal));
    }

    [Fact]
    public void WithGalaxySearchOffItIsNotNamed() =>
        Assert.DoesNotContain("Galaxy map", Entry(galaxySearch: false).What, StringComparison.Ordinal);
}
