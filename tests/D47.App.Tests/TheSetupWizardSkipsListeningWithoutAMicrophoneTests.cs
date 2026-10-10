using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The setup wizard's route when the machine has no capture device.</summary>
[Trait("Category", "Integration")]
public class TheSetupWizardSkipsListeningWithoutAMicrophoneTests
{
    private static readonly string[] ListeningKeys =
    [
        ListeningCapability.ProviderKey,
        ListeningCapability.PushToTalkKeyKey,
        ListeningCapability.PushToTalkButtonKey,
        ListeningCapability.ModeKey,
    ];

    private static (SetupWizard Wizard, Window Host, SettingsService Settings) Open(
        Func<bool> hasMicrophone,
        Action<SettingsService>? before = null)
    {
        var (settings, _, _, _, _) = TestSurface.CreateFull();
        before?.Invoke(settings);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var wizard = new SetupWizard(settings, hasMicrophone: hasMicrophone);

        return (wizard, wizard.Show(), settings);
    }

    private static void Go(SetupWizard wizard, SetupWizard.Step step)
    {
        wizard.Go(step);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static Button? Find(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(candidate => candidate.Name == name);

    private static void Press(Control root, string name)
    {
        Find(root, name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static string Text(Control root) => string.Join(
        "\n",
        root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    private static void NoKeyNeeded(SetupWizard wizard)
    {
        Go(wizard, SetupWizard.Step.Conversation);
        Press(wizard, $"SetupOption-{LlmProviderCatalog.NoneId}");
    }

    [AvaloniaFact]
    public void WithNoMicrophoneVoiceLeadsToKeysAndThenReady()
    {
        var (wizard, host, _) = Open(() => false);

        Go(wizard, SetupWizard.Step.Voice);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.Keys, wizard.Current);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.Ready, wizard.Current);

        Press(wizard, "SetupBack");
        Assert.Equal(SetupWizard.Step.Keys, wizard.Current);

        Press(wizard, "SetupBack");
        Assert.Equal(SetupWizard.Step.Voice, wizard.Current);

        host.Close();
    }

    [AvaloniaFact]
    public void WithNoMicrophoneAndNoKeyNeededVoiceLeadsStraightToReady()
    {
        var (wizard, host, _) = Open(() => false);

        NoKeyNeeded(wizard);
        Go(wizard, SetupWizard.Step.Voice);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.Ready, wizard.Current);

        Press(wizard, "SetupBack");
        Assert.Equal(SetupWizard.Step.Voice, wizard.Current);

        host.Close();
    }

    [AvaloniaFact]
    public void WithAMicrophoneTheRouteIsUnchanged()
    {
        var (wizard, host, _) = Open(() => true);

        NoKeyNeeded(wizard);
        Go(wizard, SetupWizard.Step.Voice);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.Listening, wizard.Current);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.TalkButton, wizard.Current);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.Ready, wizard.Current);
        Assert.NotNull(Find(wizard, "SetupGoToTalkButton"));
        Assert.True(Find(wizard, "SetupGoToListening")!.IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void WithNoMicrophoneAKeyOnlyListeningNeedsIsNotAsked()
    {
        var (wizard, host, _) = Open(
            () => false,
            settings => settings.Apply(ListeningCapability.ProviderKey, SttProviderCatalog.DeepgramId, SettingsCaller.Panel));

        NoKeyNeeded(wizard);
        Go(wizard, SetupWizard.Step.Voice);

        Assert.Empty(wizard.Keys);

        Press(wizard, "SetupNext");
        Assert.Equal(SetupWizard.Step.Ready, wizard.Current);

        host.Close();
    }

    [AvaloniaFact]
    public void WithNoMicrophoneReadyShowsListeningUnselectableAndNoTalkButton()
    {
        var (wizard, host, _) = Open(() => false);

        Go(wizard, SetupWizard.Step.Ready);

        Assert.False(Find(wizard, "SetupGoToListening")!.IsEnabled);
        Assert.Contains("No microphone found", Text(wizard), StringComparison.OrdinalIgnoreCase);
        Assert.Null(Find(wizard, "SetupGoToTalkButton"));

        host.Close();
    }

    [AvaloniaFact]
    public void WithNoMicrophoneStartLeavesTheListeningSettingsAlone()
    {
        var (wizard, host, settings) = Open(
            () => false,
            settings => settings.Apply(ListeningCapability.ProviderKey, SttProviderCatalog.DeepgramId, SettingsCaller.Panel));
        var before = ListeningKeys.Select(settings.Read).ToList();

        NoKeyNeeded(wizard);
        Go(wizard, SetupWizard.Step.Ready);
        Press(wizard, "SetupNext");

        Assert.True(wizard.Started);
        Assert.Equal(before, ListeningKeys.Select(settings.Read));

        host.Close();
    }
}
