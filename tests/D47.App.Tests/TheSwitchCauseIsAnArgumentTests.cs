using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>The cause of a persona switch travels as an argument, never as shared state.</summary>
[Trait("Category", "Gate")]
public class TheSwitchCauseIsAnArgumentTests
{
    [Fact]
    public void NoCodeLineKeepsTheCauseInAField()
    {
        Assert.Empty(AppSource.CodeLines("_personaCause"));
    }

    [Fact]
    public void EveryApplyPersonaSettingsCallPassesACause()
    {
        var calls = AppSource.Files
            .SelectMany(file => file.Tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Where(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "ApplyPersonaSettings" })
            .ToList();

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.Single(call.ArgumentList.Arguments));
    }

    [Fact]
    public void TheSettingsFanoutSkipsPersonaWritesMadeByTheShipBinding()
    {
        Assert.Contains("SettingsCaller.ShipBinding", AppSource.Method("AppHost.OnSettingsChanged").Text, StringComparison.Ordinal);
    }
}
