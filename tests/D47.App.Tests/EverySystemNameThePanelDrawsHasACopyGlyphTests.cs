using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A member of <c>src/D47.App</c> that draws a system name as text also calls <c>CopyGlyph.</c>,
/// unless it is listed in <see cref="Exclusions"/>. The rule: every system name the panel draws gets
/// a copy glyph, except text in inputs, galaxy-map canvas labels and transcript prose.
/// </summary>
[Trait("Category", "Gate")]
public class EverySystemNameThePanelDrawsHasACopyGlyphTests
{
    private static readonly string[] SystemMembers = ["StarSystem", "SystemName", "System"];

    /// <summary>
    /// The panel's text builders. A new text builder has to be added here, or its system-name
    /// arguments are not seen.
    /// </summary>
    private static readonly string[] TextBuilders =
        ["Value", "Chrome", "Sub", "Mono", "Text", "Ellipsed", "CreditsKit.Line", "TitleText.Build"];

    private static readonly string[] NotDrawing =
        ["Equals", "Contains", "StartsWith", "EndsWith", "Compare", "IndexOf", "SortColumn.Text"];

    /// <summary>Calls that put a copy glyph beside a system name; <c>RoutingKit.Row</c> wraps <c>CopyGlyph.Beside</c>.</summary>
    private static bool CallsGlyph(MemberAccessExpressionSyntax call) =>
        call.Expression.ToString() is "CopyGlyph" or "D47.App.Controls.CopyGlyph"
        || (call.Expression.ToString() == "RoutingKit" && call.Name.Identifier.Text == "Row");

    /// <summary>"File.cs:Member" to the reason the member draws a system name without a glyph.</summary>
    private static readonly Dictionary<string, string> Exclusions = new()
    {
        ["CodexChoosePage.cs:Row"] = "A picker row inside a button; a nested glyph would be a button in a button.",
        ["LoreDialog.cs:AddAsync"] = "A status sentence in the dialog, prose rather than a system label.",
        ["BookmarkFormPage.cs:Refresh"] = "A status line under the Location input naming the system a pick resolved to.",
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void NoMemberDrawsASystemNameWithoutACopyGlyph()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "D47.App");
        var misses = new SortedSet<string>(StringComparer.Ordinal);
        var used = new HashSet<string>();

        foreach (var path in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
            var file = Path.GetFileName(path);

            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (!SystemMembers.Contains(access.Name.Identifier.Text) || !IsDrawn(access))
                {
                    continue;
                }

                var member = access.Ancestors().OfType<MemberDeclarationSyntax>()
                    .First(m => m is not BaseTypeDeclarationSyntax);
                var key = $"{file}:{MemberName(member)}";

                if (member.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(CallsGlyph))
                {
                    continue;
                }

                if (Exclusions.ContainsKey(key))
                {
                    used.Add(key);
                    continue;
                }

                misses.Add(key);
            }
        }

        Assert.True(
            misses.Count == 0,
            "These members draw a system name without calling CopyGlyph. Add the glyph, or list the member in "
            + "Exclusions with its reason (an input, a canvas label, transcript prose). If the text goes through a "
            + "new builder, add the builder to TextBuilders:\n" + string.Join("\n", misses));

        Assert.Empty(Exclusions.Keys.Except(used));
    }

    private static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        PropertyDeclarationSyntax p => p.Identifier.Text,
        _ => member.Kind().ToString(),
    };

    private static bool IsDrawn(SyntaxNode access)
    {
        for (var node = access.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression):
                    return false;
                case InvocationExpressionSyntax i when NotDrawing.Contains(Qualified(i.Expression)):
                    return false;
                case ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax call } }
                    when TextBuilders.Contains(Qualified(call.Expression)):
                    return true;
                case ArgumentSyntax { Parent: ArgumentListSyntax { Parent: ObjectCreationExpressionSyntax create } }
                    when NameOf(create.Type) == "Run":
                    return true;
                case AssignmentExpressionSyntax a when NameOf(a.Left) == "Text":
                    return true;
                case MemberDeclarationSyntax or StatementSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>The callee as <c>Receiver.Method</c> when the receiver is a plain or generic name, else the method name.</summary>
    private static string Qualified(ExpressionSyntax callee) => callee is MemberAccessExpressionSyntax { Expression: var receiver } m
        && NameOf(receiver) is { Length: > 0 } name
        && (name is "CreditsKit" or "TitleText" or "SortColumn" or "RoutingKit")
            ? $"{name}.{m.Name.Identifier.Text}"
            : NameOf(callee);

    private static string NameOf(SyntaxNode node) => node switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        GenericNameSyntax g => g.Identifier.Text,
        _ => string.Empty,
    };
}
