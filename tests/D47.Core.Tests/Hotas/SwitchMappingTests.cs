using D47.Core.Hotas;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>
/// What a stored switch is allowed to say, and how a reading is turned into a position
/// (list.md Phase 21).
/// </summary>
public class SwitchMappingTests
{
    private static HotasReading Held(params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = "d", Buttons = buttons };
    }

    private static SwitchMapping Mapping(params SwitchPosition[] positions) => new()
    {
        Name = "gear switch",
        DeviceId = "d",
        Positions = positions,
    };

    [Fact]
    public void EveryAssignableActionIsOneEliteReportsTheStateOf()
    {
        // The rule that keeps a switch from being a press. An action with no reported flag cannot
        // be asked "are you already there", so a switch bound to it could only toggle.
        Assert.NotEmpty(SwitchValidation.Assignable);
        Assert.All(SwitchValidation.Assignable, action => Assert.NotNull(action.Reports));
    }

    [Fact]
    public void AnActionEliteDoesNotReportIsRefusedWithTheReasonRatherThanAccepted()
    {
        // "boost" is real, bindable, and reports nothing. A switch meaning it could only press it.
        var problem = SwitchValidation.Problem(
            Mapping(new SwitchPosition(8, "boost"), new SwitchPosition(9)));

        Assert.NotNull(problem);
        Assert.Contains("does not report the state", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void APositionThatTogglesIsRefused()
    {
        var problem = SwitchValidation.Problem(
            Mapping(
                new SwitchPosition(8, "landing_gear", DesiredState.Toggle),
                new SwitchPosition(9, "landing_gear", DesiredState.Off)));

        Assert.NotNull(problem);
        Assert.Contains("would be a press", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoPositionsReadingTheSameButtonAreRefused()
    {
        // Indistinguishable at reconcile time, so one of them would silently never happen.
        var problem = SwitchValidation.Problem(
            Mapping(
                new SwitchPosition(8, "landing_gear", DesiredState.On),
                new SwitchPosition(8, "landing_gear", DesiredState.Off)));

        Assert.NotNull(problem);
        Assert.Contains("same button", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ASwitchWithOnePositionIsRefused()
    {
        Assert.NotNull(SwitchValidation.Problem(Mapping(new SwitchPosition(8, "landing_gear"))));
    }

    [Fact]
    public void AnOrdinaryTwoPositionSwitchIsAccepted()
    {
        Assert.Null(SwitchValidation.Problem(
            Mapping(
                new SwitchPosition(8, "landing_gear", DesiredState.On),
                new SwitchPosition(9, "landing_gear", DesiredState.Off))));
    }

    [Fact]
    public void APositionThatMeansNothingIsAccepted()
    {
        // The centre detent of a three-position switch used as two states.
        Assert.Null(SwitchValidation.Problem(
            Mapping(
                new SwitchPosition(4, "landing_gear", DesiredState.On),
                new SwitchPosition(5),
                new SwitchPosition(6, "landing_gear", DesiredState.Off))));
    }

    [Fact]
    public void NothingHeldIsAPositionAndIsFoundByAReading()
    {
        var mapping = Mapping(new SwitchPosition(8, "landing_gear"), new SwitchPosition(null, "landing_gear"));

        Assert.Equal(8, mapping.At(Held(8))?.Button);
        Assert.NotNull(mapping.At(Held()));
        Assert.Null(mapping.At(Held())!.Button);
    }

    [Fact]
    public void NothingHeldIsUnknownOnASwitchWhoseWalkFoundAButtonEverywhere()
    {
        // The difference this type exists to keep: null is *unknown*, never *unchanged*.
        var mapping = Mapping(new SwitchPosition(8, "landing_gear"), new SwitchPosition(9, "landing_gear"));

        Assert.Null(mapping.At(Held()));
        Assert.Null(mapping.At(Held(20)));
    }

    [Fact]
    public void TwoOfTheSwitchesOwnButtonsHeldAtOnceIsUnknown()
    {
        var mapping = Mapping(new SwitchPosition(8, "landing_gear"), new SwitchPosition(9, "landing_gear"));

        Assert.Null(mapping.At(Held(8, 9)));
    }

    [Fact]
    public void TheModesAreTakenFromTheActionsTheSwitchNames()
    {
        var mapping = Mapping(
            new SwitchPosition(8, "srv_handbrake", DesiredState.On),
            new SwitchPosition(9, "srv_handbrake", DesiredState.Off));

        Assert.True((mapping.Contexts & ControlContext.Srv) != 0);
        Assert.Equal(ControlContext.None, mapping.Contexts & ControlContext.OnFoot);
    }
}

/// <summary>The file, and the rule that a bad entry is reported rather than dropped.</summary>
public class SwitchStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-switch-tests", Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(_folder, "switches.json");

    private SwitchStore Store() => new(Path_, NullLogger<SwitchStore>.Instance);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not a test failure.
        }
    }

    [Fact]
    public void NoFileIsNoSwitchesAndNoProblem()
    {
        var store = Store();

        Assert.False(store.Poll());
        Assert.Empty(store.Switches);
        Assert.Empty(store.Problems);
    }

    [Fact]
    public void WhatIsSavedIsWhatIsReadBack()
    {
        var store = Store();

        store.Save([
            new SwitchMapping
            {
                Name = "gear switch",
                DeviceId = "{wgi/nrid/throttle}",
                Device = "VID 0x4098 PID 0xBD65",
                Positions =
                [
                    new SwitchPosition(8, "landing_gear", DesiredState.On),
                    new SwitchPosition(9, "landing_gear", DesiredState.Off),
                ],
            },
        ]);

        Assert.True(store.Poll());

        var mapping = Assert.Single(store.Switches);
        Assert.Equal("gear switch", mapping.Name);
        Assert.Equal("{wgi/nrid/throttle}", mapping.DeviceId);
        Assert.Equal([8, 9], mapping.Positions.Select(position => position.Button));
        Assert.Equal(DesiredState.Off, mapping.Positions[1].State);
    }

    [Fact]
    public void ABadSwitchIsRefusedByNameAndTheRestOfTheFileStillLoads()
    {
        // A switch that quietly vanished would be a Commander flipping a toggle into the dark.
        Directory.CreateDirectory(_folder);

        File.WriteAllText(Path_, """
            {
              "switches": [
                { "name": "good", "deviceId": "d", "positions": [
                    { "button": 8, "action": "landing_gear", "state": "on" },
                    { "button": 9, "action": "landing_gear", "state": "off" } ] },
                { "name": "bad", "deviceId": "d", "positions": [
                    { "button": 8, "action": "boost", "state": "on" },
                    { "button": 9, "action": "boost", "state": "off" } ] }
              ]
            }
            """);

        var store = Store();

        Assert.True(store.Poll());
        Assert.Equal("good", Assert.Single(store.Switches).Name);

        var problem = Assert.Single(store.Problems);
        Assert.Equal("bad", problem.Name);
        Assert.Contains("does not report the state", problem.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableFileIsOneProblemWithOneNameRatherThanAThrow()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, "{ this is not json");

        var store = Store();

        Assert.True(store.Poll());
        Assert.Empty(store.Switches);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void AFileEditedByHandIsPickedUpWithoutARestart()
    {
        var store = Store();
        store.Save([]);
        store.Poll();

        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """
            {
              "switches": [
                { "name": "gear switch", "deviceId": "d", "positions": [
                    { "button": 8, "action": "landing_gear", "state": "on" },
                    { "button": 9, "action": "landing_gear", "state": "off" } ] }
              ]
            }
            """);

        Assert.True(store.Poll());
        Assert.Single(store.Switches);
    }

    /// <summary>
    /// The same thing as above, with the one variable that used to decide it held still.
    /// <para>
    /// The comment this replaces claimed that saving first was "what makes this real rather than a
    /// race the test happens to win". It was wrong, and precisely backwards: <c>Save</c> resets the
    /// stamp <em>before</em> the first poll, so the second write is the race — and on a machine
    /// quick enough to do both inside one 15.6 ms tick of the file-system clock, the edit carried
    /// the same last-write time as the save and the store saw nothing. The suite passed here and
    /// failed on CI, which is the only reason anybody found out.
    /// </para>
    /// <para>
    /// So the timestamp is pinned rather than hoped about. This fails against a store that decides
    /// by last-write time and passes against one that reads the file, which is the whole point of
    /// writing it.
    /// </para>
    /// </summary>
    [Fact]
    public void AnEditThatLandsOnTheSameTimestampIsStillPickedUp()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """{ "switches": [] }""");

        var frozen = File.GetLastWriteTimeUtc(Path_);

        var store = Store();
        Assert.True(store.Poll());
        Assert.Empty(store.Switches);

        File.WriteAllText(Path_, """
            {
              "switches": [
                { "name": "gear switch", "deviceId": "d", "positions": [
                    { "button": 8, "action": "landing_gear", "state": "on" },
                    { "button": 9, "action": "landing_gear", "state": "off" } ] }
              ]
            }
            """);

        File.SetLastWriteTimeUtc(Path_, frozen);
        Assert.Equal(frozen, File.GetLastWriteTimeUtc(Path_));

        Assert.True(store.Poll());
        Assert.Single(store.Switches);
    }

    [Fact]
    public void TwoSwitchesWithOneNameKeepTheFirstAndReportTheSecond()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path_, """
            {
              "switches": [
                { "name": "gear switch", "deviceId": "a", "positions": [
                    { "button": 8, "action": "landing_gear", "state": "on" },
                    { "button": 9, "action": "landing_gear", "state": "off" } ] },
                { "name": "Gear Switch", "deviceId": "b", "positions": [
                    { "button": 1, "action": "landing_gear", "state": "on" },
                    { "button": 2, "action": "landing_gear", "state": "off" } ] }
              ]
            }
            """);

        var store = Store();
        store.Poll();

        Assert.Equal("a", Assert.Single(store.Switches).DeviceId);
        Assert.Single(store.Problems);
    }
}
