using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The first-run setup wizard, on the surface.</summary>
public class TheSetupWizardSavesOnlyOnStartTests
{
    private static readonly string[] SavedKeys =
    [
        ConversationCapability.ProviderKey,
        SpeechCapability.ProviderKey,
        ListeningCapability.ProviderKey,
        ListeningCapability.PushToTalkKeyKey,
        ListeningCapability.PushToTalkButtonKey,
        ListeningCapability.ModeKey,
    ];

    private static (SetupWizard Wizard, Window Host, SettingsService Settings, SecretStore Secrets) Open(
        Func<EliteBinds>? binds = null,
        Action<SettingsService>? before = null)
    {
        var (settings, _, _, registry, secrets) = TestSurface.CreateFull();
        before?.Invoke(settings);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var wizard = new SetupWizard(settings, binds);
        var host = wizard.Show();

        return (wizard, host, settings, secrets);
    }

    private static void Go(SetupWizard wizard, SetupWizard.Step step)
    {
        wizard.Go(step);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Control root, string name)
    {
        var button = root.GetVisualDescendants().OfType<Button>().First(candidate => candidate.Name == name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static string Text(Control root) => string.Join(
        "\n",
        root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    private static IReadOnlyList<Button> Options(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Name?.StartsWith("SetupOption-", StringComparison.Ordinal) == true)];

    private static string? Selected(Control root) =>
        Options(root).SingleOrDefault(button => button.Classes.Contains(ListRow.SelectedClass))?.Name?["SetupOption-".Length..];

    [AvaloniaFact]
    public void EachProviderStepListsItsWholeCatalogWithTheDefaultsChosen()
    {
        var (wizard, host, _, _) = Open();

        Go(wizard, SetupWizard.Step.Conversation);
        Assert.Equal(LlmProviderCatalog.All.Count, Options(wizard).Count);
        Assert.Equal(LlmProviderCatalog.AnthropicId, Selected(wizard));

        Go(wizard, SetupWizard.Step.Voice);
        Assert.Equal(TtsProviderCatalog.All.Count, Options(wizard).Count);
        Assert.Equal(TtsProviderCatalog.EdgeId, Selected(wizard));

        Go(wizard, SetupWizard.Step.Listening);
        Assert.Equal(SttProviderCatalog.All.Count, Options(wizard).Count);
        Assert.Equal(SttProviderCatalog.LocalId, Selected(wizard));

        host.Close();
    }

    [AvaloniaFact]
    public void ReopenedItPreselectsTheCurrentSettings()
    {
        var (wizard, host, _, _) = Open(before: settings =>
            settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.KokoroId, SettingsCaller.Panel));

        Go(wizard, SetupWizard.Step.Voice);
        Assert.Equal(TtsProviderCatalog.KokoroId, Selected(wizard));

        host.Close();
    }

    [AvaloniaFact]
    public void ASharedKeyGetsOneEditor()
    {
        var (wizard, host, settings, _) = Open();

        Go(wizard, SetupWizard.Step.Conversation);
        Press(wizard, $"SetupOption-{LlmProviderCatalog.OpenAiId}");
        Go(wizard, SetupWizard.Step.Voice);
        Press(wizard, $"SetupOption-{TtsProviderCatalog.OpenAiId}");
        Go(wizard, SetupWizard.Step.Listening);
        Press(wizard, $"SetupOption-{SttProviderCatalog.OpenAiId}");

        Go(wizard, SetupWizard.Step.Keys);

        Assert.Single(wizard.GetVisualDescendants().OfType<SecretEditor>());

        // Choosing is not saving.
        Assert.Equal(LlmProviderCatalog.AnthropicId, settings.Current.Llm.Provider);

        host.Close();
    }

    [AvaloniaFact]
    public void TheKeyLinesComeFromTheDisclosure()
    {
        var (wizard, host, settings, _) = Open();

        Go(wizard, SetupWizard.Step.Voice);
        Press(wizard, $"SetupOption-{TtsProviderCatalog.ElevenLabsId}");
        Go(wizard, SetupWizard.Step.Keys);

        var lines = wizard.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.Name == "SetupKeyEgress")
            .Select(block => block.Text)
            .ToList();

        var anthropic = FirstRun.Egress(SetupSlot.Conversation, FirstRun.Draft(settings, wizard.Choices));
        var voice = EgressDisclosure.TextToSpeechFor(TtsProviderCatalog.ElevenLabs);

        Assert.Equal(
            [$"{anthropic.Destination} · {anthropic.Summary}", $"{voice.Destination} · {voice.Summary}"],
            lines);

        host.Close();
    }

    [AvaloniaFact]
    public void TheKeysStepIsPassedOverWhenNothingNeedsAKey()
    {
        var (wizard, host, _, _) = Open();

        Go(wizard, SetupWizard.Step.Conversation);
        Press(wizard, $"SetupOption-{LlmProviderCatalog.NoneId}");
        Go(wizard, SetupWizard.Step.Listening);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.TalkButton, wizard.Current);

        Press(wizard, "SetupBack");
        Assert.Equal(SetupWizard.Step.Listening, wizard.Current);

        host.Close();
    }

    [AvaloniaFact]
    public void SkipSetupChangesNoSetting()
    {
        var (wizard, host, settings, _) = Open();
        var before = SavedKeys.Select(settings.Read).ToList();

        Go(wizard, SetupWizard.Step.Conversation);
        Press(wizard, $"SetupOption-{LlmProviderCatalog.NoneId}");
        Go(wizard, SetupWizard.Step.Voice);
        Press(wizard, $"SetupOption-{TtsProviderCatalog.KokoroId}");
        Go(wizard, SetupWizard.Step.TalkButton);
        Press(wizard, "SetupKeyClear");

        Press(wizard, "SetupSkip");

        Assert.False(wizard.Started);
        Assert.Equal(before, SavedKeys.Select(settings.Read));
        Assert.Empty(host.Modals());
    }

    [AvaloniaFact]
    public void StartSavesAKeylessChoiceAsItsFreeOne()
    {
        var (wizard, host, settings, secrets) = Open();

        Go(wizard, SetupWizard.Step.Voice);
        Press(wizard, $"SetupOption-{TtsProviderCatalog.KokoroId}");
        Go(wizard, SetupWizard.Step.Ready);
        Press(wizard, "SetupNext");

        Assert.True(wizard.Started);
        Assert.Equal(LlmProviderCatalog.NoneId, settings.Current.Llm.Provider);
        Assert.Equal(TtsProviderCatalog.KokoroId, settings.Current.Speech.Provider);
        Assert.False(FirstRun.IsNeeded(LlmProviderCatalog.Selected(settings.Current.Llm.Provider), secrets.Has));
        Assert.Empty(host.Modals());
    }

    [AvaloniaFact]
    public void ACollidingTalkKeyNamesTheEliteAction()
    {
        var binds = new EliteBinds
        {
            PresetName = "Custom",
            SourceFile = "Custom.4.2.binds",
            Bindings = [new EliteBinding("UseBoostJuice", "Primary", "Keyboard", "Key_F9")],
        };

        var (wizard, host, _, _) = Open(
            () => binds,
            settings => settings.Apply(ListeningCapability.PushToTalkKeyKey, "F9", SettingsCaller.Panel));

        Go(wizard, SetupWizard.Step.TalkButton);

        var warning = wizard.GetVisualDescendants().OfType<Notice>().Single(notice => notice.Name == "SetupCollision");
        Assert.Contains("UseBoostJuice", warning.Text, StringComparison.Ordinal);

        host.Close();
    }

    [AvaloniaFact]
    public void TheModesAreHoldToggleAlwaysOnAndWakeWord()
    {
        var (wizard, host, _, _) = Open();

        Go(wizard, SetupWizard.Step.TalkButton);

        var segment = wizard.GetVisualDescendants().OfType<Segment>().Single(control => control.Name == "SetupMode");
        Assert.Equal(["Hold", "Toggle", "Always on", "Wake word"], segment.ItemsSource);
        Assert.Equal(
            [ListeningCapability.HoldMode, ListeningCapability.ToggleMode, ListeningCapability.ContinuousMode, ListeningCapability.WakeMode],
            SetupWizard.TalkModes.Select(entry => entry.Mode));

        host.Close();
    }

    public static TheoryData<SetupWizard.Step> Steps() => new(Enum.GetValues<SetupWizard.Step>());

    /// <summary>No step is wider than a modal, and a tall one scrolls its body under a fixed footer.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Steps))]
    public void EveryStepFitsTheModal(SetupWizard.Step step)
    {
        using var look = AppLook.Put();
        var (wizard, host, _, _) = Open();

        // The choices that make the keys and ready steps longest.
        Go(wizard, SetupWizard.Step.Voice);
        Press(wizard, $"SetupOption-{TtsProviderCatalog.ElevenLabsId}");
        Go(wizard, SetupWizard.Step.Listening);
        Press(wizard, $"SetupOption-{SttProviderCatalog.DeepgramId}");

        Go(wizard, step);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(wizard.Bounds.Width <= Modal.Width, $"{step} is {wizard.Bounds.Width} wide");
        Assert.True(wizard.Bounds.Height <= wizard.MaxHeight, $"{step} is {wizard.Bounds.Height} tall");

        var next = wizard.GetVisualDescendants().OfType<Button>().First(button => button.Name == "SetupNext");
        var bottom = next.TranslatePoint(new Point(0, next.Bounds.Height), wizard);
        Assert.NotNull(bottom);
        Assert.True(bottom!.Value.Y <= wizard.Bounds.Height, $"{step}'s footer is pushed out of the modal");

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoStepSaysLanguageModel(bool keysStored)
    {
        var (wizard, host, _, secrets) = Open();

        if (keysStored)
        {
            secrets.Set("anthropic.apiKey", "sk-test");
            secrets.Set("openai.apiKey", "sk-test");

            Go(wizard, SetupWizard.Step.Conversation);
            Press(wizard, $"SetupOption-{LlmProviderCatalog.OpenAiId}");
        }

        foreach (var step in Enum.GetValues<SetupWizard.Step>())
        {
            Go(wizard, step);
            Assert.DoesNotContain("language model", Text(wizard), StringComparison.OrdinalIgnoreCase);
        }

        host.Close();
    }

    [AvaloniaFact]
    public void ARefusedSettingKeepsTheWizardOpenAndSaysWhy()
    {
        var (wizard, host, settings, _) = Open();

        // A mode no row accepts.
        Go(wizard, SetupWizard.Step.Ready);
        typeof(SetupWizard).GetProperty(nameof(SetupWizard.Choices))!
            .SetValue(wizard, wizard.Choices with { TalkMode = "sideways" });

        Press(wizard, "SetupNext");

        Assert.False(wizard.Started);
        Assert.Single(host.Modals());
        Assert.Contains(
            wizard.GetVisualDescendants().OfType<Notice>(),
            notice => notice.Name == "SetupRefused" && notice.Text?.Contains("sideways", StringComparison.Ordinal) == true);

        host.Close();
    }

    /// <summary>Each step, for a human to look at.</summary>
    [AvaloniaFact]
    public void EveryStepIsDrawnForLookingAt()
    {
        using var look = AppLook.Put();
        var (wizard, host, _, _) = Open();

        Go(wizard, SetupWizard.Step.Voice);
        Press(wizard, $"SetupOption-{TtsProviderCatalog.ElevenLabsId}");

        foreach (var step in Enum.GetValues<SetupWizard.Step>())
        {
            Go(wizard, step);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            host.CaptureRenderedFrame()!.Save(
                Path.Combine(TestSurface.CaptureDirectory, $"setup-{(int)step + 1}-{step}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        host.Close();
    }
}
