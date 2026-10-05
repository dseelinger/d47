using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The galaxy entry says that following the Commander on the System page sends each arrival (#824).</summary>
public class TheEgressListNamesTheSystemPageTests
{
    private const string Sentence =
        "While the Search tab's System page is open and following you, arriving in a system sends that system's "
        + "address to spansh.co.uk, to read its record.";

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
    public void ItIsNamedWhateverTheCalloutsAre() =>
        Assert.Contains(Sentence, What(GalaxyOn(callouts: false)), StringComparison.Ordinal);
}
