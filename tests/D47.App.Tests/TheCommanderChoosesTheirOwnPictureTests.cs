using Avalonia;
using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Commander record shows the picture kept for the Commander flying and offers the buttons that replace it;
/// before a Commander is known the buttons are disabled.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheCommanderChoosesTheirOwnPictureTests
{
    private static StandingPage Page(SpeakerPictures pictures, Func<string?> picture)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(TestSurface.Settings().Current.Ui.Theme);
        return new StandingPage(() => null, () => "JOHN DEPARAGON", new D47.App.Controls.JournalClock(() => null), pictures, picture);
    }

    private static List<Button> Buttons(Control page) => [.. page.GetVisualDescendants().OfType<Button>()];

    private static (SpeakerPictures Pictures, AppPaths Paths) Fresh()
    {
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "d47-commander-picture"));
        return (new SpeakerPictures(new MemoryFileSystem(), paths), paths);
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void WithNoFrontierIdTheButtonsAreDisabledAndSayWhy()
    {
        var (pictures, _) = Fresh();
        var page = Page(pictures, () => null);
        var window = new Window { Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = Buttons(page).Where(button => button.Content is "Change picture" or "Use the default").ToList();

        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, button => Assert.False(button.IsEnabled));
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Fly once with D47 running to set your picture.");
        window.Close();
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void TheCommandersFileShowsAndUseTheDefaultRemovesIt()
    {
        var (pictures, _) = Fresh();
        var name = SpeakerPictures.Commander("F1234");
        using (var source = new MemoryStream(TheCommandersPictureReplacesTheDefaultTests.Jpeg(300, 200, Avalonia.Media.Colors.SteelBlue)))
        {
            Assert.Null(PictureImport.Save(pictures.Files, source, "me.jpg", pictures.Chosen(name)));
        }

        var page = Page(pictures, () => name);
        var window = new Window { Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(page.GetVisualDescendants().OfType<Image>());

        Buttons(page).Single(button => Equals(button.Content, "Use the default")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(pictures.Files.Stat(pictures.Chosen(name)));
        Assert.Empty(page.GetVisualDescendants().OfType<Image>());
        window.Close();
    }

    [AvaloniaFact]
    public void ASecondCommanderHasNoPictureUntilTheyChooseOne()
    {
        var (pictures, _) = Fresh();
        using (var source = new MemoryStream(TheCommandersPictureReplacesTheDefaultTests.Jpeg(100, 100, Avalonia.Media.Colors.SteelBlue)))
        {
            Assert.Null(PictureImport.Save(pictures.Files, source, "me.jpg", pictures.Chosen(SpeakerPictures.Commander("F1"))));
        }

        Assert.NotNull(pictures.Find(SpeakerPictures.Commander("F1")));
        Assert.Null(pictures.Find(SpeakerPictures.Commander("F2")));
    }

    [AvaloniaFact]
    public void ARefusedFileWritesNothing()
    {
        var (pictures, _) = Fresh();
        var name = SpeakerPictures.Commander("F1234");
        using var huge = new MemoryStream(new byte[PictureImport.MostBytes + 1]);
        using var text = new MemoryStream("not a picture"u8.ToArray());

        Assert.NotNull(PictureImport.Save(pictures.Files, huge, "huge.png", pictures.Chosen(name)));
        Assert.NotNull(PictureImport.Save(pictures.Files, text, "notes.png", pictures.Chosen(name)));
        Assert.Null(pictures.Files.Stat(pictures.Chosen(name)));
        Assert.Null(pictures.Find(name));
    }
}
