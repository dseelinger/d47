using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A switch position can press a key chord once per flip into it, with nothing read back (#1428).</summary>
public class ASwitchPositionPressesAKeyChordTests
{
    private const string Stick = "{wgi/nrid/stick}";

    private static readonly KeyChord Paste = new("Key_V", ChordModifiers.Ctrl);

    /// <summary>A momentary stick button: nothing held, or button 3, which pastes.</summary>
    private static SwitchMapping PasteButton() => new()
    {
        Name = "paste button",
        DeviceId = Stick,
        Positions = [new SwitchPosition(null), new SwitchPosition(3, Chord: Paste)],
    };

    private static SwitchTick Tick(int[] held, int[]? downSince = null, bool chords = true)
    {
        var now = new bool[32];
        var down = new bool[32];

        foreach (var button in held)
        {
            now[button] = true;
            down[button] = true;
        }

        foreach (var button in downSince ?? [])
        {
            down[button] = true;
        }

        return new SwitchTick
        {
            Now = DateTimeOffset.UnixEpoch,
            Readings = [new HotasReading { Id = Stick, Buttons = now, DownSinceLastRead = down }],
            Status = new GameStatus { ReadAt = DateTimeOffset.UnixEpoch },
            Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
            Enabled = false,
            ChordsEnabled = chords,
        };
    }

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    [Fact]
    public void AFlipIntoTheChordPositionQueuesOnePressCarryingTheChord()
    {
        var reconciler = New();
        var switches = new[] { PasteButton() };

        reconciler.Poll(Tick([]), switches);
        reconciler.Poll(Tick([3]), switches);

        var pending = Assert.Single(reconciler.Drain());

        Assert.Equal(Paste, pending.Chord);
        Assert.Equal("Ctrl+V", pending.Label);
        Assert.Empty(pending.Steps);
        Assert.Null(pending.Say);
        Assert.Null(pending.View);
        Assert.Null(pending.Destination);
    }

    [Fact]
    public void AFlickBetweenPollsQueuesOnePress()
    {
        var reconciler = New();
        var switches = new[] { PasteButton() };

        reconciler.Poll(Tick([]), switches);
        reconciler.Poll(Tick([], downSince: [3]), switches);

        Assert.Equal(Paste, Assert.Single(reconciler.Drain()).Chord);
    }

    [Fact]
    public void FirstSightOfTheChordPositionQueuesNothing()
    {
        var reconciler = New();

        reconciler.Poll(Tick([3]), [PasteButton()]);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void ASwitchThatStaysPutQueuesNothingAfterTheFlip()
    {
        var reconciler = New();
        var switches = new[] { PasteButton() };

        reconciler.Poll(Tick([]), switches);
        reconciler.Poll(Tick([3]), switches);
        reconciler.Drain();

        reconciler.Poll(Tick([3]), switches);
        reconciler.Poll(Tick([3]), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void LettingGoOfTheButtonPressesNothing()
    {
        var reconciler = New();
        var switches = new[] { PasteButton() };

        reconciler.Poll(Tick([]), switches);
        reconciler.Poll(Tick([3]), switches);
        reconciler.Drain();

        reconciler.Poll(Tick([]), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void WithTheSettingOffNothingIsQueuedAndTheNoteSaysSo()
    {
        var reconciler = New();
        var switches = new[] { PasteButton() };

        reconciler.Poll(Tick([], chords: false), switches);
        reconciler.Poll(Tick([3], chords: false), switches);

        Assert.Empty(reconciler.Drain());

        var state = Assert.Single(reconciler.States);
        Assert.Equal(SwitchHealth.Off, state.Health);
        Assert.Contains("Let a HOTAS switch press key chords in any window", state.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void AChordPositionNeverDisagreesOrPauses()
    {
        var reconciler = New();
        var switches = new[] { PasteButton() };

        reconciler.Poll(Tick([]), switches);
        reconciler.Poll(Tick([3]), switches);
        reconciler.Poll(Tick([3]), switches);

        var state = Assert.Single(reconciler.States);
        Assert.Equal(SwitchHealth.Ready, state.Health);
        Assert.Null(state.Disagrees);
        Assert.False(reconciler.IsPaused("paste button"));
    }

    [Fact]
    public void AChordIsAssignedAndReachesNoGameMode()
    {
        var mapping = PasteButton();

        Assert.True(mapping.IsAssigned);
        Assert.Equal(ControlContext.None, mapping.Contexts);
        Assert.Null(SwitchValidation.Problem(mapping));
    }

    [Fact]
    public void APositionWithAChordAndAnActionIsRefusedWithOneMeaning()
    {
        var mapping = PasteButton() with
        {
            Positions = [new SwitchPosition(null), new SwitchPosition(3, "landing_gear", Chord: Paste)],
        };

        Assert.Equal(SwitchValidation.OneMeaning, SwitchValidation.Problem(mapping));
    }

    [Fact]
    public void TheRowFollowsTheHeadsetRowIsProtectedAndStartsOff()
    {
        var settings = SwitchCapability.Create(SwitchSurface.Inert, () => false).Settings;
        var keys = settings.Select(setting => setting.Key).ToList();
        var row = settings.Single(setting => setting.Key == SwitchCapability.ChordsKey);

        Assert.Equal(keys.IndexOf(SwitchCapability.HeadsetViewKey) + 1, keys.IndexOf(SwitchCapability.ChordsKey));
        Assert.Equal("Let a HOTAS switch press key chords in any window", row.Label);
        Assert.True(row.Protected);
        Assert.Null(row.AppliesWhen);
        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.Equal(2, row.Commands.Count);
        Assert.Equal("false", row.Binding!.Read!(new D47Settings()));
        Assert.Equal("true", row.Binding.Read(row.Binding.Write!(new D47Settings(), "true")));
    }

    [Fact]
    public void ARefusedChordIsRefusedInKeyChordsOwnWords()
    {
        var closer = new KeyChord("Key_F4", ChordModifiers.Alt);
        var mapping = PasteButton() with { Positions = [new SwitchPosition(null), new SwitchPosition(3, Chord: closer)] };

        Assert.Equal(KeyChord.Problem(closer), SwitchValidation.Problem(mapping));
    }
}
