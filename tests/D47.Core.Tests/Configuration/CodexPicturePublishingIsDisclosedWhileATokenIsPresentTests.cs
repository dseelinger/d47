using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class CodexPicturePublishingIsDisclosedWhileATokenIsPresentTests
{
    [Fact]
    public void WithATokenItIsActiveAndNamesWhatGoes()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CodexPublish, new D47Settings(), llmKeyPresent: false, codexPublishTokenPresent: true);

        Assert.Contains(EgressDisclosure.CodexPublish, EgressDisclosure.Ids);
        Assert.Equal("Codex picture publishing", entry.Name);
        Assert.True(entry.Active);
        Assert.Equal("api.github.com and uploads.github.com", entry.Destination);
        Assert.Equal(
            "When you press Publish or Unpublish: the picture, its codex entry and region, and your publish token, to dseelinger/d47-codex-pictures. Nothing else goes with it.",
            entry.What);
    }

    [Fact]
    public void WithNoTokenItIsSilent()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CodexPublish, new D47Settings(), llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Equal("No codex publish token is stored, so nothing is published.", entry.What);
    }

    [Fact]
    public async Task AStoredTokenTurnsThePrivacyRowAndTheToolActive()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        secrets.Set(CodexPublishToken.SecretName, "github_pat_test");

        var settings = new SettingsService(store, secrets, new D47Settings(), NullLogger<SettingsService>.Instance);
        var privacy = PrivacyCapability.Create(settings);

        var report = await privacy.Tools.Single().Handler(
            D47.Core.Capabilities.ToolArguments.Empty, TestContext.Current.CancellationToken);
        var row = privacy.Settings.Single(setting => setting.Key == $"egress.{EgressDisclosure.CodexPublish}");

        Assert.Contains("Codex picture publishing → api.github.com and uploads.github.com", report.Content, StringComparison.Ordinal);
        Assert.StartsWith("Codex picture publishing → api.github.com", row.Binding!.Read(settings.Current), StringComparison.Ordinal);
        Assert.DoesNotContain("github_pat_test", report.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTokenIsTheStoredSecretElseTheEnvironmentVariable()
    {
        static string? Environment(string name) => name == CodexPublishToken.Variable ? "from-the-environment" : null;

        Assert.Equal("stored", CodexPublishToken.Resolve("stored", Environment));
        Assert.Equal("from-the-environment", CodexPublishToken.Resolve(null, Environment));
        Assert.Null(CodexPublishToken.Resolve(null, _ => null));
        Assert.Null(CodexPublishToken.Resolve(null, _ => "  "));

        Assert.True(CodexPublishToken.Present(stored: true, _ => null));
        Assert.True(CodexPublishToken.Present(stored: false, Environment));
        Assert.False(CodexPublishToken.Present(stored: false, _ => null));
    }

    [Fact]
    public void TheTokenRowIsAnAdvancedSecretUnderItsOwnAnchor()
    {
        var row = JournalCapability.Create(new D47.Core.Journal.GameStateStore())
            .Settings.Single(setting => setting.Key == JournalCapability.CodexPublishTokenKey);

        Assert.Equal("Codex publish token", row.Label);
        Assert.True(row.Advanced);
        Assert.Equal(D47.Core.Capabilities.SettingKind.Secret, row.Kind);
        Assert.Equal("codex-publish-token", row.SecretName);
        Assert.Equal("codex-publish-token", row.DocsAnchor);
    }
}
