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
public class SeatsAreNotHiredPilotsTests
{
    private const string ShipAi = "Warden";

    private static readonly CrewSeat Teo = new("0000000a", CrewRole.ScienceOfficer, null, "Teo Marsh");

    private static readonly ShipCrew Pilots = new()
    {
        Members = [new CrewMember("Vance", CrewId: 1, CombatRank: "Dangerous", Active: true)],
    };

    private static TurnLoop Build(TestSurface surface, ILlmProvider provider, params CrewSeat[] seats)
    {
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

        IReadOnlyList<CrewSeat> aboard = seats;

        loop.Lines.Add(new CrewLine(() => Pilots, () => "Warden's Reach", () => ShipAi, () => aboard));
        loop.Lines.Add(new SeatLine(() => aboard, () => "Warden's Reach", () => "Python", () => Pilots, () => ShipAi));

        return loop;
    }

    private static async Task<string> NameOfAsync(TurnLoop loop, string input)
    {
        var name = "";

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Addressed addressed)
            {
                name = addressed.Name;
            }
        }

        return name;
    }

    [Fact]
    public async Task APilotAndASeatOnTheSameShipAreEachReachedByTheirOwnName()
    {
        using var install = new TempInstall();
        var loop = Build(TestSurface.For(install), FakeLlmProvider.Answering("Aye."), Teo);

        Assert.Equal("Vance", await NameOfAsync(loop, "Vance, how is the fighter"));
        Assert.Equal("Teo Marsh", await NameOfAsync(loop, "Teo Marsh, what do you make of this system"));
        Assert.Equal("Vance", await NameOfAsync(loop, "Vance, and the fighter now"));
    }

    [Fact]
    public async Task ASeatNamedLikeAPilotIsReachedAsThePilot()
    {
        using var install = new TempInstall();
        var twin = new CrewSeat("0000000d", CrewRole.Helm, null, "Vance");
        var loop = Build(TestSurface.For(install), FakeLlmProvider.Answering("Aye."), twin);

        Assert.Equal("Vance", await NameOfAsync(loop, "Vance, report"));
    }

    [Fact]
    public async Task ASeatsBriefOffersNoToolsAndIsNotAPilotsBrief()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye.");
        var loop = Build(TestSurface.For(install), provider, Teo);

        await NameOfAsync(loop, "Teo Marsh, what do you make of this system");

        var prompt = provider.LastRequest!.Prompt;
        Assert.Empty(prompt.Tools);
        Assert.DoesNotContain("hired", prompt.Persona, StringComparison.Ordinal);
    }
}
