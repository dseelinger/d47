using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
/// Two things about the language model card that are read at a glance, usually at the moment
/// something is not working: whether a key is stored, and whether there is an endpoint worth
/// thinking about.
/// </summary>
public class SecretRowTests
{
    /// <summary>
    /// The one thing this row is asked. It used to answer in muted eleven-point text at the far
    /// end of the row, after two buttons.
    /// </summary>
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

    /// <summary>
    /// Anthropic has one address and no reason to accept another, so the row is not offered.
    /// Having a default endpoint and being pointable somewhere else are different facts.
    /// </summary>
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

    /// <summary>
    /// What is actually on screen. A row that does not apply is hidden rather than removed from
    /// the tree, so a test that walked the visual tree without asking about visibility would
    /// find every row that has ever existed and pass regardless.
    /// </summary>
    private static List<string> Texts(SettingsHost host) =>
    [
        .. host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0),
    ];
}
