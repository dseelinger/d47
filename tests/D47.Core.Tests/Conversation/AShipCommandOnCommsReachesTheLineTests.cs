using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A command the router would run is said to the selected party when it is spoken on comms, and presses nothing.</summary>
public class AShipCommandOnCommsReachesTheLineTests
{
    [Fact]
    public async Task OnCommsTheCaptainHearsTheCommand()
    {
        var (loop, _, provider, input) = TheShipAisNameReachesTheShipTests.Build();
        loop.CommsSelected = () => OnComms.Captain;

        var events = await OnComms.SayAsync(loop, "request docking");

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(events.OfType<TurnEvent.Addressed>()).Role);
        Assert.Equal(TurnRoute.Model, TheShipAisNameReachesTheShipTests.Result(events).Route);
        Assert.DoesNotContain(input.Steps, step => step.Kind == InputStepKind.KeyDown);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task OnThePrimaryKeyTheSameCommandRuns()
    {
        var (loop, _, _, input) = TheShipAisNameReachesTheShipTests.Build();
        loop.CommsSelected = () => OnComms.Captain;

        var events = await OnComms.AskAsync(loop, "request docking");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.ActionCommand, TheShipAisNameReachesTheShipTests.Result(events).Route);
        Assert.Contains(input.Steps, step => step.Kind == InputStepKind.KeyDown);
    }
}
