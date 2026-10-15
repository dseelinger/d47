using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests;

/// <summary>No string literal in src/ calls the core the ship's AI.</summary>
[Trait("Category", "Gate")]
public sealed partial class NoTextCallsTheCoreAShipAiGateTests
{
    [GeneratedRegex(@"\bship(?:['’]s)? AI\b", RegexOptions.IgnoreCase)]
    private static partial Regex ShipAi();

    [Fact]
    public void NoStringLiteralInSourceCallsTheCoreTheShipsAi()
    {
        var source = Path.Combine(RepositoryRoot(), "src");
        var found = new List<string>();

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, path);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Any(part => part is "obj" or "bin" or "vendor"))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview), path, cancellationToken: TestContext.Current.CancellationToken);

            foreach (var token in tree.GetRoot(TestContext.Current.CancellationToken).DescendantTokens())
            {
                var isText = token.Parent switch
                {
                    LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
                    InterpolatedStringTextSyntax => true,
                    _ => false,
                };

                if (isText && ShipAi().IsMatch(token.ValueText))
                {
                    var line = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    found.Add($"{relative.Replace('\\', '/')}:{line}");
                }
            }
        }

        Assert.True(
            found.Count == 0,
            "These string literals call the core the ship's AI; say \"core\":" + Environment.NewLine
            + string.Join(Environment.NewLine, found));
    }

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
