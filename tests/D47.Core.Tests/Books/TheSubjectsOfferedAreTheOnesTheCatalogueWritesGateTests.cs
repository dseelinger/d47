using System.Text.RegularExpressions;
using D47.Core.Books;
using Xunit;

namespace D47.Core.Tests.Books;

/// <summary>
/// The subjects Add a book offers are the ones <c>tools/book-catalogue.py</c> writes into the catalogue, in its order.
/// A subject spelled differently would match no book.
/// </summary>
[Trait("Category", "Gate")]
public sealed partial class TheSubjectsOfferedAreTheOnesTheCatalogueWritesGateTests
{
    [GeneratedRegex(@"^\s*\(""(?<name>[^""]+)"",\s*\(", RegexOptions.Multiline)]
    private static partial Regex SubjectLine();

    [Fact]
    public void EverySubjectOfferedIsOneTheGeneratorWrites()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryFolder.Root(), "tools", "book-catalogue.py"));
        var start = script.IndexOf("SUBJECTS = (", StringComparison.Ordinal);
        var end = script.IndexOf("\n)", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "tools/book-catalogue.py has no SUBJECTS table.");

        var written = SubjectLine().Matches(script[start..end]).Select(match => match.Groups["name"].Value).ToList();

        Assert.Equal(written, GutenbergCatalogue.Subjects);
    }
}
