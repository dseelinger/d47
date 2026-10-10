using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Gate")]
public sealed class TheCompositionKeepsItsOrderTests
{
    private static readonly string[] Stages =
    [
        "ComposeFoundations", "ComposeStores", "PrimeGameLoop", "ComposeServices", "ComposeCapabilities",
        "ComposeCrew", "ComposeHost", "RegisterHostTicks", "StartRunning",
    ];

    private static readonly string[] Registrations =
    [
        "journal", "journal history", "commander pick", "push-to-talk", "default-audio-devices", "macros",
        "binds", "switches", "reminders", "ships", "own cores", "core absences", "ship cores", "memory", "goals", "adventures",
        "callout-drain", "ambience", "voice-scope", "autonomous-drain", "switch-drain",
    ];

    private static MethodDeclarationSyntax Start() =>
        AppSource.Methods()
            .Single(method => method.Node.Identifier.ValueText == "Start"
                && AppSource.EnclosingTypeName(method.Node) == "AppHost"
                && method.Parameters.Count == 1)
            .Node;

    private static SyntaxNode Stage(string name) => AppSource.Method(name).Node;

    private static IEnumerable<InvocationExpressionSyntax> Invocations(SyntaxNode scope, string member) =>
        scope.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => MemberName(call) == member)
            .OrderBy(call => call.SpanStart);

    private static string? MemberName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        IdentifierNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    private static List<InvocationExpressionSyntax> TickAdds(SyntaxNode scope) =>
        [.. Invocations(scope, "Add")
            .Where(call => call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "tick" } })
            .Where(call => call.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax)];

    private static int FirstAdd(SyntaxNode scope, string name) =>
        TickAdds(scope).First(call => Name(call) == name).SpanStart;

    private static string Name(InvocationExpressionSyntax call) =>
        ((LiteralExpressionSyntax)call.ArgumentList.Arguments[0].Expression).Token.ValueText;

    private static int First(SyntaxNode scope, string member, Func<InvocationExpressionSyntax, bool>? where = null)
    {
        var found = Invocations(scope, member).Where(call => where?.Invoke(call) ?? true).ToList();
        Assert.True(found.Count > 0, $"No call to {member} where it was expected.");
        return found[0].SpanStart;
    }

    private static int Subscription(SyntaxNode scope, string handler)
    {
        var found = scope.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            .Where(assignment => assignment.Right.ToString().Contains(handler, StringComparison.Ordinal))
            .ToList();
        Assert.True(found.Count == 1, $"Expected one += naming {handler}; found {found.Count}.");
        return found[0].SpanStart;
    }

    private static int PrimingTick(SyntaxNode scope)
    {
        var found = Invocations(scope, "Tick")
            .Where(call => !call.Ancestors().TakeWhile(node => node != scope).OfType<LambdaExpressionSyntax>().Any())
            .ToList();
        Assert.True(found.Count == 1, $"Expected one Tick( outside the tick subscribers; found {found.Count}.");
        return found[0].SpanStart;
    }

    private static int StageCall(string stage) => First(Start(), stage);

    private static List<ObjectCreationExpressionSyntax> Creations(SyntaxNode scope, string type) =>
        [.. scope.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(creation => creation.Type.ToString() == type)];

    [Fact]
    public void StartCallsTheNineStagesInOrderAndNothingElse()
    {
        var start = Start();
        var calls = start.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(MemberName).ToList();

        Assert.True(Stages.SequenceEqual(calls), "Start() calls:\n" + string.Join(", ", calls));

        var statements = start.Body!.Statements;

        Assert.Equal(Stages.Length + 2, statements.Count);
        Assert.Single(Creations(statements[0], "LateBound"));
        Assert.IsType<ReturnStatementSyntax>(statements[^1]);
    }

    [Fact]
    public void TickSubscribersAreRegisteredInTheOrderTheyRely()
    {
        var actual = TickAdds(Stage("PrimeGameLoop")).Concat(TickAdds(Stage("RegisterHostTicks"))).Select(Name).ToList();

        Assert.True(
            Registrations.SequenceEqual(actual),
            "tick.Add registrations in PrimeGameLoop and RegisterHostTicks were:\n" + string.Join(", ", actual));
    }

    [Fact]
    public void NoTickSubscriberIsRegisteredOutsideTheRegisteringStagesExceptTheVrAndOverlayOnes()
    {
        var outside = AppSource.CodeLines("tick.Add(")
            .Where(line => line.File != "AppHost.cs")
            .Select(line => line.Text.Split('"') is [_, var name, ..] ? name : line.Text)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            outside.SequenceEqual(["overlay", "overlay-pages", "vr"]),
            "tick.Add( outside AppHost.cs: " + string.Join(", ", outside));

        var inHost = AppSource.CodeLines("tick.Add(").Count(line => line.File == "AppHost.cs");

        Assert.True(inHost == Registrations.Length, $"Expected {Registrations.Length} tick.Add( lines in AppHost.cs; found {inHost}.");
    }

    [Fact]
    public void TheCommanderIsPickedBeforeThePrimingTick()
    {
        var prime = Stage("PrimeGameLoop");

        Assert.True(FirstAdd(prime, "commander pick") < PrimingTick(prime));
    }

    [Fact]
    public void TheHostIsBuiltAfterThePrimingTick()
    {
        PrimingTick(Stage("PrimeGameLoop"));
        Assert.Single(Creations(Stage("ComposeHost"), "AppHost"));

        Assert.True(StageCall("PrimeGameLoop") < StageCall("ComposeHost"));
    }

    [Fact]
    public void ThePersonaIsWatchedBeforeTheLlmSettingsAreApplied()
    {
        var host = Stage("ComposeHost");

        Assert.True(Subscription(host, "OnPersonaChanged") < First(host, "ApplyLlmSettings"));
    }

    [Fact]
    public void EverySettingIsAppliedBeforeSettingsChangesAreWatched()
    {
        var host = Stage("ComposeHost");
        var watched = Subscription(host, "OnSettingsChanged");

        foreach (var apply in new[] { "ApplyLlmSettings", "ApplySpeechSettings", "ApplyListeningSettings" })
        {
            Assert.True(First(host, apply) < watched, $"{apply} must come before the OnSettingsChanged subscription.");
        }
    }

    [Fact]
    public void DirectionsBeginBeforeProposalsAreReworded()
    {
        var host = Stage("ComposeHost");

        Assert.True(First(host, "BeginDirections") < First(host, "RewordProposals"));
    }

    [Fact]
    public void TheTickDriverStartsAfterEveryRegistration()
    {
        var running = Stage("StartRunning");

        Assert.Single(Creations(running, "TickDriver"));
        Assert.Empty(TickAdds(running));

        Assert.True(StageCall("PrimeGameLoop") < StageCall("StartRunning"));
        Assert.True(StageCall("RegisterHostTicks") < StageCall("StartRunning"));
    }
}
