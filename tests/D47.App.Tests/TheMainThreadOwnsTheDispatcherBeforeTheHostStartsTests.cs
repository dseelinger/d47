using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Gate")]
public sealed class TheMainThreadOwnsTheDispatcherBeforeTheHostStartsTests
{
    [Fact]
    public void MainClaimsTheDispatcherBeforeTheHostStarts()
    {
        var body = AppSource.Method("Program.Main").Node.Body!;

        var claim = body.Statements
            .FirstOrDefault(statement => statement.ToString().Contains("Dispatcher.UIThread", StringComparison.Ordinal));

        var start = body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(call => call.Expression.ToString() == "AppHost.Start");

        Assert.True(start is not null, "Program.Main no longer calls AppHost.Start.");

        Assert.True(
            claim is not null && claim.SpanStart < start.SpanStart,
            "Program.Main must read Dispatcher.UIThread in a statement of its own before AppHost.Start, so the main "
            + "thread owns the dispatcher before the host's threads can post to it.");
    }
}
