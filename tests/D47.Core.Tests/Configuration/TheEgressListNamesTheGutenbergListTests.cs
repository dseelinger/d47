using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesTheGutenbergListTests
{
    private static EgressEntry Books() =>
        Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.Books);

    [Fact]
    public void ItIsActiveOnDefaultSettingsAndNamesGitHubAndTheRelease()
    {
        var entry = Books();

        Assert.True(entry.Active);
        Assert.StartsWith("github.com", entry.Destination, StringComparison.Ordinal);
        Assert.Contains("books-1", entry.What, StringComparison.Ordinal);
        Assert.Contains("more than 7 days old", entry.What, StringComparison.Ordinal);
        Assert.Contains("Nothing about you goes with it", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void ItNamesTheMirrorABooksTextComesFrom()
    {
        var entry = Books();

        Assert.Equal("github.com, gutenberg.pglaf.org", entry.Destination);
        Assert.Contains(
            "A book's text, from gutenberg.pglaf.org, Project Gutenberg's own server for programs, when you add it",
            entry.What,
            StringComparison.Ordinal);
        Assert.Contains("No book text is sent anywhere to be spoken.", entry.What, StringComparison.Ordinal);
        Assert.Contains("and a book's text when you add it", entry.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("www.gutenberg.org", entry.Destination, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDataEgressNamesTheMirror() =>
        Assert.Contains("gutenberg.pglaf.org", EgressDisclosure.Describe(new D47Settings(), llmKeyPresent: false), StringComparison.Ordinal);
}
