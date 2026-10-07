using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>MessageStore.Post</c> writes files, so a handler raised on the tick posts from inside a <c>Task.Run</c> (#911).
/// </summary>
public sealed class TickRaisedMessagesArePostedOnThePoolTests
{
    [Theory]
    [InlineData("OnCoresWoke")]
    [InlineData("PostVoicesNotReady")]
    public void APostFromATickHandlerRunsInsideTaskRun(string method)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "D47.App", "AppHost.cs");
        var tree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(path), path: path, cancellationToken: TestContext.Current.CancellationToken);

        var declaration = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == method);

        var posts = declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CalledName(invocation) == "Post")
            .ToList();

        Assert.NotEmpty(posts);

        var onTheCallingThread = posts
            .Where(post => !post.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any(IsTaskRunArgument))
            .Select(post => $"AppHost.cs:{post.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
            .ToList();

        Assert.True(
            onTheCallingThread.Count == 0,
            $"{method} calls Post on the thread that raised it:\n" + string.Join(Environment.NewLine, onTheCallingThread));
    }

    private static string? CalledName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => null,
    };

    private static bool IsTaskRunArgument(AnonymousFunctionExpressionSyntax lambda) =>
        lambda.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax run }
        && run.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "Task" },
            Name.Identifier.ValueText: "Run",
        };

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
