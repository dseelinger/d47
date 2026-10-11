using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public sealed class TheCustomVoicesPageAddsAndListsVoicesTests : IDisposable
{
    private const int Rate = 48_000;
    private const string Data = @"C:\d47-memory\data";

    private readonly MemoryFileSystem _files = new();
    private readonly CustomVoices _store;
    private readonly OwnVoice _own;
    private readonly OwnVoiceRecording _recording;
    private readonly Decoder _decoder = new();
    private readonly List<AudioClip> _played = [];
    private bool _chatterboxInstalled = true;

    public TheCustomVoicesPageAddsAndListsVoicesTests()
    {
        _store = new CustomVoices(Data, _files, new PlainProtector());
        _own = new OwnVoice(Data, _files, new PlainProtector());
        _recording = new OwnVoiceRecording(
            _own,
            new OwnVoiceCapture(NullLogger<OwnVoiceCapture>.Instance),
            () => null,
            () => false,
            () => { },
            _played.Add,
            NullLogger.Instance);
    }

    public void Dispose() => _recording.Dispose();

    private static float[] Recording(double quiet, double loud) =>
    [
        .. new float[(int)(quiet * Rate)],
        .. Enumerable.Range(0, (int)(loud * Rate)).Select(i => 0.1f + (0.02f * (i / Rate))),
    ];

    private CustomVoicesEditing Editing() => new(
        _store,
        _recording,
        new CustomVoiceDraft(_store, _recording, _decoder, _played.Add, NullLogger.Instance),
        _played.Add,
        () => _chatterboxInstalled,
        (_, _) => Task.FromResult<string?>(null));

    private (CustomVoicesPage Page, Window Window, CustomVoiceDraft Draft) Shown(int height = 1100)
    {
        var editing = Editing();
        var draft = editing.Draft;
        var page = new CustomVoicesPage(editing);
        var window = new Window { Content = page, Width = 960, Height = height };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (page, window, draft);
    }

    private static T Named<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static bool Shows(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Any(block => block.IsEffectivelyVisible && block.Text?.Contains(text, StringComparison.Ordinal) == true);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void WithNothingRecordedThePageSaysWhatEachPartNeeds()
    {
        using var look = AppLook.Put();
        var (page, window, _) = Shown();

        Assert.True(Shows(page, "Not recorded. Story lines in your voice need a recording."));
        Assert.True(Shows(page, "No other custom voices yet. Add one below."));
        Assert.True(Shows(page, "Record or choose a file first."));
        Assert.Equal("Record", Named<Button>(page, "RecordOwnVoice").Content);
        Assert.False(Named<Button>(page, "PlayTake").IsEnabled);
        Assert.False(Named<Button>(page, "SaveTake").IsEnabled);
        Assert.False(Named<Notice>(page, "Chatterbox").IsVisible);

        Capture(window, "custom-voices-empty.png");
        window.Close();
    }

    [AvaloniaFact]
    public void EachVoiceIsListedUnderYourVoiceWithItsDetails()
    {
        using var look = AppLook.Put();
        Assert.Null(_own.Save(Recording(0, 6), Rate));
        Assert.Null(_store.Save("Harbour master", "female", "low", "slow", Recording(0, 8), Rate));
        Assert.Null(_store.Save("Old pilot", "male", "mid", "even", Recording(0, 8), Rate));

        var (page, window, _) = Shown();

        Assert.True(Shows(page, "You, recorded here · Stories cast your lines in it"));
        Assert.True(Shows(page, "Female · Low pitch · Slow pace"));
        Assert.True(Shows(page, "Male · Mid pitch · Even pace"));
        Assert.Equal("Record again", Named<Button>(page, "RecordOwnVoice").Content);
        Assert.False(Shows(page, "No other custom voices yet."));

        Capture(window, "custom-voices-list.png");
        window.Close();
    }

    [AvaloniaFact]
    public void DeletingAVoiceAsksOnceThenDeletes()
    {
        using var look = AppLook.Put();
        Assert.Null(_store.Save("Old pilot", "male", "mid", "even", Recording(0, 8), Rate));

        var (page, window, _) = Shown();

        var delete = page.GetVisualDescendants().OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Delete Old pilot");

        Press(delete);
        Assert.Equal("Press again to confirm", delete.Content);
        Assert.Single(_store.List());

        Press(delete);
        Assert.Empty(_store.List());
        Assert.True(Shows(page, "No other custom voices yet. Add one below."));

        window.Close();
    }

    [AvaloniaFact]
    public void RenamingAVoiceKeepsItAndRefusesANameInUse()
    {
        using var look = AppLook.Put();
        Assert.Null(_store.Save("Old pilot", "male", "mid", "even", Recording(0, 8), Rate));
        Assert.Null(_store.Save("Kestrel", "", "mid", "even", Recording(0, 8), Rate));

        var (page, window, _) = Shown();

        Press(page.GetVisualDescendants().OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Rename Old pilot"));

        var box = Named<TextBox>(page, "RenameVoice");
        box.Text = "KESTREL";
        Dispatcher.UIThread.RunJobs();
        Press(Named<Button>(page, "SaveRename"));

        Assert.Equal("A custom voice is already named \"KESTREL\".", FieldMessage.GetText(Named<TextBox>(page, "RenameVoice")));
        Capture(window, "custom-voices-rename-taken.png");

        Named<TextBox>(page, "RenameVoice").Text = "Night pilot";
        Dispatcher.UIThread.RunJobs();
        Press(Named<Button>(page, "SaveRename"));

        Assert.Equal(["Night pilot", "Kestrel"], _store.List().Select(voice => voice.Name));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<TextBox>(), found => found.Name == "RenameVoice");
        Assert.True(Shows(page, "Night pilot"));

        window.Close();
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void TheVoiceAndHearingRowSaysWhatIsKeptAndOpensThePage()
    {
        using var look = AppLook.Put();
        Assert.Null(_own.Save(Recording(0, 6), Rate));
        Assert.Null(_store.Save("Old pilot", "male", "mid", "even", Recording(0, 8), Rate));

        var editing = Editing();
        Assert.Equal("Your voice is recorded. 1 other custom voice.", editing.Summary());

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, customVoices: editing);

        host.View.RevealRow(D47.Core.Capabilities.Builtin.SpeechCapability.CustomVoicesKey);
        Dispatcher.UIThread.RunJobs();

        var open = Named<Button>(host.View, "OpenCustomVoices");
        open.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        Capture(host.Window, "custom-voices-settings-row.png");

        Press(open);

        Assert.Single(host.Panel.GetVisualDescendants().OfType<CustomVoicesPage>());

        host.Close();
    }

    [AvaloniaFact]
    public async Task AFileTakeShowsTheSevenSecondsItKeeps()
    {
        using var look = AppLook.Put();
        _decoder.Next = new DecodedSamples(Recording(2, 40), Rate);

        var (page, window, draft) = Shown();

        await draft.ImportAsync(@"C:\clips\announcer-take3.mp3");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "announcer-take3.mp3 · 0:42"));
        Assert.True(Shows(page, "0:02 – 0:09"));
        Assert.True(Named<Button>(page, "PlayTake").IsEnabled);
        Assert.True(Named<Button>(page, "SaveTake").IsEnabled);

        Press(Named<Button>(page, "PlayTake"));
        Assert.Equal(7 * CustomVoices.SampleRate * 2, Assert.Single(_played).Pcm.Length);

        Capture(window, "custom-voices-take-ready.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ASilentFileIsNotKeptAndSaysWhy()
    {
        using var look = AppLook.Put();
        _decoder.Next = new DecodedSamples(new float[10 * Rate], Rate);

        var (page, window, draft) = Shown();

        await draft.ImportAsync(@"C:\clips\quiet.wav");
        Dispatcher.UIThread.RunJobs();

        var refused = Named<Notice>(page, "NotKept");
        Assert.True(refused.IsVisible);
        Assert.Contains("louder than -40 dBFS", refused.Text, StringComparison.Ordinal);
        Assert.False(Named<Button>(page, "SaveTake").IsEnabled);

        Capture(window, "custom-voices-refused.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task AFileThatWillNotDecodeSaysWhy()
    {
        using var look = AppLook.Put();
        _decoder.Failure = "The file could not be decoded.";

        var (page, window, draft) = Shown();

        await draft.ImportAsync(@"C:\clips\broken.mp3");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("The file could not be decoded.", Named<Notice>(page, "NotKept").Text);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ATakenNameIsShownOnTheNameField()
    {
        using var look = AppLook.Put();
        Assert.Null(_store.Save("Kestrel", "", "mid", "even", Recording(0, 8), Rate));
        _decoder.Next = new DecodedSamples(Recording(0, 8), Rate);

        var (page, window, draft) = Shown();

        await draft.ImportAsync(@"C:\clips\kestrel.wav");
        Dispatcher.UIThread.RunJobs();

        var name = Named<TextBox>(page, "VoiceName");
        name.Text = "kestrel";
        Press(Named<Button>(page, "SaveTake"));

        Assert.Equal("A custom voice is already named \"kestrel\".", FieldMessage.GetText(name));
        Assert.False(Named<Notice>(page, "NotKept").IsVisible);
        Assert.Single(_store.List());

        Capture(window, "custom-voices-name-taken.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task SavingATakeAddsItToTheListWithItsChoices()
    {
        using var look = AppLook.Put();
        _decoder.Next = new DecodedSamples(Recording(1, 9), Rate);

        var (page, window, draft) = Shown();

        await draft.ImportAsync(@"C:\clips\night.flac");
        Dispatcher.UIThread.RunJobs();

        Named<TextBox>(page, "VoiceName").Text = "Night watch";
        Pick(page, "Gender", 0);
        Pick(page, "Pace", 2);
        Press(Named<Button>(page, "SaveTake"));

        var saved = Assert.Single(_store.List());
        Assert.Equal(("Night watch", "female", "mid", "brisk"), (saved.Name, saved.Gender, saved.Pitch, saved.Pace));
        Assert.True(Shows(page, "Female · Mid pitch · Brisk pace"));
        Assert.False(draft.HasTake);
        Assert.Equal(string.Empty, Named<TextBox>(page, "VoiceName").Text);

        window.Close();
    }

    [AvaloniaFact]
    public void WithoutChatterboxThePageOffersTheDownloadAndStillAddsVoices()
    {
        using var look = AppLook.Put();
        _chatterboxInstalled = false;

        var (page, window, _) = Shown(1200);

        var notice = Named<Notice>(page, "Chatterbox");
        Assert.True(notice.IsVisible);
        Assert.True(Named<Button>(page, "DownloadChatterbox").IsEffectivelyVisible);
        Assert.True(Named<Button>(page, "ChooseFile").IsEnabled);

        Capture(window, "custom-voices-not-downloaded.png");
        window.Close();
    }

    private static void Pick(Control page, string row, int option)
    {
        Named<Segment>(page, row).GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Decoder : IAudioFileDecoder
    {
        public DecodedSamples? Next { get; set; }

        public string? Failure { get; set; }

        public DecodedSamples Decode(string path) =>
            Failure is { } failure ? throw new AudioDecodeException(failure) : Next!;
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => [.. plaintext];

        public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
        {
            plaintext = [.. ciphertext];
            return true;
        }
    }
}
