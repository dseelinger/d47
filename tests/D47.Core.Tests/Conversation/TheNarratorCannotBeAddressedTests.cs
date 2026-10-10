using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>"Narrator, ..." reaches no addressed speaker: the Narrator tells the story and takes no questions.</summary>
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
        };

        loop.Lines.Add(new CaptainLine(() => CarrierState.None, () => "Diaguandri", () => "Warden"));
        loop.Lines.Add(new CrewLine(() => null, () => "Vagrant", () => "Warden"));
        loop.Lines.Add(new ChatterLine(() => now, () => "Diaguandri", () => AmbientSituation.Docked, () => "Warden"));

        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync("Narrator, what happens next", cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }
}
