using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Crew gives each hired pilot a picture kept under their crew id.</summary>
public sealed class EachHiredPilotKeepsTheirOwnPictureTests
{
    private static (CrewPage Page, SpeakerPictures Pictures, GameStateStore Store, Window Window) Open(Action<SpeakerPictures>? before, params string[] hires)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(TestSurface.Settings().Current.Ui.Theme);
        var paths = new AppPaths(TempFolders.Create("d47-crew-pictures"));
        paths.EnsureCreated();
        var pictures = new SpeakerPictures(paths);
        before?.Invoke(pictures);
        var store = new GameStateStore();

        string[] start = ["{\"timestamp\":\"2026-10-05T09:00:00Z\",\"event\":\"Commander\",\"FID\":\"F1\",\"Name\":\"Jameson\"}"];

        foreach (var line in start.Concat(hires))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var page = new CrewPage(() => store.Active, null, pictures);
        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (page, pictures, store, window);
    }

    private static string Hire(string name, int id) =>
        $$"""{"timestamp":"2026-10-05T09:01:00Z","event":"CrewHire","Name":"{{name}}","CrewID":{{id}},"CombatRank":"Dangerous"}""";

    private static void Choose(SpeakerPictures pictures, string name, Color color)
    {
        using var source = new MemoryStream(TheCommandersPictureReplacesTheDefaultTests.Jpeg(200, 100, color));
        Assert.Null(PictureImport.Save(source, "pilot.jpg", pictures.Chosen(name)));
    }

    private static List<Button> Buttons(Control page, string label) =>
        [.. page.GetVisualDescendants().OfType<Button>().Where(button => Equals(button.Content, label))];

    [AvaloniaFact]
    public void EveryHiredPilotHasTheirOwnPictureButtons()
    {
        var (page, _, _, window) = Open(null, Hire("Samira Voss", 1), Hire("Samira Voss", 2));

        Assert.Equal(2, Buttons(page, "Change picture").Count);
        Assert.Equal(2, Buttons(page, "Use the default").Count);
        window.Close();
    }

    [AvaloniaFact]
    public void TwoPilotsWithOneNameKeepSeparatePictures()
    {
        var (_, pictures, _, window) = Open(
            pictures => Choose(pictures, SpeakerPictures.Crew(1), Colors.SteelBlue), Hire("Samira Voss", 1), Hire("Samira Voss", 2));

        Assert.NotNull(pictures.Find(SpeakerPictures.Crew(1)));
        Assert.Null(pictures.Find(SpeakerPictures.Crew(2)));
        window.Close();
    }

    [AvaloniaFact]
    public void AChosenPictureShowsOnTheRowAndUseTheDefaultRemovesIt()
    {
        var (page, pictures, _, window) = Open(
            pictures => Choose(pictures, SpeakerPictures.Crew(2), Colors.Firebrick), Hire("Samira Voss", 1), Hire("Ilse Bruhn", 2));

        Assert.Single(page.GetVisualDescendants().OfType<Image>());

        var restore = Buttons(page, "Use the default").Single(button => button.IsEnabled);
        restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(File.Exists(pictures.Chosen(SpeakerPictures.Crew(2))));
        Assert.Empty(page.GetVisualDescendants().OfType<Image>());
        window.Close();
    }

    [AvaloniaFact]
    public void AFiredPilotsPictureIsKeptForARehire()
    {
        var (page, pictures, store, window) = Open(
            pictures => Choose(pictures, SpeakerPictures.Crew(1), Colors.SteelBlue), Hire("Samira Voss", 1));

        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-10-05T10:00:00Z","event":"CrewFire","Name":"Samira Voss","CrewID":1}""", NullLogger.Instance, out var fired));
        store.Apply(fired!);
        page.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(pictures.Find(SpeakerPictures.Crew(1)));

        Assert.True(JournalEvent.TryParse(Hire("Samira Voss", 1), NullLogger.Instance, out var hired));
        store.Apply(hired!);
        page.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(page.GetVisualDescendants().OfType<Image>());
        window.Close();
    }
}
