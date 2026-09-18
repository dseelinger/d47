using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>The gate for #274: every dropdown is a segment or a stepper, and none comes back.</summary>
public sealed class NothingInTheAppConstructsAComboBoxTests
{
    [Fact]
    public void NoCSharpFileNamesAComboBox()
    {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "src", "D47.App");
        var sightings = new List<string>();

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var tree = CSharpSyntaxTree.ParseText(
                File.ReadAllText(path), path: path, cancellationToken: TestContext.Current.CancellationToken);

            var syntaxRoot = tree.GetRoot(TestContext.Current.CancellationToken);

            foreach (var name in syntaxRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (name.Identifier.ValueText == "ComboBox")
                {
                    var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    sightings.Add($"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{line}");
                }
            }
        }

        Assert.True(
            sightings.Count == 0,
            "ComboBox is named where a segment or a stepper should be (#274):\n"
            + string.Join(Environment.NewLine, sightings));
    }

    [Fact]
    public void NoMarkupFileDrawsAComboBox()
    {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "src", "D47.App");
        var tag = new Regex(@"[<:]ComboBox\b", RegexOptions.Compiled);
        var comment = new Regex(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
        var sightings = new List<string>();

        foreach (var path in Directory.EnumerateFiles(source, "*.axaml", SearchOption.AllDirectories))
        {
            var text = comment.Replace(File.ReadAllText(path), string.Empty);
            var lines = text.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (tag.IsMatch(lines[i]))
                {
                    sightings.Add($"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{i + 1}");
                }
            }
        }

        Assert.True(
            sightings.Count == 0,
            "ComboBox is drawn where a segment or a stepper should be (#274):\n"
            + string.Join(Environment.NewLine, sightings));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
