using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A test class names <c>File</c>, <c>Directory</c>, <c>FileInfo</c>, <c>FileStream</c>, <c>DiskFileSystem</c>,
/// <c>TempInstall</c>, <c>TempFolders</c> or <c>Path.GetTempPath</c> only when the class or the member using it is
/// tagged <c>Integration</c> or <c>Gate</c>, or the type is listed here. Every test project is read.
/// </summary>
[Trait("Category", "Gate")]
public sealed class UnitTestsStayOffTheDiskGateTests
{
    private static readonly HashSet<string> Banned =
    [
        "File", "Directory", "FileInfo", "FileStream", "DiskFileSystem", "TempInstall", "TempFolders",
    ];

    /// <summary>Helper types that write render captures when <c>D47_CAPTURES=1</c>.</summary>
    private static readonly HashSet<string> Helpers =
    [
        "D47.App.Tests.TestSurface",
    ];

    [Fact]
    public void NoUntaggedTestClassNamesTheDisk()
    {
        var found = Survey();

        Assert.True(
            found.Count == 0,
            $"""
             These test members name the disk but are not tagged Integration or Gate. Use MemoryFileSystem, or tag the
             test [Trait("Category", "Integration")].

             {string.Join(Environment.NewLine, found)}
             """);
    }

    private static List<string> Survey()
    {
        var tests = Path.Combine(RepositoryRoot(), "tests");
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var found = new List<string>();

        foreach (var path in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(tests, path);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Any(part => part is "obj" or "bin" or "fixtures"))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path).GetRoot();

            foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var banned = Banned.Contains(name.Identifier.ValueText) && !IsMemberName(name)
                    || name.Identifier.ValueText == "GetTempPath" && name.Parent is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Path" } };

                if (!banned || name.Ancestors().OfType<BaseTypeDeclarationSyntax>().LastOrDefault() is not { } outermost)
                {
                    continue;
                }

                if (Helpers.Contains($"{Namespace(outermost)}.{outermost.Identifier.ValueText}")
                    || name.Ancestors().Any(node => node is MemberDeclarationSyntax member && Tagged(member.AttributeLists)))
                {
                    continue;
                }

                var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                found.Add($"{relative.Replace(Path.DirectorySeparatorChar, '/')}:{line} {name.Identifier.ValueText}");
            }
        }

        return found;
    }

    /// <summary>True for <c>x.File</c> where <c>x</c> is not the <c>System.IO</c> namespace.</summary>
    private static bool IsMemberName(IdentifierNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access
        && access.Name == name
        && access.Expression.ToString() is not ("System.IO" or "IO");

    private static bool Tagged(SyntaxList<AttributeListSyntax> lists) =>
        lists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString() is "Trait" or "Xunit.Trait"
            && attribute.ArgumentList?.Arguments is [{ } first, { } second, ..]
            && first.ToString() == "\"Category\""
            && second.ToString() is "\"Integration\"" or "\"Gate\"");

    private static string Namespace(SyntaxNode node) =>
        node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;

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
