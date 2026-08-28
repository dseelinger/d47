using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The guided key setup, on the surface (Phase 16). The decision itself is covered in
/// Core; what is asserted here is that the window shows the real rows, discloses egress, and can
/// be reached again from About.
/// </summary>
public class FirstRunKeysTests
{
    private static FirstRunWindow Guide(params string[] optionalKeys)
    {
        var (settings, _, _, registry, secrets) = TestSurface.CreateFull();
        var provider = LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

        // Selected for real, because the egress lines are computed from settings: with the
        // default "none" provider the model destination is correctly reported as sending
        // nothing, which is a true sentence about a Commander this guide is never shown to.
        settings.Apply(ConversationCapability.ProviderKey, provider.Id, SettingsCaller.Panel);

        var steps = FirstRun.Steps(
            registry,
            settings.Current,
            provider,
            secrets.Has,
            ConversationCapability.KeyRowFor(provider),
            optionalKeys);

        return new FirstRunWindow(steps, settings);
    }

    /// <summary>
    /// The window shows the real key controls rather than a re-authored copy of them — the same
    /// <see cref="SecretEditor"/> the settings surface builds.
    /// </summary>
    [AvaloniaFact]
    public void TheGuideShowsTheSameKeyControlAsSettings()
    {
        var window = Guide(SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs));
        window.Show();

        var editors = window.GetVisualDescendants().OfType<SecretEditor>().ToList();

        Assert.Equal(2, editors.Count);
        window.Close();
    }

    /// <summary>
    /// Every key says what it sends and where. Computed from <see cref="EgressDisclosure"/>, so
    /// this asserts the sentence reached the screen rather than that somebody typed one.
    /// </summary>
    [AvaloniaFact]
    public void EveryKeyDisclosesWhatLeavesTheMachine()
    {
        var window = Guide(SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs));
        window.Show();

        var text = string.Join(
            "\n",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? ""));

        Assert.Contains("api.anthropic.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("elevenlabs", text, StringComparison.OrdinalIgnoreCase);
        window.Close();
    }

    /// <summary>
    /// The key row with its two glyphs in it, for a human to look at. They are drawn in repo as
    /// path data rather than taken from a font, and path data is the kind of thing that compiles,
    /// lays out, passes every assertion here and still looks wrong — so "does the eye read as an
    /// eye" gets an artifact to answer with, the same way the speech model row does.
    /// <para>
    /// Both states, because the struck-through eye is a second drawing and it is the one that
    /// only appears once a Commander has pressed something.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheKeyRowGlyphsAreDrawnForLookingAt()
    {
        var (settings, _, _, registry, secrets) = TestSurface.CreateFull();
        var provider = LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

        settings.Apply(ConversationCapability.ProviderKey, provider.Id, SettingsCaller.Panel);

        // The glyphs take their colour from a theme resource, so a capture with no theme manager
        // is a capture of two unpainted paths. Nothing about the drawing, everything about the
        // harness — but it looks identical to the drawing being wrong.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var steps = FirstRun.Steps(
            registry, settings.Current, provider, secrets.Has,
            ConversationCapability.KeyRowFor(provider), []);

        var window = new FirstRunWindow(steps, settings);
        window.Show();

        var editor = window.GetVisualDescendants().OfType<SecretEditor>().First();
        var box = editor.GetVisualDescendants().OfType<TextBox>().First();

        // Something in the box, or the clear glyph is correctly hidden and the capture shows one
        // control where the point is to see both.
        box.Text = "sk-ant-api03-not-a-real-key";

        Capture(window, "secret-row-masked");

        var reveal = editor.GetVisualDescendants().OfType<ToggleButton>().First();
        reveal.IsChecked = true;

        Capture(window, "secret-row-revealed");

        window.Close();
    }

    private static void Capture(Window window, string name)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"{name}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    /// <summary>
    /// Nothing here is a wall: the window closes on its own button, and declining is simply
    /// closing it. Capabilities are state rather than guards (Phase 3).
    /// </summary>
    [AvaloniaFact]
    public void ItCanBeClosedWithoutStoringAnything()
    {
        var (settings, _, _, registry, secrets) = TestSurface.CreateFull();
        var provider = LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

        var steps = FirstRun.Steps(
            registry, settings.Current, provider, secrets.Has,
            ConversationCapability.KeyRowFor(provider), []);

        var window = new FirstRunWindow(steps, settings);
        window.Show();

        var done = window.GetVisualDescendants().OfType<Button>()
            .First(button => (button.Content as string) == "Done");

        done.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.False(secrets.Has(provider.KeySecretName!));
    }

    /// <summary>
    /// The key setup is reachable again after first run, because <b>keys get rotated and
    /// revoked</b> — so the state that triggers the guide is one a working install can return to.
    /// <para>
    /// It used to be a button on <c>AboutWindow</c>. About is a settings area now
    /// (<a href="https://github.com/dseelinger/d47/issues/50">#50</a>), so the offer is a row, and
    /// a caller with nothing to open still gets no row rather than a button that does nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void AboutOffersTheKeySetupAgain()
    {
        var (_, _, paths) = TestSurface.Create();
        var opened = 0;

        var about = D47.Core.Capabilities.Builtin.AboutCapability.Create(
            paths,
            "1.2.3",
            "1.2.3+abcdef0",
            setUpKeys: () => opened++);

        var row = about.Settings.Single(
            candidate => candidate.Key == D47.Core.Capabilities.Builtin.AboutCapability.SetUpKeysKey);

        Assert.NotNull(row.Press);
        row.Press!();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void AboutHidesTheButtonWhenThereIsNothingToOpen()
    {
        var (_, _, paths) = TestSurface.Create();

        var about = D47.Core.Capabilities.Builtin.AboutCapability.Create(
            paths,
            "1.2.3",
            "1.2.3+abcdef0");

        Assert.DoesNotContain(
            about.Settings,
            row => row.Key == D47.Core.Capabilities.Builtin.AboutCapability.SetUpKeysKey);
    }
}
