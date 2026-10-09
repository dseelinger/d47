using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A provider that wraps another must declare <c>StreamAsync</c>; the interface default would synthesise whole
/// clips through it and silently stop speech from playing as it arrives.
/// </summary>
[Trait("Category", "Gate")]
public sealed class ADecoratorForwardsTheStreamGateTests
{
    [Fact]
    public void EveryProviderThatWrapsAProviderDeclaresStreamAsync()
    {
        var decorators = Decorators().ToList();

        Assert.Contains(decorators, decorator => decorator.Name == "MeteredTtsProvider");
        Assert.Contains(decorators, decorator => decorator.Name == "FallingBackTtsProvider");

        var missing = decorators.Where(decorator => !decorator.Streams).Select(decorator => decorator.Where).ToList();

        Assert.True(
            missing.Count == 0,
            $"These ITtsProvider decorators do not declare StreamAsync:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    private sealed record Decorator(string Name, string Where, bool Streams);

    private static IEnumerable<Decorator> Decorators()
    {
        var root = RepositoryRoot();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

            foreach (var type in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (type.BaseList?.Types.Any(listed => Named(listed.Type) == "ITtsProvider") != true)
                {
                    continue;
                }

                var parameters = (type.ParameterList?.Parameters ?? [])
                    .Concat(type.Members.OfType<ConstructorDeclarationSyntax>().SelectMany(ctor => ctor.ParameterList.Parameters));

                if (!parameters.Any(parameter => parameter.Type is { } declared && Named(declared) == "ITtsProvider"))
                {
                    continue;
                }

                var streams = type.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.Text == "StreamAsync");

                yield return new Decorator(type.Identifier.Text, $"{Path.GetRelativePath(root, file)}: {type.Identifier.Text}", streams);
            }
        }
    }

    private static string Named(TypeSyntax type) => type switch
    {
        NullableTypeSyntax nullable => Named(nullable.ElementType),
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
