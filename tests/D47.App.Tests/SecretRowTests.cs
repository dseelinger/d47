using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Two things about the language model card that are read at a glance, usually at the moment something
/// is not working: whether a key is stored, and whether there is an endpoint worth thinking about.
/// </summary>
public class SecretRowTests
{
    /// <summary>The one thing this row is asked.</summary>
    [AvaloniaFact]
    public void WhetherAKeyIsStoredIsStatedInBothWordsAndColour()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.Contains("No key", Texts(host));
        Assert.DoesNotContain("Key stored", Texts(host));

        settings.Apply("llm.anthropic.apiKey", "sk-not-a-real-key", SettingsCaller.Panel);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains("Key stored", Texts(host));

        // And the box stops inviting a first key once there is one to replace.
        Assert.Contains(
            host.View.GetVisualDescendants().OfType<TextBox>(),
            box => box.PlaceholderText == "Paste a new key to replace it");

        host.Close();
    }

    [AvaloniaFact]
    public void VerifyIsShutUntilAKeyHasBeenTyped()
    {
        var (settings, editor, _) = Editor();
        var window = Showing(editor);

        var verify = Verify(editor);

        Assert.True(verify.IsVisible);
        Assert.False(verify.IsEnabled);
        Assert.Equal("Paste a key first — there is nothing here to check yet", ToolTip.GetTip(verify));

        Box(editor).Text = "sk-ant-api03-not-a-real-key";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(verify.IsEnabled);

        // And a stored key does not bring it back on its own.
        settings.Apply("llm.anthropic.apiKey", "sk-ant-api03-also-not-real", SettingsCaller.Panel);
        Box(editor).Text = string.Empty;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(verify.IsEnabled);

        window.Close();
    }

    /// <summary>And it checks what was typed, which means storing it first.</summary>
    [AvaloniaFact]
    public async Task PressingVerifyStoresWhatWasTypedAndThenChecksIt()
    {
        var checks = 0;
        var (settings, editor, secrets) = Editor(() => checks++);
        var window = Showing(editor);

        Box(editor).Text = "  sk-ant-api03-not-a-real-key\n";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Verify(editor).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, checks);
        Assert.True(secrets.Has(SecretName(settings)), "the typed key was checked without being stored.");

        // Stored means emptied, and an emptied box means the button is shut again.
        Assert.Empty(Box(editor).Text ?? string.Empty);
        Assert.False(Verify(editor).IsEnabled);

        window.Close();
    }

    /// <summary>The real Anthropic key row with a check bolted to it.</summary>
    private static (SettingsService Settings, SecretEditor Editor, SecretStore Secrets) Editor(
        Action? onCheck = null)
    {
        var (settings, _, _, _, secrets) = TestSurface.CreateFull();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        settings.Apply(
            ConversationCapability.ProviderKey, LlmProviderCatalog.AnthropicId, SettingsCaller.Panel);

        var row = settings.Sections
            .SelectMany(section => section.Rows)
            .First(candidate => candidate.Key == "llm.anthropic.apiKey");

        var checkable = row with
        {
            Verify = _ =>
            {
                onCheck?.Invoke();
                return Task.FromResult(SecretCheck.Works("Anthropic answered."));
            },
        };

        return (settings, new SecretEditor(checkable, settings), secrets);
    }

    private static string SecretName(SettingsService settings) =>
        settings.Sections.SelectMany(section => section.Rows)
            .First(row => row.Key == "llm.anthropic.apiKey").SecretName!;

    private static Window Showing(Control content)
    {
        var window = new Window { Content = content };

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Button Verify(SecretEditor editor) =>
        editor.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Verify Key");

    private static TextBox Box(SecretEditor editor) =>
        editor.GetVisualDescendants().OfType<TextBox>().First();

    /// <summary>Anthropic has one address and no reason to accept another, so the row is not offered.</summary>
    [Fact]
    public void AProviderWithNowhereElseToPointOffersNoEndpointRow()
    {
        var anthropic = LlmProviderCatalog.Find(LlmProviderCatalog.AnthropicId);

        Assert.NotNull(anthropic);
        Assert.True(anthropic.HasEndpoint, "it does have an address");
        Assert.False(anthropic.AcceptsCustomEndpoint, "but retyping it is not a setting");
    }

    [AvaloniaFact]
    public void TheEndpointRowIsAbsentFromTheSurfaceForSuchAProvider()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Apply(ConversationCapability.ProviderKey, LlmProviderCatalog.AnthropicId, SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.DoesNotContain("Endpoint", Texts(host));

        host.Close();
    }

    /// <summary>What is actually on screen.</summary>
    private static List<string> Texts(SettingsHost host) =>
    [
        .. host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0),
    ];
}
