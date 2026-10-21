using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary><c>take_picture</c> keeps a full-size picture and a note, for the Commander alone.</summary>
public class ACommanderTakenPictureIsKeptWithANoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

    private static readonly byte[] Full = [0xFF, 0xD8, 0xFF, 0xE0, 0x09, 0x09];

    private static readonly int Here = GalacticRegions.IdOf(GalacticRegions.Find(StarPosition.Origin)!)!.Value;

    private sealed class FakeCapture(ScreenCaptureResult full) : IScreenCapture
    {
        public int Taken { get; private set; }

        public int TakenFull { get; private set; }

        public ScreenCaptureResult Take()
        {
            Taken++;
            return new ScreenCaptureResult(null, "not asked for");
        }

        public ScreenCaptureResult TakeFull()
        {
            TakenFull++;
            return full;
        }
    }

    private static FakeCapture Picture() =>
        new(new ScreenCaptureResult(new ScreenPicture(Full, 3840, 2160, ScreenPictures.FromWindow), null));

    private static SettingsService Settings(MemoryInstall install, bool lookAtScreen)
    {
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var settings = new D47Settings();

        return new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance),
            settings with { Llm = settings.Llm with { LookAtScreen = lookAtScreen } },
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

    private static CapabilityRegistry Registry(
        MemoryInstall install,
        IScreenCapture capture,
        GameStateStore store,
        bool lookAtScreen = false) =>
        CapabilityRegistry.Build(
        [
            ScreenCapability.Create(
                Settings(install, lookAtScreen),
                capture,
                new PictureKeeping(install.Files, install.Paths.Captures, () => store.Active, () => Now)),
        ]);

    private static Task<ToolResult> Take(CapabilityRegistry registry, ToolCaller caller = ToolCaller.Commander) =>
        registry.InvokeAsync(
            ScreenCapability.TakePictureToolName,
            ToolArguments.Empty,
            TestContext.Current.CancellationToken,
            caller);

    private static string Picked(MemoryInstall install, string extension) =>
        Assert.Single(install.Files.Enumerate(install.Paths.Captures, "*" + extension));

    [Fact]
    public async Task TakeAPictureWritesThePictureAndANoteWithTheRegionFromThePosition()
    {
        var install = new MemoryInstall();
        var capture = Picture();

        var result = await Take(Registry(install, capture, Store()));

        Assert.False(result.IsError);
        Assert.Equal("Picture taken.", result.Content);
        Assert.Equal((0, 1), (capture.Taken, capture.TakenFull));

        var jpg = Picked(install, ".jpg");
        var json = Picked(install, ".json");

        Assert.Equal("capture-20260816-103000.jpg", Path.GetFileName(jpg));
        Assert.Equal("capture-20260816-103000.json", Path.GetFileName(json));
        Assert.Equal(Full, install.Files.ReadBytes(jpg));

        var note = PictureNote.Parse(install.Files.ReadText(json));

        Assert.NotNull(note);
        Assert.Equal((ScreenPictures.FromWindow, 3840, 2160), (note.Source, note.Width, note.Height));
        Assert.Equal(("Fixture", 1000300L, "Fixture A 1", Here), (note.System, note.SystemAddress, note.Body, note.RegionId));
        Assert.Equal(Now, note.At);
    }

    [Fact]
    public async Task TheNoteHoldsOnlyTheCodexEntriesFromTheLastTenMinutes()
    {
        var install = new MemoryInstall();
        var store = Store("2026-08-16T10:05:00Z", "2026-08-16T10:21:00Z", "2026-08-16T10:29:00Z");

        await Take(Registry(install, Picture(), store));

        var note = PictureNote.Parse(install.Files.ReadText(Picked(install, ".json")));

        Assert.Equal(
            [new PictureNoteEntry(2, "Entry 2", Here), new PictureNoteEntry(3, "Entry 3", Here)],
            note!.CodexEntries);
    }

    [Fact]
    public async Task TheAnswerNamesAtMostThreeEntriesThenCountsTheRest()
    {
        var install = new MemoryInstall();
        var store = Store(
            "2026-08-16T10:21:00Z",
            "2026-08-16T10:22:00Z",
            "2026-08-16T10:23:00Z",
            "2026-08-16T10:24:00Z",
            "2026-08-16T10:25:00Z");

        var result = await Take(Registry(install, Picture(), store));

        Assert.Equal("Picture taken. Noted with Entry 1, Entry 2, Entry 3 and 2 more.", result.Content);
    }

    [Fact]
    public async Task TheAnswerListsTwoEntriesWithAnAnd()
    {
        var install = new MemoryInstall();
        var store = Store("2026-08-16T10:21:00Z", "2026-08-16T10:22:00Z");

        var result = await Take(Registry(install, Picture(), store));

        Assert.Equal("Picture taken. Noted with Entry 1 and Entry 2.", result.Content);
    }

    [Fact]
    public async Task WithoutAPositionTheRegionNumberIsNull()
    {
        var install = new MemoryInstall();

        await Take(Registry(install, Picture(), new GameStateStore()));

        var note = PictureNote.Parse(install.Files.ReadText(Picked(install, ".json")));

        Assert.Null(note!.RegionId);
        Assert.Null(note.System);
        Assert.Empty(note.CodexEntries);
    }

    [Fact]
    public async Task WithAHundredPairsAlreadyThereTheOldestPairGoes()
    {
        var install = new MemoryInstall();

        for (var i = 0; i < ScreenCapability.KeptPictures; i++)
        {
            var stem = Path.Combine(install.Paths.Captures, $"capture-20260101-{i:000000}");
            install.Files.WriteBytes(stem + ".jpg", [1]);
            install.Files.WriteText(stem + ".json", "{}");
        }

        await Take(Registry(install, Picture(), Store()));

        var kept = install.Files.Enumerate(install.Paths.Captures, "*.jpg");

        Assert.Equal(ScreenCapability.KeptPictures, kept.Count);
        Assert.Equal(ScreenCapability.KeptPictures, install.Files.Enumerate(install.Paths.Captures, "*.json").Count);
        Assert.DoesNotContain(kept, path => path.EndsWith("capture-20260101-000000.jpg", StringComparison.Ordinal));
        Assert.Null(install.Files.ReadText(Path.Combine(install.Paths.Captures, "capture-20260101-000000.json")));
        Assert.Contains(kept, path => path.EndsWith("capture-20260816-103000.jpg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARefusedCaptureWritesNothingAndTheAnswerGivesTheRefusal()
    {
        var install = new MemoryInstall();
        var capture = new FakeCapture(new ScreenCaptureResult(null, "Elite is not running"));

        var result = await Take(Registry(install, capture, Store()));

        Assert.True(result.IsError);
        Assert.Equal("No picture was taken: Elite is not running.", result.Content);
        Assert.Empty(install.Files.Enumerate(install.Paths.Captures, "*"));
    }

    [Fact]
    public async Task TheToolWorksWithTheModelSettingOff()
    {
        var install = new MemoryInstall();

        var result = await Take(Registry(install, Picture(), Store(), lookAtScreen: false));

        Assert.False(result.IsError);
        Assert.Single(install.Files.Enumerate(install.Paths.Captures, "*.jpg"));
    }

    [Fact]
    public async Task TheModelIsRefusedTheToolAndTheCommanderIsNot()
    {
        var install = new MemoryInstall();
        var capture = Picture();
        var registry = Registry(install, capture, Store());

        var refused = await Take(registry, ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.Equal(0, capture.TakenFull);
        Assert.Empty(install.Files.Enumerate(install.Paths.Captures, "*"));

        var allowed = await Take(registry, ToolCaller.Commander);

        Assert.False(allowed.IsError);
        Assert.Equal(1, capture.TakenFull);
    }

    [Fact]
    public void TheKeywordRouterReachesTheToolByItsThreePhrases()
    {
        var install = new MemoryInstall();
        var registry = Registry(install, Picture(), Store());
        var router = new KeywordRouter(registry);

        foreach (var phrase in new[] { "take a picture", "take a picture of this", "save a picture" })
        {
            var matched = router.MatchToolCommand(phrase);

            Assert.NotNull(matched);
            Assert.Equal(ScreenCapability.TakePictureToolName, matched.ToolName);
        }
    }

    [Fact]
    public void ANoteReadBackIsTheNoteThatWasWritten()
    {
        var note = new PictureNote(
            Now,
            ScreenPictures.FromHeadset,
            3072,
            1728,
            "Fixture",
            1000300,
            "Fixture A 1",
            Here,
            [new PictureNoteEntry(7, "Entry 7", Here), new PictureNoteEntry(8, null, null)]);

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
