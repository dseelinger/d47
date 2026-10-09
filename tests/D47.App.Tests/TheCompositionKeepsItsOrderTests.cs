using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

public sealed class TheCompositionKeepsItsOrderTests
{
    private static readonly string[] Registrations =
    [
        "journal", "journal history", "commander pick", "push-to-talk", "default-audio-devices", "macros",
        "binds", "switches", "reminders", "ships", "own cores", "core absences", "ship cores", "memory", "goals", "adventures",
        "callout-drain", "ambience", "voice-scope", "autonomous-drain", "switch-drain",
    ];

    private static SyntaxNode Start() =>
        AppSource.Methods()
            .Single(method => method.Node.Identifier.ValueText == "Start"
                && AppSource.EnclosingTypeName(method.Node) == "AppHost"
                && method.Parameters.Count == 1)
            .Node;

    private static IEnumerable<InvocationExpressionSyntax> Invocations(SyntaxNode start, string member) =>
        start.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => MemberName(call) == member)
            .OrderBy(call => call.SpanStart);

    private static string? MemberName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        IdentifierNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    private static List<InvocationExpressionSyntax> TickAdds(SyntaxNode start) =>
        [.. Invocations(start, "Add")
            .Where(call => call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "tick" } })
            .Where(call => call.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax)];

    private static int FirstAdd(SyntaxNode start, string name) =>
        TickAdds(start).First(call => Name(call) == name).SpanStart;

    private static string Name(InvocationExpressionSyntax call) =>
        ((LiteralExpressionSyntax)call.ArgumentList.Arguments[0].Expression).Token.ValueText;

    private static int First(SyntaxNode start, string member, Func<InvocationExpressionSyntax, bool>? where = null)
    {
        var found = Invocations(start, member).Where(call => where?.Invoke(call) ?? true).ToList();
        Assert.True(found.Count > 0, $"Start() has no call to {member}.");
        return found[0].SpanStart;
    }

    private static int Subscription(SyntaxNode start, string handler)
    {
        var found = start.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            .Where(assignment => assignment.Right.ToString().Contains(handler, StringComparison.Ordinal))
            .ToList();
        Assert.True(found.Count == 1, $"Expected one += naming {handler} in Start(); found {found.Count}.");
        return found[0].SpanStart;
    }

    private static int PrimingTick(SyntaxNode start)
    {
        var found = Invocations(start, "Tick")
            .Where(call => !call.Ancestors().TakeWhile(node => node != start).OfType<LambdaExpressionSyntax>().Any())
            .ToList();
        Assert.True(found.Count == 1, $"Expected one Tick( outside the tick subscribers; found {found.Count}.");
        return found[0].SpanStart;
    }

    [Fact]
    public void TickSubscribersAreRegisteredInTheOrderTheyRely()
    {
        var actual = TickAdds(Start()).Select(Name).ToList();

        Assert.True(
            Registrations.SequenceEqual(actual),
            "tick.Add registrations in Start() were:\n" + string.Join(", ", actual));
    }

    [Fact]
    public void NoTickSubscriberIsRegisteredOutsideStartExceptTheVrAndOverlayOnes()
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
        var start = Start();

        Assert.True(FirstAdd(start, "commander pick") < PrimingTick(start));
    }

    [Fact]
    public void TheHostIsBuiltAfterThePrimingTick()
    {
        var start = Start();

        Assert.True(PrimingTick(start) < NewAppHost(start));
    }

    [Fact]
    public void ThePersonaIsWatchedBeforeTheLlmSettingsAreApplied()
    {
        var start = Start();

        Assert.True(Subscription(start, "OnPersonaChanged") < First(start, "ApplyLlmSettings"));
    }

    [Fact]
    public void EverySettingIsAppliedBeforeSettingsChangesAreWatched()
    {
        var start = Start();
        var watched = Subscription(start, "OnSettingsChanged");

        foreach (var apply in new[] { "ApplyLlmSettings", "ApplySpeechSettings", "ApplyListeningSettings" })
        {
            Assert.True(First(start, apply) < watched, $"{apply} must come before the OnSettingsChanged subscription.");
        }
    }

    [Fact]
    public void DirectionsBeginBeforeProposalsAreReworded()
    {
        var start = Start();

        Assert.True(First(start, "BeginDirections") < First(start, "RewordProposals"));
    }

    [Fact]
    public void TheTickDriverStartsAfterEveryRegistration()
    {
        var start = Start();
        var driver = start.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Single(creation => creation.Type.ToString() == "TickDriver").SpanStart;

        Assert.True(TickAdds(start).All(call => call.SpanStart < driver));
    }

    private static int NewAppHost(SyntaxNode start) =>
        start.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .First(creation => creation.Type.ToString() == "AppHost").SpanStart;
}
