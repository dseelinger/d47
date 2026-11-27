using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Where the headset route takes a picture of the headset view, <c>take_picture</c> keeps it beside Elite's JPEGs, or
/// in the captures folder while converting is off, and still presses F10. Either can be kept without the other.
/// </summary>
public class TakeAPictureInTheHeadsetKeepsTheHeadsetViewTooTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xD9];

    private static readonly HeadsetShot Taken = new(Jpeg, 1400, 1300, null);

    private sealed class FakeInput(InjectionResult result) : IGameInput
    {
        public int Presses { get; private set; }

        public bool IsGameForeground => result.Outcome != InjectionOutcome.NotForeground;

        public Task<InjectionResult> SendAsync(IReadOnlyList<InputStep> steps, CancellationToken cancellationToken = default)
        {
            if (result.Sent)
            {
                Presses++;
            }

            return Task.FromResult(result);
        }

        public void ReleaseAll()
        {
        }
    }

    private sealed record Rig(MemoryInstall Install, FakeInput Input, CapabilityRegistry Registry, string Shots);

    private static Rig Build(HeadsetShot shot, bool keys = true, bool convert = true, InjectionResult? press = null) =>
        Build(_ => Task.FromResult(shot), keys, convert, press);

    private static Rig Build(
        Func<CancellationToken, Task<HeadsetShot>> take,
        bool keys = true,
        bool convert = true,
        InjectionResult? press = null)
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var defaults = new D47Settings();
        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance),
            defaults with
            {
                Actions = defaults.Actions with { Keyboard = keys },
                Screenshots = defaults.Screenshots with { Convert = convert },
            },
            NullLogger<SettingsService>.Instance);

        var input = new FakeInput(press ?? InjectionResult.Ok);
        var game = Game();
        var shots = Path.Combine(install.Paths.Data, "shots");
        var registry = CapabilityRegistry.Build(
        [
            ScreenCapability.Create(
                settings,
                null,
                new PictureKeeping(
                    cancellationToken => input.SendAsync(ScreenCapability.ScreenshotKey, cancellationToken),
                    () => game.Active,
                    () => Now,
                    new HeadsetKeeping(take, install.Files, () => shots, install.Paths.Captures))),
        ]);

        return new Rig(install, input, registry, shots);
    }

    private static GameStateStore Game()
    {
        var store = new GameStateStore();

        foreach (var json in new[]
        {
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-08-16T09:00:01Z","event":"LoadGame","Credits":1}""",
            """{"timestamp":"2026-08-16T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":1000300,"StarPos":[0,0,0],"Body":"Fixture A 1","BodyID":4}""",
        })
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static Task<ToolResult> Take(Rig rig) =>
        rig.Registry.InvokeAsync(
            ScreenCapability.TakePictureToolName,
            ToolArguments.Empty,
            TestContext.Current.CancellationToken,
            ToolCaller.Commander);

    [Fact]
    public async Task BothAreTakenAndTheHeadsetViewIsKeptBesideElitesJpegs()
    {
        var rig = Build(Taken);

        var result = await Take(rig);

        Assert.False(result.IsError);
        Assert.Equal(ScreenCapability.BothTaken, result.Content);
        Assert.Equal(1, rig.Input.Presses);

        var kept = Assert.Single(rig.Install.Files.Enumerate(rig.Shots, "*.jpg"));
        Assert.EndsWith(ScreenCapability.HeadsetSuffix + ".jpg", kept);
        Assert.Equal(Jpeg, rig.Install.Files.ReadBytes(kept));

        var note = PictureNote.Parse(rig.Install.Files.ReadText(Path.ChangeExtension(kept, ".json")));
        Assert.NotNull(note);
        Assert.Equal((Now, ScreenPictures.FromHeadset, 1400, 1300), (note.At, note.Source, note.Width, note.Height));
        Assert.Equal(("Fixture", 1000300L, "Fixture A 1"), (note.System, note.SystemAddress, note.Body));
        Assert.Equal(PictureLift.HeadsetGamma, PictureLift.For(note.Source));
    }

    [Fact]
    public async Task WithConvertingOffTheHeadsetViewGoesToTheCapturesFolderAndMovesInLater()
    {
        var rig = Build(Taken, convert: false);

        var result = await Take(rig);

        Assert.Equal($"{ScreenCapability.BothTaken} {ScreenCapability.BmpOnly}", result.Content);
        Assert.Empty(rig.Install.Files.Enumerate(rig.Shots, "*.jpg"));
        Assert.Single(rig.Install.Files.Enumerate(rig.Install.Paths.Captures, "capture-*.jpg"));

        Assert.Equal(1, ScreenshotFolder.MoveCaptures(rig.Install.Files, rig.Install.Paths.Captures, rig.Shots));
        var moved = Assert.Single(ScreenshotFolder.List(rig.Install.Files, rig.Shots));
        Assert.Equal(ScreenPictures.FromHeadset, moved.Note?.Source);
    }

    [Fact]
    public async Task WithPressingKeysOffTheHeadsetViewIsStillKept()
    {
        var rig = Build(Taken, keys: false);

        var result = await Take(rig);

        Assert.False(result.IsError);
        Assert.Equal(
            $"{ScreenCapability.HeadsetTaken} Elite took no screenshot. {ScreenCapability.KeysOffShort}",
            result.Content);
        Assert.Equal(0, rig.Input.Presses);
        Assert.Single(rig.Install.Files.Enumerate(rig.Shots, "*.jpg"));
    }

    [Fact]
    public async Task WithEliteNotInFrontTheHeadsetViewIsKeptAndTheReplySaysWhyF10WasNot()
    {
        var rig = Build(Taken, press: new InjectionResult(InjectionOutcome.NotForeground, "Elite is not the window in front, so I did not send that."));

        var result = await Take(rig);

        Assert.False(result.IsError);
        Assert.Equal(
            $"{ScreenCapability.HeadsetTaken} Elite took no screenshot. Elite is not the window in front, so I did not send that.",
            result.Content);
        Assert.Single(rig.Install.Files.Enumerate(rig.Shots, "*.jpg"));
    }

    [Fact]
    public async Task AHeadsetViewThatFailsLeavesF10AndSaysWhy()
    {
        var rig = Build(new HeadsetShot(null, 0, 0, "Elite did not hand over its eye image within a second."));

        var result = await Take(rig);

        Assert.False(result.IsError);
        Assert.Equal(
            $"Picture taken. {ScreenCapability.NoHeadset} Elite did not hand over its eye image within a second.",
            result.Content);
        Assert.Equal(1, rig.Input.Presses);
        Assert.Empty(rig.Install.Files.Enumerate(rig.Shots, "*.jpg"));
    }

    [Fact]
    public async Task AHeadsetPictureThatThrowsLeavesF10AndSaysWhy()
    {
        var rig = Build(_ => throw new InvalidOperationException("the device was lost"));

        var result = await Take(rig);

        Assert.False(result.IsError);
        Assert.Equal($"Picture taken. {ScreenCapability.NoHeadset} the device was lost", result.Content);
        Assert.Equal(1, rig.Input.Presses);
    }

    [Fact]
    public async Task WhenNeitherIsTakenTheReplyIsAnErrorGivingBothReasons()
    {
        var rig = Build(new HeadsetShot(null, 0, 0, "The headset is not shown through the D47 layer."), keys: false);

        var result = await Take(rig);

        Assert.True(result.IsError);
        Assert.Equal(
            $"No picture was taken. {ScreenCapability.KeysOffShort} {ScreenCapability.NoHeadset} The headset is not shown through the D47 layer.",
            result.Content);
    }

    [Fact]
    public async Task OffTheLayerRouteNothingChanges()
    {
        var rig = Build(HeadsetShot.NotHere);

        var result = await Take(rig);

        Assert.Equal("Picture taken.", result.Content);
        Assert.Equal(1, rig.Input.Presses);
        Assert.Empty(rig.Install.Files.Enumerate(rig.Shots, "*.jpg"));
    }

    [Fact]
    public async Task OffTheLayerRouteWithPressingKeysOffTheReplyIsAsBefore()
    {
        var rig = Build(HeadsetShot.NotHere, keys: false);

        var result = await Take(rig);

        Assert.True(result.IsError);
        Assert.Equal(ScreenCapability.KeysOff, result.Content);
    }

    [Fact]
    public async Task TwoPicturesInOneSecondKeepTwoFiles()
    {
        var rig = Build(Taken);

        await Take(rig);
        await Take(rig);

        Assert.Equal(2, rig.Install.Files.Enumerate(rig.Shots, "*.jpg").Count());
    }
}
