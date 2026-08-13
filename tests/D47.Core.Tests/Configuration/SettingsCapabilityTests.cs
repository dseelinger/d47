using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The model's reach over settings, asserted from the outside. Everything here is what a
/// hostile in-game message would get if it managed to steer a tool call (architecture.md §7).
/// </summary>
public class SettingsCapabilityTests
{
    [Fact]
    public async Task ProtectedAndSecretRowsAreNotEvenListed()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var listed = await surface.Registry.Invoke("list_settings", ToolArguments.Empty);

        Assert.False(listed.IsError);

        var hidden = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.Protected || row.Kind is SettingKind.Secret or SettingKind.Info)
            // Whole keys, not substrings. A protected row whose key is a prefix of a listed
            // row's key — "callouts.route" against "callouts.routeEveryNJumps" — is reported as
            // exposed by a Contains check while being nothing of the sort, and a false positive
            // here is a test that gets worked around rather than believed.
            .Where(row => Lists(listed.Content, row.Key))
            .Select(row => row.Key)
            .ToArray();

        Assert.True(
            hidden.Length == 0,
            $"A row the model cannot change has no reason to be in its vocabulary: {string.Join(", ", hidden)}");
    }

    /// <summary>
    /// Whether a settings listing offers this exact key, rather than one that merely starts
    /// with it. Keys are bounded by whitespace or punctuation in every listing format.
    /// </summary>
    private static bool Lists(string listing, string key)
    {
        var index = listing.IndexOf(key, StringComparison.OrdinalIgnoreCase);

        while (index >= 0)
        {
            var after = index + key.Length;

            if (after >= listing.Length || !IsKeyCharacter(listing[after]))
            {
                return true;
            }

            index = listing.IndexOf(key, after, StringComparison.OrdinalIgnoreCase);
        }

        return false;

        static bool IsKeyCharacter(char character) =>
            char.IsLetterOrDigit(character) || character is '.' or '_' or '-';
    }

    [Fact]
    public async Task AListedRowIsOneTheModelCanActuallyChange()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = await surface.Registry.Invoke(
            "set_setting",
            new ToolArguments(new Dictionary<string, string>
            {
                ["key"] = InterfaceCapability.ThemeKey,
                ["value"] = ThemeCatalog.Guardian,
            }));

        Assert.False(result.IsError);
        Assert.Equal(ThemeCatalog.Guardian, surface.Settings.Current.Ui.Theme);
    }

    [Theory]
    [InlineData("llm.provider")]
    [InlineData("llm.endpoint")]
    [InlineData("updates.checkOnStartup")]
    [InlineData("hotkeys.openSettings")]
    public async Task AProtectedRowIsRefusedThroughTheToolSurface(string key)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var before = surface.Settings.Read(key);

        var result = await surface.Registry.Invoke(
            "set_setting",
            new ToolArguments(new Dictionary<string, string> { ["key"] = key, ["value"] = "none" }));

        Assert.True(result.IsError);
        Assert.Equal(before, surface.Settings.Read(key));
    }

    [Fact]
    public async Task AKeyCannotBeStoredOrReadThroughTheToolSurface()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = LlmProviderCatalog.Find(LlmProviderCatalog.AnthropicId)!;
        var row = ConversationCapability.KeyRowFor(provider);

        var written = await surface.Registry.Invoke(
            "set_setting",
            new ToolArguments(new Dictionary<string, string> { ["key"] = row, ["value"] = "sk-not-a-real-key" }));

        Assert.True(written.IsError);
        Assert.False(surface.Settings.HasSecret(provider.KeySecretName));

        // And with a key present, reading it back is still not a thing the tool surface does.
        surface.Settings.Apply(row, "sk-not-a-real-key", SettingsCaller.Panel);

        var read = await surface.Registry.Invoke(
            "get_setting",
            new ToolArguments(new Dictionary<string, string> { ["key"] = row }));

        Assert.True(read.IsError);
        Assert.DoesNotContain("sk-not-a-real-key", read.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusalSaysWhereTheSettingLivesInsteadOfPretendingItDoesNotExist()
    {
        // The refusal is the safety property, not the silence: a Commander asking d47 to change
        // their provider deserves to be told where it is.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = await surface.Registry.Invoke(
            "get_setting",
            new ToolArguments(new Dictionary<string, string> { ["key"] = ConversationCapability.ProviderKey }));

        Assert.True(result.IsError);
        Assert.Contains("settings panel", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}

public class SettingCommandRoutingTests
{
    [Fact]
    public async Task AProtectedRowIsReachableByVoiceOnlyThroughTheModelFreePath()
    {
        // The whole shape of the protected rule in one test: the same change the tool surface
        // refuses goes through when it arrives as a declared phrase with no model involved.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            surface.Availability,
            surface.Spend,
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            settings: surface.Settings);

        TurnResult? result = null;
        await foreach (var turnEvent in loop.RunAsync("stop checking for updates", cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        Assert.Equal(TurnRoute.SettingCommand, result.Route);
        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.False(surface.Settings.Current.Updates.CheckOnStartup);
    }

    [Fact]
    public void EveryDeclaredPhraseMatchesTheRowThatDeclaredIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var rows = surface.Settings.Sections.SelectMany(s => s.Rows).Where(r => r.Commands.Count > 0).ToArray();

        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            foreach (var command in row.Commands)
            {
                var match = surface.Router.MatchSetting(command.Phrase);

                Assert.NotNull(match);
                Assert.Equal(row.Key, match.Row.Key);
                Assert.Equal(command.Value, match.Value);
            }
        }
    }

    [Fact]
    public void AnOrdinaryQuestionIsNotASettingsCommand()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Null(surface.Router.MatchSetting("what does personality off actually change"));
    }
}
