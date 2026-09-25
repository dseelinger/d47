using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>Night vision resolves to the ship binding in the ship and SRV, and the suit binding on foot (#456).</summary>
public sealed class NightVisionWorksInTheShipAndOnFootTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);

    private readonly string _path = Path.Combine(
        Path.GetTempPath(), "d47-night-vision-tests", Guid.NewGuid().ToString("N") + ".binds");

    public NightVisionWorksInTheShipAndOnFootTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        // The Commander's own two bindings, from Custom.4.2.binds.
        File.WriteAllText(_path, """
            <Root PresetName="Custom">
              <NightVisionToggle>
                <Primary Device="Keyboard" Key="Key_V">
                  <Modifier Device="Keyboard" Key="Key_N" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </NightVisionToggle>
              <HumanoidToggleNightVisionButton>
                <Primary Device="Keyboard" Key="Key_N" />
                <Secondary Device="{NoDevice}" Key="" />
              </HumanoidToggleNightVisionButton>
            </Root>
            """);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private static GameAction NightVision =>
        GameActions.Find("night_vision") ?? throw new InvalidOperationException("No night_vision action.");

    private IReadOnlyList<InputStep> Presses(ControlContext context)
    {
        var binds = EliteBinds.Parse(_path, "Custom", NullLogger.Instance);
        var reach = ActionReachability.Resolve(NightVision, binds, context);

        Assert.True(reach.IsOffered, reach.Reason);

        return InputSequence.Tap(reach.Binding!);
    }

    private static uint Code(string key) => EliteKeys.Resolve(key).Code;

    [Theory]
    [InlineData(ControlContext.NormalSpace)]
    [InlineData(ControlContext.Supercruise)]
    [InlineData(ControlContext.Docked)]
    [InlineData(ControlContext.Srv)]
    public void InTheShipAndSrvItPressesVWithNHeld(ControlContext context)
    {
        var keys = Presses(context).Where(step => step.Kind != InputStepKind.Delay).ToArray();

        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, Code("Key_N")),
                new InputStep(InputStepKind.KeyDown, Code("Key_V")),
                new InputStep(InputStepKind.KeyUp, Code("Key_V")),
                new InputStep(InputStepKind.KeyUp, Code("Key_N")),
            ],
            keys);
    }

    [Fact]
    public void OnFootItPressesN()
    {
        var keys = Presses(ControlContext.OnFoot).Where(step => step.Kind != InputStepKind.Delay).ToArray();

        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, Code("Key_N")),
                new InputStep(InputStepKind.KeyUp, Code("Key_N")),
            ],
            keys);
    }

    [Theory]
    [InlineData("night vision", DesiredState.Toggle)]
    [InlineData("night vision on", DesiredState.On)]
    [InlineData("night vision off", DesiredState.Off)]
    public void ItHasTheThreePhrases(string phrase, DesiredState state) =>
        Assert.Contains((phrase, state), NightVision.Phrases);

    [Fact]
    public void NightVisionOnInTheShipWhenItIsAlreadyOnPressesNothing()
    {
        var status = new GameStatus { Flags = StatusFlags.InMainShip | StatusFlags.NightVision, ReadAt = Start };

        Assert.True(NightVision.AlreadyIn(DesiredState.On, status));
        Assert.False(NightVision.AlreadyIn(DesiredState.Off, status));
    }

    [Fact]
    public void OnFootTheFlagIsNotTrusted()
    {
        var status = new GameStatus
        {
            Flags = StatusFlags.None,
            Flags2 = (uint)StatusFlags2.OnFoot,
            ReadAt = Start,
        };

        // Unknown, so "off" presses rather than claiming night vision is already off.
        Assert.Null(NightVision.AlreadyIn(DesiredState.Off, status));
    }
}
