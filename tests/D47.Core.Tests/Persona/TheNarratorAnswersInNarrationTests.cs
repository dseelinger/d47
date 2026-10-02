using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>What the Commander says within ninety seconds of a narration is answered by the Narrator, in narration.</summary>
public class TheNarratorAnswersInNarrationTests
{
    internal const string ShipAi = "Warden";

    internal const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Narration = "Commander Vale did not know it yet, but the beacon had already seen her.";

    private const string Hidden = "The beacon belongs to the Lantern Society.";

    internal sealed class World
    {
        public DateTimeOffset Now { get; set; } = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

        public List<string> Posted { get; } = [];
    }

    internal static (TurnLoop Loop, NarratorLine Line, World World) Build(TestSurface surface, ILlmProvider provider) =>
        Build(surface.Registry, surface.Router, provider);

    internal static (TurnLoop Loop, NarratorLine Line, World World) Build(
        CapabilityRegistry registry, KeywordRouter router, ILlmProvider provider)
    {
        var world = new World();

        var line = new NarratorLine(
            () => world.Now,
            () => ShipAi,
            input => router.MatchSetting(input) is not null
                     || router.MatchToolCommand(input) is not null
                     || router.Match(input, InputSource.Spoken) is not null
                     || router.Book.Candidates(input, InputSource.Spoken).Count > 0,
            () => Hidden,
            () => "Commander Vale.")
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
            HiddenStory = () => "The ship AI's own reading.",
        };

        loop.Lines.Add(line);
        line.Heard(Narration);

        return (loop, line, world);
    }

    internal static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, InputSource.Spoken, TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    [Fact]
    public async Task WhatHappenedNextIsNarratedWithNoTools()
    {
        using var install = new TempInstall();
        const string reply = "She turned toward the beacon, and the beacon, patient as ever, turned toward her.";
        var provider = FakeLlmProvider.Answering(reply);
        var (loop, _, world) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, "what happened next?");

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
    public async Task AReplyThatSaysYouIsNotSaidAndClosesTheLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("You turned toward the beacon.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, "what happened next?");

        Assert.Equal(string.Empty, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Empty(world.Posted);
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task FourRepliesAndTheNextGoesToTheShip()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("The silence held a moment longer.");
        var (loop, line, _) = Build(TestSurface.For(install), provider);

        for (var reply = 1; reply <= NarratorLine.MostReplies; reply++)
        {
            Assert.Single((await RunAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
        }

        Assert.Contains("last reply", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.False(line.IsOpen);
        Assert.Empty((await RunAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NinetyOneSecondsAfterTheNarrationTheShipAnswers()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);

        world.Now += NarratorLine.Window;
        Assert.True(line.IsOpen);

        world.Now += TimeSpan.FromSeconds(1);

        Assert.Empty((await RunAsync(loop, "what happened next?")).OfType<TurnEvent.Addressed>());
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
