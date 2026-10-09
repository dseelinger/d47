using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Seats;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Seats;

[Trait("Category", "Integration")]
public class ASeatAnswersToItsNameTests
{
    private const string ShipAi = "Warden";

    private static readonly CrewSeat Teo = new("0000000a", CrewRole.ScienceOfficer, null, "Teo Marsh");

    private static readonly CrewSeat Ines = new("0000000b", CrewRole.Custom, "Cargo master", "Ines Roy");

    private static (TurnLoop Loop, SeatLine Line) Build(
        TestSurface surface,
        ILlmProvider provider,
        Func<IReadOnlyList<CrewSeat>> seats)
    {
        var line = new SeatLine(seats, () => "Warden's Reach", () => "Python", () => null, () => ShipAi);

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI aboard this vessel.",
        };

        loop.Lines.Add(line);

        return (loop, line);
    }

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    [Fact]
    public async Task TheSeatByNameIsAnsweredWithItsBriefAndNoTools()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Nothing unusual on the scanners.");
        var (loop, line) = Build(TestSurface.For(install), provider, () => [Teo, Ines]);

        var events = await RunAsync(loop, "Teo Marsh, what do you make of this system");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Crew, addressed.Role);
        Assert.Equal("Teo Marsh", addressed.Name);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Contains("You are Teo Marsh, the science officer aboard Warden's Reach, a Python", prompt.Persona, StringComparison.Ordinal);
        Assert.Empty(prompt.Tools);
        Assert.True(line.IsOpen);
    }

    [Fact]
    public async Task ACustomSeatIsBriefedWithItsTitle()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Hold is full.");
        var (loop, _) = Build(TestSurface.For(install), provider, () => [Teo, Ines]);

        await RunAsync(loop, "Ines Roy, how is the hold");

        Assert.Contains("the Cargo master aboard", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFollowUpWithNoNameReachesTheSeatUntilDismissed()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider, () => [Teo, Ines]);

        await RunAsync(loop, "Teo Marsh, what do you make of this system");
        var next = await RunAsync(loop, "and the star");

        Assert.Equal("Teo Marsh", Assert.Single(next.OfType<TurnEvent.Addressed>()).Name);

        await RunAsync(loop, "That's all");

        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task NamingTheShipAiClosesTheSeatsLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider, () => [Teo]);

        await RunAsync(loop, "Teo Marsh, what do you make of this system");
        var next = await RunAsync(loop, $"{ShipAi}, summarise that");

        Assert.Empty(next.OfType<TurnEvent.Addressed>());
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task ASeatNotAboardTheShipFlownIsNotTaken()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("I have no crew by that name.");
        var seats = new List<CrewSeat> { Teo };
        var (loop, line) = Build(TestSurface.For(install), provider, () => seats);

        await RunAsync(loop, "Teo Marsh, status");

        seats.Clear();

        var events = await RunAsync(loop, "Teo Marsh, status");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task TheLongerOfTwoOverlappingSeatNamesWins()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye.");
        var shorter = new CrewSeat("0000000c", CrewRole.Helm, null, "Teo");
        var (loop, _) = Build(TestSurface.For(install), provider, () => [shorter, Teo]);

        var events = await RunAsync(loop, "Teo Marsh, status");

        Assert.Equal("Teo Marsh", Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
    }
}
