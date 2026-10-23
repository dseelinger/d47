using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests;

/// <summary>Stored <c>NonRoamableId</c>s are carried to HID identities only where the match is certain (#1513).</summary>
public class AStoredStickIdMovesToItsHidIdentityTests
{
    private const string Old = "{wgi/nrid/one}";

    private static HotasReading Reading(string id, params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = id, VendorId = 0x4098, ProductId = 0xBD65, Buttons = buttons, AxisCount = 7 };
    }

    [Fact]
    public void OneCandidateTakesTheId()
    {
        var moves = StoredIdCarryOver.Plan([Old], [Reading(Old)], [Reading("hid:a:b32")]);

        Assert.Equal([new IdMove(Old, "hid:a:b32")], moves);
    }

    [Fact]
    public void SameShapeCandidatesWithEqualHeldSetsMoveNothing()
    {
        var moves = StoredIdCarryOver.Plan(
            [Old], [Reading(Old, 3)], [Reading("hid:a:b32", 3), Reading("hid:b:b32", 3)]);

        Assert.Empty(moves);
    }

    [Fact]
    public void DistinctHeldSetsMoveTheIdToTheOneThatMatches()
    {
        var moves = StoredIdCarryOver.Plan(
            [Old], [Reading(Old, 5)], [Reading("hid:a:b32", 3), Reading("hid:b:b32", 5), Reading("hid:c:b32", 7)]);

        Assert.Equal([new IdMove(Old, "hid:b:b32")], moves);
    }

    [Fact]
    public void DistinctHeldSetsThatNoneMatchMoveNothing()
    {
        var moves = StoredIdCarryOver.Plan(
            [Old], [Reading(Old, 9)], [Reading("hid:a:b32", 3), Reading("hid:b:b32", 5)]);

        Assert.Empty(moves);
    }

    [Fact]
    public void AnIdTheReaderCannotSeeStaysAsItIs()
    {
        var moves = StoredIdCarryOver.Plan(["{wgi/nrid/other}"], [Reading(Old)], [Reading("hid:a:b32")]);

        Assert.Empty(moves);
    }

    [Fact]
    public void ADifferentShapeIsNotACandidate()
    {
        var other = Reading("hid:a:b32") with { AxisCount = 3 };

        Assert.Empty(StoredIdCarryOver.Plan([Old], [Reading(Old)], [other]));
    }

    [Fact]
    public void EverySwitchOnTheIdMovesWhateverItsDeviceTextAndASecondPlanFindsNothing()
    {
        var positions = new[] { new SwitchPosition(1, "landing_gear", DesiredState.On) };
        var stored = new[]
        {
            new SwitchMapping { Name = "a", DeviceId = Old, Device = "text", Positions = positions },
            new SwitchMapping { Name = "b", DeviceId = Old, Positions = positions },
            new SwitchMapping { Name = "c", DeviceId = "hid:z:b32", Positions = positions },
        };
        var settings = new D47Settings
        {
            Listening = new ListeningSettings { PushToTalkButton = $"{Old}#4" },
            Speech = new SpeechSettings { CancelButton = $"{Old}#7" },
        };

        var moves = StoredIdCarryOver.Plan(
            StoredIdCarryOver.LegacyIds(stored, settings), [Reading(Old)], [Reading("hid:a:b32")]);
        var next = StoredIdCarryOver.Apply(stored, moves);
        var buttons = moves[0].AsFound().RepointButtons(settings, out _);

        Assert.Equal(["hid:a:b32", "hid:a:b32", "hid:z:b32"], next.Select(mapping => mapping.DeviceId));
        Assert.Equal("text", next[0].Device);
        Assert.Equal("hid:a:b32#4", buttons.Listening.PushToTalkButton);
        Assert.Equal("hid:a:b32#7", buttons.Speech.CancelButton);
        Assert.Empty(StoredIdCarryOver.LegacyIds(next, buttons));
    }
}
