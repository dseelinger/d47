using System.Text.RegularExpressions;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// Journal files are ordered by <c>JournalFolder.Ordered</c>; a sort by file name folds the hour a zone
/// repeats in the wrong order.
/// </summary>
[Trait("Category", "Gate")]
public sealed partial class EveryJournalFolderIsOrderedByUtcStartGateTests
{
    [Fact]
    public void NoSourceFileSortsJournalFilesByName()
    {
        var source = Path.Combine(RepositoryRoot(), "src");
        var offenders = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && Path.GetFileName(path) != "JournalFolder.cs")
            .Where(path => SortedByName().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(source, path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These files sort the journal folder by name; call JournalFolder.Ordered:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void TheGateRecognisesASortByName()
    {
        const string Sample = "files.Enumerate(directory, JournalFolder.FilePattern)\n    .Where(x => x)\n    .OrderBy(Path.GetFileName, StringComparer.Ordinal)";

        Assert.Matches(SortedByName(), Sample);
        Assert.DoesNotMatch(SortedByName(), "JournalFolder.Ordered(files, directory);\nvar b = list.OrderBy(Path.GetFileName);");
    }

    [GeneratedRegex(@"Enumerate(?:Files)?\([^;]*?JournalFolder\.FilePattern\)[^;]*?OrderBy\(\s*Path\.GetFileName", RegexOptions.Singleline)]
    private static partial Regex SortedByName();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }
}
