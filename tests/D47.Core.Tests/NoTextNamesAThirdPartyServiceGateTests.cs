using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests;

/// <summary>No string literal in src/ names a third-party service outside the places allowed to.</summary>
[Trait("Category", "Gate")]
public sealed partial class NoTextNamesAThirdPartyServiceGateTests
{
    [GeneratedRegex(@"\b(?:Spansh|Inara|Canonn|Raven Colonial|EDDN|Ardent|EDSM|EDAstro)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ServiceName();

    [Fact]
    public void NoStringLiteralInSourceNamesAThirdPartyServiceOutsideTheAllowedPlaces()
    {
        var source = Path.Combine(RepositoryRoot(), "src");
        var found = new List<string>();

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, path).Replace(Path.DirectorySeparatorChar, '/');
            var parts = relative.Split('/');

            if (parts.Any(part => part is "obj" or "bin" or "vendor") || relative.EndsWith("Configuration/EgressDisclosure.cs", StringComparison.Ordinal))
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

                if (isText && ServiceName().IsMatch(token.ValueText) && !IsAllowed(token, relative))
                {
                    var line = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    found.Add($"{relative}:{line}");
                }
            }
        }

        Assert.True(
            found.Count == 0,
            "These string literals name a third-party service outside Settings rows, the egress disclosure, "
            + "log calls, hosts and the listed exceptions:" + Environment.NewLine
            + string.Join(Environment.NewLine, found));
    }

    private static bool IsAllowed(SyntaxToken token, string relative)
    {
        var text = token.ValueText;

        // A setting key or a file name is an identifier, not text.
        if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || (text.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_') && text.Any(c => c is '.' or '-' or '_')))
        {
            return true;
        }

        // The notes that ColonisationProgress puts on its Settings row.
        if (relative == "D47.Core/Knowledge/ColonisationProgress.cs")
        {
            return true;
        }

        foreach (var node in token.Parent!.Ancestors())
        {
            switch (node)
            {
                case ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax { Identifier.Text: "SettingRow" } }:
                    return true;

                case InvocationExpressionSyntax invocation when InvokedName(invocation).StartsWith("Log", StringComparison.Ordinal):
                    return true;

                // The helper that builds the callout rows on the Settings page.
                case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Toggle" } }
                    when relative == "D47.Core/Capabilities/Builtin/CalloutCapability.cs":
                    return true;

                case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Remove", Expression: IdentifierNameSyntax { Identifier.Text: "secrets" } } }
                    when relative == "D47.App/AppHost.cs":
                    return true;

                case VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax field } } declarator
                    when field.Modifiers.Any(SyntaxKind.ConstKeyword)
                        && (declarator.Identifier.Text is "Host" or "Endpoint" || declarator.Identifier.Text.EndsWith("Schema", StringComparison.Ordinal)):
                    return true;

                // Feeds the ColonisationProgress row note.
                case VariableDeclaratorSyntax { Identifier.Text: "NoKey" } when relative == "D47.Knowledge/RavenColonialPlanWriter.cs":
                    return true;

                // The two browser tooltips.
                case MethodDeclarationSyntax { Identifier.Text: "TitleRow" } when relative == "D47.App/Panel/ConstructionPage.cs":
                    return true;
            }
        }

        return false;
    }

    private static string InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax name => name.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        _ => "",
    };

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
