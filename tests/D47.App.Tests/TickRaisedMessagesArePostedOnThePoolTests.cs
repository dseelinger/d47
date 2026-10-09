using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <c>MessageStore.Post</c> writes files, so a handler raised on the tick posts from inside a <c>Task.Run</c> (#911).
/// </summary>
[Trait("Category", "Gate")]
public sealed class TickRaisedMessagesArePostedOnThePoolTests
{
    [Theory]
    [InlineData("OnCoresWoke")]
    [InlineData("PostVoicesNotReady")]
    public void APostFromATickHandlerRunsInsideTaskRun(string method)
    {
        var declaration = AppSource.Method(method);

        var posts = declaration.Node.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CalledName(invocation) == "Post")
            .ToList();

        Assert.NotEmpty(posts);

        var onTheCallingThread = posts
            .Where(post => !post.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any(IsTaskRunArgument))
            .Select(post => $"{declaration.File.Name}:{post.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
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
}
