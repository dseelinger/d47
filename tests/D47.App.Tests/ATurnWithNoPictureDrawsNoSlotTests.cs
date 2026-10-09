using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A turn whose picture has no file is laid out exactly as a turn with no picture at all; mini mode draws none; and a
/// full redraw never decodes a picture file twice (#770).
/// </summary>
public sealed class ATurnWithNoPictureDrawsNoSlotTests
{
    private static void Converse(PanelViewModel model)
    {
        model.Append("Good evening, Commander.", picture: "core.warden");
        model.Append("Where am I?", voice: TranscriptVoice.Commander);
        model.Append("Jump in fifteen minutes.", speaker: "Carrier", picture: "captain.woman");
    }

    private static IReadOnlyList<Rect> Rows(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").Children.Select(child => child.Bounds)];

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void ATurnWhosePictureHasNoFileIsLaidOutAsBefore()
    {
        var pictured = new SpeakerPictureFixture("d47-no-picture-no-slot");
        var (pictureWindow, picturePanel) = pictured.Open();
        Converse(pictured.Model);
        Dispatcher.UIThread.RunJobs();

        var plainModel = new PanelViewModel();
        var plainPanel = new PanelView { DataContext = plainModel };
        var plainWindow = new Window { Content = plainPanel, Width = 900, Height = 700 };
        plainWindow.Show();
        Converse(plainModel);
        Dispatcher.UIThread.RunJobs();

        Capture(pictureWindow, "conversation-picture-names-without-files.png");
        Capture(plainWindow, "conversation-without-pictures.png");

        Assert.Empty(SpeakerPictureFixture.Pictures(picturePanel));
        Assert.All(picturePanel.GetControl<StackPanel>("Bubbles").Children, child => Assert.IsNotType<StackPanel>(child));
        Assert.Equal(Rows(plainPanel), Rows(picturePanel));
    }

    [AvaloniaFact]
    public void PicturesSitOutsideTheBarOnEachSide()
    {
        var fixture = new SpeakerPictureFixture("d47-pictures-beside-the-bar");
        fixture.Ship("core.warden", Colors.OrangeRed);
        fixture.Ship("captain.woman", Colors.SeaGreen);
        fixture.Choose("commander.F123", Colors.SteelBlue);
        fixture.Model.CommanderPictureSource = () => "commander.F123";

        var (window, panel) = fixture.Open();
        Converse(fixture.Model);
        Dispatcher.UIThread.RunJobs();

        Capture(window, "conversation-with-pictures.png");

        var pictures = SpeakerPictureFixture.Pictures(panel);
        var rows = panel.GetControl<StackPanel>("Bubbles").Children;

        Assert.Equal(3, pictures.Count);
        Assert.All(pictures, picture => Assert.Equal(new Size(SpeakerPortraits.Size, SpeakerPortraits.Size), picture.Bounds.Size));

        // The ship's picture leads its row; the Commander's ends theirs.
        Assert.Same(pictures[0], ((StackPanel)rows[0]).Children[0]);
        Assert.Same(pictures[1], ((StackPanel)rows[1]).Children[^1]);
    }

    [AvaloniaFact]
    public void MiniModeDrawsNoPictures()
    {
        var fixture = new SpeakerPictureFixture("d47-mini-draws-no-pictures");
        fixture.Ship("core.warden", Colors.OrangeRed);

        var (_, panel) = fixture.Open();
        panel.Mode = PanelMode.Mini;
        fixture.Model.Append("Good evening, Commander.", picture: "core.warden");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(SpeakerPictureFixture.Pictures(panel));
    }

    [AvaloniaFact]
    public void TwentyTurnsDecodeEachDistinctFileOnce()
    {
        var fixture = new SpeakerPictureFixture("d47-pictures-decode-once");
        fixture.Ship("core.warden", Colors.OrangeRed);
        fixture.Ship("captain.man", Colors.SeaGreen);

        var (_, panel) = fixture.Open();

        // Each append adds a turn, so each is a full redraw of every turn before it.
        for (var turn = 0; turn < 20; turn++)
        {
            fixture.Model.Append($"Line {turn}.", picture: turn % 2 == 0 ? "core.warden" : "captain.man");
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(20, SpeakerPictureFixture.Pictures(panel).Count);
        Assert.Equal(2, fixture.Portraits.Decodes);
    }
}
