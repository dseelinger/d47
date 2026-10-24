using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Asset Mgmt › Characters: the captain, the tower, the story's cast and the Commander's own characters (#1603).</summary>
public class TheCharactersPageKeepsEveryoneYouCanTalkToTests
{
    private sealed record Surface(
        Window Window, CharactersPage Page, CharacterStore Store, SettingsService Settings, PanelNavigator Nav, IDisposable Look);

    private static Surface Open(
        bool big = false,
        RunningCast? story = null,
        Func<string, string, CancellationToken, Task<double?>>? distance = null,
        bool galaxy = false)
    {
        var look = AppLook.Put();
        var settings = TestSurface.Settings();

        if (galaxy)
        {
            Assert.True(settings.Apply("knowledge.galaxy", "true", SettingsCaller.Panel).Ok);
        }

        var store = new CharacterStore(
            Path.Combine(TestSurface.MemoryFolder("characters"), "characters.json"),
            new MemoryFileSystem(),
            NullLogger<CharacterStore>.Instance);

        var state = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-10-24T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-10-24T09:00:01Z","event":"Location","StarSystem":"Sol","StarPos":[0,0,0],"SystemAddress":10477373803}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        var host = new CharactersHost(
            () => store,
            settings,
            _ => story,
            distance ?? ((_, _, _) => Task.FromResult<double?>(112.6)));

        var nav = new PanelNavigator();
        nav.Register(PanelTab.Assets, new NavCrumb(CharactersPage.RootKey, "Characters"));
        Assert.True(nav.Select(PanelTab.Assets));

        var prompts = new PanelPrompts(nav, new Avalonia.Controls.Panel());
        var page = new CharactersPage(host, () => state.Active, prompts, big: big);
        var window = new Window
        {
            Content = page,
            Width = big ? AppLook.CaptureWidth : 1440,
            Height = big ? AppLook.CaptureHeight : 900,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, page, store, settings, nav, look);
    }

    private static void Close(Surface surface)
    {
        surface.Window.Close();
        surface.Look.Dispose();
    }

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static T Named<T>(Control page, string name)
        where T : Control =>
        page.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static bool Has<T>(Control page, string name)
        where T : Control =>
        page.GetVisualDescendants().OfType<T>().Any(control => control.Name == name);

    private static void Press(Control page, string name)
    {
        Named<Button>(page, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Control page, string name, string text)
    {
        var box = Named<TextBox>(page, name);

        box.Text = text;
        box.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs UI jobs until <paramref name="task"/> completes; the lookup's last step is posted to the UI thread.</summary>
    private static void Settle(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(task.IsCompleted, "the distance lookup did not settle");
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Surface surface, string name)
    {
        using var frame = surface.Window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    private static RunningCast Story()
    {
        StoryCastMember Member(string id, string name) => new(
            "salt-road",
            "Salt Road",
            new StorySpeakerShown(id, name, "edge", "voice", $"salt-road.{id}") { Primary = true },
            new PinnedVoice("edge", "voice"),
            new PinnedVoice("edge", "voice"));

        return new RunningCast("salt-road", "Salt Road", [Member("wren", "Wren Calloway"), Member("ilse", "Ilse Marr")]);
    }

    [AvaloniaFact]
    public void TheCaptainAndTheTowerAreAlwaysListedAndTheStoryGroupSaysNoneIsRunning()
    {
        var surface = Open();
        var text = Text(surface.Page);

        Assert.Contains("CAPTAIN", text.Select(line => line.ToUpperInvariant()));
        Assert.Contains("TOWER CONTROL", text.Select(line => line.ToUpperInvariant()));
        Assert.Contains(CharactersPage.NoStory, text);
        Assert.Equal(CharactersPage.CaptainKey, surface.Page.Selected);

        Close(surface);
    }

    [AvaloniaFact]
    public void ANamedCaptainIsListedByNameWithTheWordCaptainBesideIt()
    {
        var surface = Open();

        Assert.True(surface.Settings.Apply("speech.carrierCaptainName", "Arden Holt", SettingsCaller.Panel).Ok);
        Assert.True(surface.Page.Tick());
        Dispatcher.UIThread.RunJobs();

        var text = Text(surface.Page).Select(line => line.ToUpperInvariant()).ToList();

        Assert.Contains("ARDEN HOLT", text);
        Assert.Contains("CAPTAIN", text);

        Close(surface);
    }

    [AvaloniaFact]
    public void TheCaptainsNameIsTheSettingAndNotesBelongToTheCommander()
    {
        var surface = Open();

        Type(surface.Page, CharactersPage.NameBoxName, "Arden Holt");
        Type(surface.Page, CharactersPage.NotesBoxName, "Dry and unhurried.");

        Assert.Equal("Arden Holt", surface.Settings.Read("speech.carrierCaptainName"));
        Assert.Equal("Dry and unhurried.", surface.Store.NotesFor("F1", CarrierCharacter.Captain));
        Assert.True(Has<Button>(surface.Page, CharactersPage.ResetName));

        Press(surface.Page, CharactersPage.ResetName);

        Assert.Null(surface.Settings.Read("speech.carrierCaptainName"));

        Close(surface);
    }

    [AvaloniaFact]
    public void ACharacterIsAddedEditedAndRemovedAndTheStoreHoldsTheResult()
    {
        var surface = Open();

        Press(surface.Page, CharactersPage.AddName);

        Assert.Equal(CharactersPage.DraftKey, surface.Page.Selected);
        Assert.Contains("Joins the CHARACTERS channel once saved.", Text(surface.Page));

        Press(surface.Page, CharactersPage.SaveName);

        Assert.Contains("Give this character a name.", Text(surface.Page));
        Assert.Empty(surface.Store.CharactersOf("F1"));

        Type(surface.Page, CharactersPage.NameBoxName, "Harlan Voss");
        Named<CheckBox>(surface.Page, CharactersPage.AboardName).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Type(surface.Page, CharactersPage.NotesBoxName, "Owes you a favour.");
        Press(surface.Page, CharactersPage.SaveName);

        var saved = Assert.Single(surface.Store.CharactersOf("F1"));

        Assert.Equal("Harlan Voss", saved.Name);
        Assert.True(saved.Aboard);
        Assert.Equal("Owes you a favour.", saved.Notes);
        Assert.Equal("custom:" + saved.Id, surface.Page.Selected);

        Type(surface.Page, CharactersPage.NameBoxName, "Harlan");

        Assert.Equal("Harlan", Assert.Single(surface.Store.CharactersOf("F1")).Name);

        Press(surface.Page, CharactersPage.RemoveName);

        Assert.Contains("REMOVE CHARACTER", Text(surface.Page));
        Assert.Single(surface.Store.CharactersOf("F1"));

        Press(surface.Page, CharactersPage.RemoveCancelName);

        Assert.Single(surface.Store.CharactersOf("F1"));

        Press(surface.Page, CharactersPage.RemoveName);
        Press(surface.Page, CharactersPage.RemoveConfirmName);

        Assert.Empty(surface.Store.CharactersOf("F1"));
        Assert.Equal(CharactersPage.CaptainKey, surface.Page.Selected);

        Close(surface);
    }

    [AvaloniaFact]
    public void ARuleThatIsBrokenIsNotSavedAndTheFieldSaysWhy()
    {
        var surface = Open();

        Assert.Null(surface.Store.Set("F1", new CustomCharacter("0a1b2c3d", "Mara Quill", null, null, true, null)));
        surface.Page.Select("custom:0a1b2c3d");
        Assert.Null(surface.Store.Set("F1", new CustomCharacter("1a1b2c3d", "Harlan Voss", null, null, true, null)));
        surface.Page.Select("custom:1a1b2c3d");

        Type(surface.Page, CharactersPage.NameBoxName, "mara quill");

        Assert.Equal("Harlan Voss", surface.Store.Find("F1", "1a1b2c3d")!.Name);
        Assert.Contains("You already have a character named mara quill.", Text(surface.Page));

        Type(surface.Page, CharactersPage.NameBoxName, new string('x', 33));

        Assert.Contains("Keep the name to 32 characters.", Text(surface.Page));

        Named<CheckBox>(surface.Page, CharactersPage.AboardName).IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Name the system they are in, or check Aboard.", Text(surface.Page));
        Assert.True(Named<CheckBox>(surface.Page, CharactersPage.AboardName).IsChecked);

        Close(surface);
    }

    [AvaloniaFact]
    public void ARefusedFieldKeepsItsFullHeightAboveTheReason()
    {
        var surface = Open();

        Assert.Null(surface.Store.Set("F1", new CustomCharacter("0a1b2c3d", "Babalooey", null, null, true, null)));
        surface.Page.Select("custom:0a1b2c3d");

        Named<CheckBox>(surface.Page, CharactersPage.AboardName).IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        var system = Named<TextBox>(surface.Page, CharactersPage.SystemBoxName);

        Assert.Equal("Name the system they are in, or check Aboard.", FieldMessage.GetText(system));
        var border = Named<Border>(system, "PART_BorderElement");
        var reason = Named<DockPanel>(system, "PART_Message");
        var boxBottom = border.TranslatePoint(new Point(0, border.Bounds.Height), system)!.Value.Y;
        var reasonTop = reason.TranslatePoint(default, system)!.Value.Y;

        Assert.True(border.Bounds.Height >= TypeScale.MinimumTarget, $"the System box is {border.Bounds.Height}px tall.");
        Assert.True(reasonTop >= boxBottom, $"the reason starts at {reasonTop}px, inside the box, which ends at {boxBottom}px.");

        Capture(surface, "characters-system-refused.png");
        Close(surface);
    }

    [AvaloniaFact]
    public void AControlFromAnEarlierDrawSavesOverWhatIsStoredNow()
    {
        var surface = Open();

        Assert.Null(surface.Store.Set("F1", new CustomCharacter("0a1b2c3d", "Babalooey", null, null, true, null)));
        surface.Page.Select("custom:0a1b2c3d");

        Named<CheckBox>(surface.Page, CharactersPage.AboardName).IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        var stale = Named<CheckBox>(surface.Page, CharactersPage.AboardName);

        Type(surface.Page, CharactersPage.SystemBoxName, "Synuefe JI-W b49-11");

        stale.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        var saved = surface.Store.Find("F1", "0a1b2c3d")!;

        Assert.False(saved.Aboard);
        Assert.Equal("Synuefe JI-W b49-11", saved.System);
        Assert.DoesNotContain("Name the system they are in, or check Aboard.", Text(surface.Page));
        Assert.Null(FieldMessage.GetText(Named<TextBox>(surface.Page, CharactersPage.SystemBoxName)));

        Close(surface);
    }

    [AvaloniaFact]
    public void ACharacterAwayShowsItsSystemAndTheDistanceFromHere()
    {
        var surface = Open(galaxy: true);

        Assert.Null(surface.Store.Set("F1", new CustomCharacter("0a1b2c3d", "Harlan Voss", null, null, false, "Shinrarta Dezhra")));
        surface.Page.Select("custom:0a1b2c3d");
        Settle(surface.Page.DistanceSettled);

        Assert.Equal("Shinrarta Dezhra", Named<TextBox>(surface.Page, CharactersPage.SystemBoxName).Text);
        Assert.Equal("112.6 ly", Named<TextBlock>(surface.Page, CharactersPage.DistanceName).Text);
        Assert.Contains("Out of comms range beyond 500 ly.", Text(surface.Page));

        Capture(surface, "characters-window.png");
        Close(surface);
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffTheDistanceIsUnknownAndSaysSo()
    {
        var surface = Open();

        Assert.Null(surface.Store.Set("F1", new CustomCharacter("0a1b2c3d", "Harlan Voss", null, null, false, "Shinrarta Dezhra")));
        surface.Page.Select("custom:0a1b2c3d");

        Assert.Equal("unknown", Named<TextBlock>(surface.Page, CharactersPage.DistanceName).Text);
        Assert.Contains("Distance unknown with Galaxy search off.", Text(surface.Page));

        Close(surface);
    }

    [AvaloniaFact]
    public void AStoryCastMemberKeepsOnlyTheCommandersNotes()
    {
        var surface = Open(story: Story());

        Assert.Contains("STORY · SALT ROAD", Text(surface.Page).Select(line => line.ToUpperInvariant()));

        surface.Page.Select("story:wren");

        Assert.False(Has<TextBox>(surface.Page, CharactersPage.NameBoxName));
        Assert.False(Named<CheckBox>(surface.Page, CharactersPage.AboardName).IsEnabled);
        Assert.Contains("Kept for this story only.", Text(surface.Page));

        Type(surface.Page, CharactersPage.NotesBoxName, "Hides a limp.");

        Assert.Equal("Hides a limp.", surface.Store.CastNotes("F1", "salt-road", "wren"));

        Close(surface);
    }

    [AvaloniaFact]
    public void OnTheBigPanelNameAndNotesAreAskedForOnTheEntryPage()
    {
        var surface = Open(big: true);

        Assert.Null(surface.Store.Set("F1", new CustomCharacter("0a1b2c3d", "Mara Quill", null, "Your engineer.", true, null)));
        surface.Page.Select("custom:0a1b2c3d");

        Assert.False(Has<TextBox>(surface.Page, CharactersPage.NameBoxName));
        Assert.False(Has<TextBox>(surface.Page, CharactersPage.NotesBoxName));

        Press(surface.Page, CharactersPage.NameBoxName);

        Assert.Equal("character.name", surface.Nav.Trail[^1].Key);

        Capture(surface, "characters-big-panel.png");
        Close(surface);
    }
}
