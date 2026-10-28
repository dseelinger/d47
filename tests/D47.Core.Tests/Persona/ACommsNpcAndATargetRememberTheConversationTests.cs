using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A real comms NPC and the pilot of the held target are sent the conversation so far on each turn.</summary>
public class ACommsNpcAndATargetRememberTheConversationTests
{
    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TargetedShip Jessa = new("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Clean");

    private static readonly TargetedShip Orrin = new("Python", "Orrin Vale", "Harmless", "Alliance", "Clean");

    private sealed class Scene
    {
        public string System { get; set; } = "Sol";

        public TargetedShip? Target { get; set; }

        public CommsSelection? Selected { get; set; }

        public NpcsHeardHere Roster { get; } = new();

        public required TurnLoop Loop { get; init; }

        public required FakeLlmProvider Provider { get; init; }

        public void Hear(string name) =>
            Roster.Heard(
                new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", "Their own words.") { Speaker = name, CommsChannel = "npc" },
                null,
                Now);

        public async Task<IReadOnlyList<string>> SayAsync(string input)
        {
            await OnComms.SayAsync(Loop, input);
            return [.. Provider.LastRequest!.Prompt.History.Select(message => $"{message.Role}: {message.Text}")];
        }
    }

    private static Scene Build(string reply)
    {
        var surface = TestSurface.For(new MemoryInstall());
        var provider = FakeLlmProvider.Answering(reply);
        Scene? scene = null;

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            CommsSelected = () => scene!.Selected,
        };

        loop.Lines.AddRange(
        [
            new ChatterLine(() => scene!.System, roster: () => scene!.Roster),
            new TargetLine(() => scene!.Target, roster: () => scene!.Roster),
        ]);

        return scene = new Scene { Loop = loop, Provider = provider };
    }

    [Fact]
    public async Task ACommsNpcIsSentTheFirstTurnOnTheSecond()
    {
        var scene = Build("Keep your distance.");
        scene.Hear("Vik Rane");
        scene.Selected = OnComms.System("Vik Rane");

        Assert.Equal(["User: who are you"], await scene.SayAsync("who are you"));

        Assert.Equal(
            ["User: who are you", "Assistant: Keep your distance.", "User: where are you headed"],
            await scene.SayAsync("where are you headed"));
    }

    [Fact]
    public async Task ATargetPilotIsSentTheFirstTurnOnTheSecondButNotAfterTheTargetChanges()
    {
        var scene = Build("State your business.");
        scene.Target = Jessa;
        scene.Selected = OnComms.Target(Jessa.PilotName!);

        await scene.SayAsync("hello");

        Assert.Equal(
            ["User: hello", "Assistant: State your business.", "User: what do you carry"],
            await scene.SayAsync("what do you carry"));

        scene.Target = Orrin;
        scene.Selected = OnComms.Target(Orrin.PilotName!);

        Assert.Equal(["User: hello"], await scene.SayAsync("hello"));
    }

    [Fact]
    public async Task ATargetPilotStartsAfreshOnceNoOneIsTargeted()
    {
        var scene = Build("State your business.");
        scene.Target = Jessa;
        scene.Selected = OnComms.Target(Jessa.PilotName!);

        await scene.SayAsync("hello");

        scene.Target = null;
        await OnComms.SayAsync(scene.Loop, "anyone there");

        scene.Target = Jessa;

        Assert.Equal(["User: hello"], await scene.SayAsync("hello"));
    }

    [Fact]
    public async Task ACommsNpcWhoSignsOffStartsAfreshWhenHeardAgain()
    {
        var scene = Build("Fine. Got to go. Vik Rane out.");
        scene.Hear("Vik Rane");
        scene.Selected = OnComms.System("Vik Rane");

        await scene.SayAsync("who are you");

        scene.Hear("Vik Rane");

        Assert.Equal(["User: who are you"], await scene.SayAsync("who are you"));
    }

    [Fact]
    public async Task ACommsNpcStartsAfreshInANewSystem()
    {
        var scene = Build("Keep your distance.");
        scene.Hear("Vik Rane");
        scene.Selected = OnComms.System("Vik Rane");

        await scene.SayAsync("who are you");

        scene.System = "Alpha Centauri";
        scene.Roster.EnteredSystem();
        scene.Hear("Vik Rane");

        Assert.Equal(["User: who are you"], await scene.SayAsync("who are you"));
    }

    [Fact]
    public async Task AReplyTheScreenReplacesIsHeldAsTheSignOff()
    {
        var scene = Build("I will scan you right now.");
        scene.Target = Jessa;
        scene.Selected = OnComms.Target(Jessa.PilotName!);

        await scene.SayAsync("who are you");

        Assert.Equal(
            ["User: who are you", "Assistant: " + NpcsHeardHere.SignOff("Jessa Corvane"), "User: again"],
            await scene.SayAsync("again"));
    }
}
