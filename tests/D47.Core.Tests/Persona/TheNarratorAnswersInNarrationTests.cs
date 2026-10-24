using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>What the Commander says on comms while the STORY channel selects the Narrator is answered in narration.</summary>
public class TheNarratorAnswersInNarrationTests
{
    internal const string ShipAi = "Warden";

    internal const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Narration = "Commander Vale did not know it yet, but the beacon had already seen her.";

    private const string Hidden = "The beacon belongs to the Lantern Society.";

    internal sealed class World
    {
        public List<string> Posted { get; } = [];
    }

    internal static (TurnLoop Loop, NarratorLine Line, World World) Build(TestSurface surface, ILlmProvider provider) =>
        Build(surface.Registry, surface.Router, provider);

    internal static (TurnLoop Loop, NarratorLine Line, World World) Build(
        CapabilityRegistry registry, KeywordRouter router, ILlmProvider provider)
    {
        var world = new World();

        var line = new NarratorLine(() => Hidden, () => "Commander Vale.")
        {
            Said = world.Posted.Add,
        };

        var loop = new TurnLoop(
            registry,
            router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = ShipAiBrief,
            ShipAiNames = () => [ShipAi],
            HiddenStory = () => "The ship AI's own reading.",
            CommsSelected = () => OnComms.Narrator,
        };

        loop.Lines.Add(line);
        line.Heard(Narration);

        return (loop, line, world);
    }

    [Fact]
    public async Task WhatHappenedNextIsNarratedWithNoTools()
    {
        var install = new MemoryInstall();
        const string reply = "She turned toward the beacon, and the beacon, patient as ever, turned toward her.";
        var provider = FakeLlmProvider.Answering(reply);
        var (loop, _, world) = Build(TestSurface.For(install), provider);

        var events = await OnComms.SayAsync(loop, "what happened next?");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Narrator, addressed.Role);
        Assert.Equal(NarratorLine.Name, addressed.Name);
        Assert.Equal(reply, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Empty(prompt.Tools);
        Assert.DoesNotContain(ShipAiBrief, prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("third person", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("Never address the Commander as \"you\"", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"Narrator: {Narration}", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("Commander: what happened next?", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("Commander Vale.", prompt.Persona, StringComparison.Ordinal);
        Assert.Equal(Hidden, prompt.HiddenStory);
        Assert.Equal([reply], world.Posted);
    }

    [Fact]
    public async Task AReplyThatSaysYouIsNotSaidAndTheNextIsStillTheNarrators()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("You turned toward the beacon.");
        var (loop, _, world) = Build(TestSurface.For(install), provider);

        var events = await OnComms.SayAsync(loop, "what happened next?");

        Assert.Equal(string.Empty, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Empty(world.Posted);
        Assert.Single((await OnComms.SayAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task AFifthReplyIsNarratedToo()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("The silence held a moment longer.");
        var (loop, _, _) = Build(TestSurface.For(install), provider);

        for (var reply = 1; reply <= 5; reply++)
        {
            Assert.Single((await OnComms.SayAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
        }

        Assert.DoesNotContain("last reply", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePrimaryKeyReachesTheShipRightAfterANarration()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, _, _) = Build(TestSurface.For(install), provider);

        Assert.Empty((await OnComms.AskAsync(loop, "what happened next?")).OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("She turned toward the beacon.", false)]
    [InlineData("You turned toward the beacon.", true)]
    [InlineData("Your hands were steady.", true)]
    [InlineData("Youthful hands were steady.", false)]
    public void SecondPersonIsDetected(string reply, bool addresses) =>
        Assert.Equal(addresses, NarratorLine.AddressesTheCommander(reply));
}
