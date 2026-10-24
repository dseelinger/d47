using D47.Core.Characters;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>An unnamed utterance the ship runs without the model goes to the ship AI, and an open line stays open.</summary>
public class AShipCommandPassesAnOpenLineTests
{
    private const string Command = "request docking";

    private static readonly CommanderGameState Commander = new(new CommanderIdentity("F1", "Tester"))
    {
        Carrier = new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
    };

    private static readonly ShipCrew Pilots = new()
    {
        Members = [new CrewMember("Vance", CrewId: 1, CombatRank: "Dangerous", Active: true)],
    };

    private static readonly CustomCharacter Teo = new("0000000a", "Teo Marsh", null, "Science officer.", Aboard: true, System: null);

    private static bool IsCommand(string input) => input == Command;

    private static async Task AssertPassesAndKeepsAsync(ILine line, string named, string question)
    {
        await line.RouteAsync(named, CancellationToken.None);
        Assert.True(line.IsOpen);

        Assert.IsType<LineDecision.NotMine>(await line.RouteAsync(Command, CancellationToken.None));
        Assert.True(line.IsOpen);

        Assert.IsType<LineDecision.Taken>(await line.RouteAsync(question, CancellationToken.None));
    }

    [Fact]
    public Task TheCaptainPassesACommandAndKeepsTheLine() =>
        AssertPassesAndKeepsAsync(
            new CaptainLine(() => Commander, () => "Sol", () => DateTimeOffset.UnixEpoch, modelFree: IsCommand),
            "Captain, status?",
            "how's our fuel?");

    [Fact]
    public Task AHiredPilotPassesACommandAndKeepsTheLine() =>
        AssertPassesAndKeepsAsync(
            new CrewLine(() => Pilots, () => "Warden's Reach", modelFree: IsCommand),
            "Vance, status?",
            "how's our fuel?");

    [Fact]
    public Task ACharacterAboardPassesACommandAndKeepsTheLine() =>
        AssertPassesAndKeepsAsync(
            new CharacterLine(() => [Teo], () => "Sol", modelFree: IsCommand),
            "Teo Marsh, status?",
            "how's our fuel?");

    [Fact]
    public async Task ACommandThatNamesTheCaptainStillGoesToTheCaptain()
    {
        var line = new CaptainLine(() => Commander, () => "Sol", () => DateTimeOffset.UnixEpoch, modelFree: IsCommand);

        await line.RouteAsync("Captain, status?", CancellationToken.None);

        Assert.IsType<LineDecision.Taken>(await line.RouteAsync("Captain, request docking", CancellationToken.None));
    }

    [Fact]
    public async Task ACommandThatNamesAPilotStillGoesToThePilot()
    {
        var line = new CrewLine(() => Pilots, () => "Warden's Reach", modelFree: IsCommand);

        Assert.IsType<LineDecision.Taken>(await line.RouteAsync("Vance, request docking", CancellationToken.None));
    }

    [Fact]
    public async Task ACommandThatNamesACharacterStillGoesToTheCharacter()
    {
        var line = new CharacterLine(() => [Teo], () => "Sol", modelFree: IsCommand);

        Assert.IsType<LineDecision.Taken>(await line.RouteAsync("Teo Marsh, request docking", CancellationToken.None));
    }
}
