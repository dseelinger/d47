using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheGalNetFetchIsDisclosedTests
{

    [Fact]
    public void OnADefaultInstallItNamesTheHostAndSaysNothingAboutYouGoesWithIt()
    {
        var settings = new D47Settings();

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.GalNet);

        Assert.True(entry.Active);
        Assert.Equal("cms.zaonce.net", entry.Destination);
        Assert.Contains("Nothing about you goes with it", entry.What, StringComparison.Ordinal);
        Assert.Contains("GalNet news → cms.zaonce.net", EgressDisclosure.Describe(settings, false), StringComparison.Ordinal);
    }
}
