using D47.Core.Seats;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests.Seats;

/// <summary>Every callout id under <c>src/D47.Core</c> is given a crew role or kept by the core, read with the compiler.</summary>
[Trait("Category", "Gate")]
public class EveryCalloutHasAnOwnerTests
{
    [Trait("Category", "Gate")]
    [Fact]
    public void EveryCalloutIdIsInTheRoleTableOrKept()
    {
        var ids = CalloutIds().ToList();

        Assert.Contains(ids, id => id.Id == "route");
        Assert.Contains(ids, id => id.Id == "danger");

        var unowned = ids
            .Where(id => !CrewDomains.Roles.ContainsKey(id.Id) && !CrewDomains.Kept.Contains(id.Id))
            .Select(id => $"{id.Where}: \"{id.Id}\"")
            .ToList();

        Assert.True(
            unowned.Count == 0,
            $"These callout ids are in neither CrewDomains.Roles nor CrewDomains.Kept:{Environment.NewLine}{string.Join(Environment.NewLine, unowned)}");
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void EveryOwnedIdIsARealCalloutAndOwnedOnce()
    {
        var real = CalloutIds().Select(id => id.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(CrewDomains.Roles.Keys.Concat(CrewDomains.Kept), id => Assert.Contains(id, real));
        Assert.All(CrewDomains.Kept, id => Assert.False(CrewDomains.Roles.ContainsKey(id), $"\"{id}\" is both kept and given a role."));
    }

    [Fact]
    public void NoCalloutIsGivenToACustomSeat()
    {
        Assert.DoesNotContain(CrewRole.Custom, CrewDomains.Roles.Values);
    }

    private sealed record CalloutId(string Id, string Where);

    private static IEnumerable<CalloutId> CalloutIds()
    {
        var root = RepositoryRoot();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "D47.Core"), "*.cs", SearchOption.AllDirectories))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

            foreach (var type in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (type.BaseList?.Types.Any(listed => Named(listed.Type) == "ICallout") != true)
                {
                    continue;
                }

                foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (property.Identifier.Text == "Id"
                        && property.ExpressionBody?.Expression is LiteralExpressionSyntax literal
                        && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        yield return new CalloutId(literal.Token.ValueText, $"{Path.GetRelativePath(root, file)}: {type.Identifier.Text}");
                    }
                }
            }
        }
    }

    private static string Named(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => type.ToString(),
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
