using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>"Narrator, ..." on the primary key reaches no addressed speaker, with the Narrator selected on comms or not.</summary>
public class TheNarratorCannotBeAddressedTests
{
    [Fact]
    public async Task SayingNarratorFirstDoesNotRouteToASpeaker()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Standing by.");
        var now = new DateTimeOffset(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI.",
            ShipAiNames = () => ["Warden"],
            CommsSelected = () => OnComms.Narrator,
        };

        loop.Lines.Add(new CaptainLine(() => null, () => "Diaguandri", () => now));
        loop.Lines.Add(new CrewLine(() => null, () => "Vagrant"));
        loop.Lines.Add(new ChatterLine(() => "Diaguandri"));
        loop.Lines.Add(new NarratorLine(() => null));

        var events = await OnComms.AskAsync(loop, "Narrator, what happens next");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }
}
