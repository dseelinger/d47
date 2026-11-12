using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// <c>take_picture</c> presses Elite's screenshot key, F10, off the calling thread, for the Commander alone, and keeps
/// nothing itself; the note goes beside the JPEG copy of the screenshot.
/// </summary>
public class TakeAPicturePressesElitesScreenshotKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

    private static readonly int Here = GalacticRegions.IdOf(GalacticRegions.Find(StarPosition.Origin)!)!.Value;

    private sealed class FakeInput(InjectionResult result) : IGameInput
    {
        public List<IReadOnlyList<InputStep>> Sent { get; } = [];

        public int? Thread { get; private set; }

        public bool IsGameForeground => result.Outcome != InjectionOutcome.NotForeground;

        public Task<InjectionResult> SendAsync(IReadOnlyList<InputStep> steps, CancellationToken cancellationToken = default)
        {
            Thread = Environment.CurrentManagedThreadId;

            if (result.Sent)
            {
                Sent.Add(steps);
            }

            return Task.FromResult(result);
        }

        public void ReleaseAll()
        {
        }
    }

    private static FakeInput InFront() => new(InjectionResult.Ok);

    private static SettingsService Settings(MemoryInstall install, bool keys = true, bool convert = true, bool lookAtScreen = false)
    {
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var settings = new D47Settings();

        return new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance),
            settings with
            {
                Llm = settings.Llm with { LookAtScreen = lookAtScreen },
                Actions = settings.Actions with { Keyboard = keys },
                Screenshots = settings.Screenshots with { Convert = convert },
            },
            NullLogger<SettingsService>.Instance);
    }

    private static GameStateStore Store(params string[] entryTimes)
    {
        var store = new GameStateStore();

        var lines = new List<string>
        {
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-08-16T09:00:01Z","event":"LoadGame","Credits":1}""",
            """{"timestamp":"2026-08-16T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":1000300,"StarPos":[0,0,0],"Body":"Fixture A 1","BodyID":4}""",
        };

        for (var i = 0; i < entryTimes.Length; i++)
        {
            lines.Add(
                $$"""{"timestamp":"{{entryTimes[i]}}","event":"CodexEntry","EntryID":{{i + 1}},"Name":"$Codex_Ent_{{i + 1}};","Name_Localised":"Entry {{i + 1}}","Region":"$Codex_RegionName_{{Here}};","System":"Fixture","SystemAddress":1000300,"BodyID":4}""");
        }

        foreach (var json in lines)
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static CapabilityRegistry Registry(SettingsService settings, IGameInput input, GameStateStore store) =>
        CapabilityRegistry.Build(
        [
            ScreenCapability.Create(
                settings,
                null,
                new PictureKeeping(
                    cancellationToken => input.SendAsync(ScreenCapability.ScreenshotKey, cancellationToken),
                    () => store.Active,
                    () => Now)),
        ]);

    private static Task<ToolResult> Take(CapabilityRegistry registry, ToolCaller caller = ToolCaller.Commander) =>
        registry.InvokeAsync(
            ScreenCapability.TakePictureToolName,
            ToolArguments.Empty,
            TestContext.Current.CancellationToken,
            caller);

    /// <summary>Runs <paramref name="take"/> on a thread of its own that waits for it, so a press made on that thread shows.</summary>
    private static (int Thread, ToolResult Result) OnABlockedThread(Func<Task<ToolResult>> take)
    {
        var thread = 0;
        ToolResult? result = null;
        var own = new Thread(() =>
        {
            thread = Environment.CurrentManagedThreadId;
            result = take().GetAwaiter().GetResult();
        });

        own.Start();
        own.Join();

        return (thread, result!);
    }

    private static EliteScreenshot Shot(string? system = "Fixture") =>
        new("Screenshot_0001.bmp", 3840, 2160, system, "A 1");

    [Fact]
    public void WithEliteInFrontOneF10TapIsSentOffTheCallingThreadAndNothingIsKept()
    {
        var install = new MemoryInstall();
        var input = InFront();
        var registry = Registry(Settings(install), input, Store());

        var (caller, result) = OnABlockedThread(() => Take(registry));

        Assert.False(result.IsError);
        Assert.Equal("Picture taken.", result.Content);

        var steps = Assert.Single(input.Sent);
        Assert.Equal(
            [new InputStep(InputStepKind.KeyDown, 0x79), InputStep.Wait(InputSequence.TapHold), new InputStep(InputStepKind.KeyUp, 0x79)],
            steps);
        Assert.NotEqual(caller, input.Thread);
        Assert.Empty(install.Files.Enumerate(install.Paths.Captures, "*"));
    }

    [Fact]
    public async Task WithEliteNotInFrontNothingIsPressedAndTheReplySaysWhy()
    {
        var install = new MemoryInstall();
        var input = new FakeInput(new InjectionResult(InjectionOutcome.NotForeground, "Elite is not the window in front, so I did not send that."));

        var result = await Take(Registry(Settings(install), input, Store()));

        Assert.True(result.IsError);
        Assert.Equal("No picture was taken. Elite is not the window in front, so I did not send that.", result.Content);
        Assert.Empty(input.Sent);
    }

    [Fact]
    public async Task WithConvertingOffTheKeyIsStillPressedAndTheReplySaysItIsTheBmpOnly()
    {
        var install = new MemoryInstall();
        var input = InFront();

        var result = await Take(Registry(Settings(install, convert: false), input, Store("2026-08-16T10:25:00Z")));

        Assert.False(result.IsError);
        Assert.Equal("Picture taken. It is Elite's BMP only, because converting Elite's screenshots is off.", result.Content);
        Assert.Single(input.Sent);
    }

    [Fact]
    public async Task WithPressingKeysOffNothingIsPressedAndTheReplySaysSo()
    {
        var install = new MemoryInstall();
        var input = InFront();

        var result = await Take(Registry(Settings(install, keys: false), input, Store()));

        Assert.True(result.IsError);
        Assert.Equal(ScreenCapability.KeysOff, result.Content);
        Assert.Empty(input.Sent);
    }

    [Fact]
    public async Task TheAnswerNamesAtMostThreeEntriesFromTheLastTenMinutesThenCountsTheRest()
    {
        var install = new MemoryInstall();
        var store = Store(
            "2026-08-16T10:05:00Z",
            "2026-08-16T10:21:00Z",
            "2026-08-16T10:22:00Z",
            "2026-08-16T10:23:00Z",
            "2026-08-16T10:24:00Z",
            "2026-08-16T10:25:00Z");

        var result = await Take(Registry(Settings(install), InFront(), store));

        Assert.Equal("Picture taken. Noted with Entry 2, Entry 3, Entry 4 and 2 more.", result.Content);
    }

    [Fact]
    public async Task TheAnswerListsTwoEntriesWithAnAnd()
    {
        var install = new MemoryInstall();
        var store = Store("2026-08-16T10:21:00Z", "2026-08-16T10:22:00Z");

        var result = await Take(Registry(Settings(install), InFront(), store));

        Assert.Equal("Picture taken. Noted with Entry 1 and Entry 2.", result.Content);
    }

    [Fact]
    public async Task TheToolWorksWithTheModelSettingOff()
    {
        var install = new MemoryInstall();
        var input = InFront();

        var result = await Take(Registry(Settings(install, lookAtScreen: false), input, Store()));

        Assert.False(result.IsError);
        Assert.Single(input.Sent);
    }

    [Fact]
    public async Task TheModelIsRefusedTheToolAndTheCommanderIsNot()
    {
        var install = new MemoryInstall();
        var input = InFront();
        var registry = Registry(Settings(install), input, Store());

        var refused = await Take(registry, ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.Empty(input.Sent);

        var allowed = await Take(registry, ToolCaller.Commander);

        Assert.False(allowed.IsError);
        Assert.Single(input.Sent);
    }

    [Fact]
    public void TheKeywordRouterReachesTheToolByItsThreePhrases()
    {
        var install = new MemoryInstall();
        var registry = Registry(Settings(install), InFront(), Store());
        var router = new KeywordRouter(registry);

        foreach (var phrase in new[] { "take a picture", "take a picture of this", "save a picture" })
        {
            var matched = router.MatchToolCommand(phrase);

            Assert.NotNull(matched);
            Assert.Equal(ScreenCapability.TakePictureToolName, matched.ToolName);
        }
    }

    [Fact]
    public void AScreenshotsNoteHoldsItsPlaceTheRegionTheBmpAndTheEntriesFromTheTenMinutesBeforeIt()
    {
        var store = Store("2026-08-16T10:05:00Z", "2026-08-16T10:21:00Z", "2026-08-16T10:29:00Z", "2026-08-16T10:31:00Z");

        var note = ScreenCapability.ScreenshotNote(Shot(), Now, store.Active);

        Assert.Equal((Now, ScreenPictures.FromScreenshot, 3840, 2160), (note.At, note.Source, note.Width, note.Height));
        Assert.Equal(("Fixture", 1000300L, "A 1", Here), (note.System, note.SystemAddress, note.Body, note.RegionId));
        Assert.Equal("Screenshot_0001.bmp", note.Bmp);
        Assert.Equal(
            [new PictureNoteEntry(2, "Entry 2", Here), new PictureNoteEntry(3, "Entry 3", Here)],
            note.CodexEntries);
        Assert.Null(PictureLift.For(note.Source));
    }

    [Fact]
    public void AScreenshotOfAnotherSystemOrWithNoStateHasNoRegionOrAddress()
    {
        var elsewhere = ScreenCapability.ScreenshotNote(Shot("Elsewhere"), Now, Store().Active);
        var unknown = ScreenCapability.ScreenshotNote(Shot(), Now, null);

        Assert.Equal(("Elsewhere", (long?)null, (int?)null), (elsewhere.System, elsewhere.SystemAddress, elsewhere.RegionId));
        Assert.Equal(((long?)null, (int?)null), (unknown.SystemAddress, unknown.RegionId));
        Assert.Empty(unknown.CodexEntries);
    }

    [Fact]
    public void ANoteReadBackIsTheNoteThatWasWritten()
    {
        var note = new PictureNote(
            Now,
            ScreenPictures.FromScreenshot,
            3072,
            1728,
            "Fixture",
            1000300,
            "A 1",
            Here,
            [new PictureNoteEntry(7, "Entry 7", Here), new PictureNoteEntry(8, null, null)],
            "Screenshot_0001.bmp");

        var read = PictureNote.Parse(note.Write());

        Assert.NotNull(read);
        Assert.Equal(note with { CodexEntries = [] }, read with { CodexEntries = [] });
        Assert.Equal(note.CodexEntries, read.CodexEntries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void TextThatIsNotANoteParsesToNull(string? text) => Assert.Null(PictureNote.Parse(text));
}
