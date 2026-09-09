using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Saying where the panel goes, end to end and with no model in the path.</summary>
public class MovingThePanelByVoiceTests
{
    /// <summary>What a nudge asked for, so the wire can be checked rather than the arithmetic.</summary>
    private sealed record Moved(VrNudge Nudge, int Steps);

    private sealed record Fixture(CapabilityRegistry Registry, KeywordRouter Router, List<Moved> Nudges);

    private static Fixture Build(VrNudgeOutcome outcome = VrNudgeOutcome.Moved)
    {
        var nudges = new List<Moved>();
        var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            VrCapability.Create(
                settings,
                new VrCapability.HeadsetSurface
                {
                    Report = () => (VrState.Active, null),
                    Nudge = (nudge, steps) =>
                    {
                        nudges.Add(new Moved(nudge, steps));
                        return outcome;
                    },
                }),
        ]);

        return new Fixture(registry, new KeywordRouter(registry), nudges);
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance)
    {
        var match = fixture.Router.MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal("move_headset_panel", match.ToolName);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("move the panel left", VrNudge.Left)]
    [InlineData("move the panel right", VrNudge.Right)]
    [InlineData("raise the panel", VrNudge.Up)]
    [InlineData("lower the panel", VrNudge.Down)]
    [InlineData("move the panel closer", VrNudge.Nearer)]
    [InlineData("bring the panel closer", VrNudge.Nearer)]
    [InlineData("push the panel away", VrNudge.Further)]
    [InlineData("turn the panel left", VrNudge.TurnLeft)]
    [InlineData("tilt the panel up", VrNudge.TiltUp)]
    [InlineData("tilt the panel forward", VrNudge.TiltDown)]
    public async Task ADeclaredPhraseMovesThePanelWithNoModelInThePath(string said, VrNudge expected)
    {
        var fixture = Build();

        var result = await Say(fixture, said);

        Assert.False(result.IsError);
        Assert.Equal(new Moved(expected, 1), Assert.Single(fixture.Nudges));
    }

    /// <summary>Every direction is reachable by voice.</summary>
    [Fact]
    public async Task EveryDirectionHasAtLeastOnePhrase()
    {
        var reached = new HashSet<VrNudge>();

        foreach (var capability in Build().Registry.All)
        {
            foreach (var tool in capability.Descriptor.Tools.Where(t => t.Name == "move_headset_panel"))
            {
                foreach (var command in tool.Commands)
                {
                    var fixture = Build();
                    await Say(fixture, command.Phrase);

                    reached.Add(Assert.Single(fixture.Nudges).Nudge);
                }
            }
        }

        Assert.Equal([.. Enum.GetValues<VrNudge>()], [.. reached.Order()]);
    }

    /// <summary>The whole utterance, not a phrase found inside one.</summary>
    [Fact]
    public void AQuestionAboutMovingThePanelIsNotAnInstructionToMoveIt()
    {
        Assert.Null(Build().Router.MatchToolCommand("can you move the panel left"));
    }

    [Fact]
    public async Task TheModelCanAskForSeveralStepsAtOnce()
    {
        var fixture = Build();

        var result = await fixture.Registry.InvokeAsync(
            "move_headset_panel",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["direction"] = "tilt-up",
                ["steps"] = "4",
            }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(new Moved(VrNudge.TiltUp, 4), Assert.Single(fixture.Nudges));
    }

    /// <summary>
    /// A direction nobody declared is refused before the host sees it, and the refusal says what the
    /// vocabulary is rather than merely that this was not it.
    /// </summary>
    [Fact]
    public async Task AnUnknownDirectionIsRefusedAndTheHeadsetIsNeverAsked()
    {
        var fixture = Build();

        var result = await fixture.Registry.InvokeAsync(
            "move_headset_panel",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["direction"] = "sideways" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(fixture.Nudges);
    }

    /// <summary>What comes back is what happened, not an acknowledgement.</summary>
    [Fact]
    public async Task BeingPutDownFirstIsSaidOutLoud()
    {
        var result = await Say(Build(VrNudgeOutcome.PutDown), "move the panel left");

        Assert.False(result.IsError);
        Assert.Contains("put it down", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoSessionIsSaidRatherThanReportedAsAMove()
    {
        var result = await Say(Build(VrNudgeOutcome.NoHeadset), "move the panel left");

        Assert.Contains("no headset", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
