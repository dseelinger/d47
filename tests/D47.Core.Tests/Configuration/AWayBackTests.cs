using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A way back from a setting that has gone wrong.</summary>
public class AWayBackTests
{
    [Fact]
    public void ARowNobodyTouchedIsNotMarkedAsChanged()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.False(surface.Settings.IsChanged(InterfaceCapability.ThemeKey));
    }

    [Fact]
    public void SettingARowMarksItAndResettingUnmarksIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.ModelKey, "claude-opus-5", SettingsCaller.Panel);
        Assert.True(surface.Settings.IsChanged(ConversationCapability.ModelKey));

        var reset = surface.Settings.Reset(ConversationCapability.ModelKey, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, reset.Status);
        Assert.False(surface.Settings.IsChanged(ConversationCapability.ModelKey));
        Assert.Null(surface.Settings.Current.Llm.Model);
    }

    /// <summary>
    /// The whole of the mechanism, stated: reset is a write of null, and null means the shipped default
    /// stands.
    /// </summary>
    [Fact]
    public void ResetLandsOnTheShippedDefaultRatherThanOnAValueAnybodyWroteDown()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var shipped = surface.Settings.Current.Llm.PersonalityEnabled;

        surface.Settings.Apply(
            "llm.personality",
            (!shipped).ToString(),
            SettingsCaller.Panel);

        Assert.NotEqual(shipped, surface.Settings.Current.Llm.PersonalityEnabled);

        surface.Settings.Reset("llm.personality", SettingsCaller.Panel);

        Assert.Equal(shipped, surface.Settings.Current.Llm.PersonalityEnabled);
    }

    /// <summary>A key is not a setting with a default to fall back to.</summary>
    [Fact]
    public void ASecretIsNeverReset()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var key = ConversationCapability.KeyRowFor(
            D47.Core.Conversation.LlmProviderCatalog.Selected(D47.Core.Conversation.LlmProviderCatalog.AnthropicId));

        surface.Settings.Apply(key, "sk-ant-not-a-real-key", SettingsCaller.Panel);

        var reset = surface.Settings.Reset(key, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Refused, reset.Status);
        Assert.True(surface.Settings.HasSecret(surface.Settings.Find(key)!.SecretName));

        // And it is never "changed", so it never grows the affordance in the first place.
        Assert.False(surface.Settings.IsChanged(key));
    }

    /// <summary>
    /// The realistic gesture: a Commander who has been fiddling with a card does not know which row did
    /// it.
    /// </summary>
    [Fact]
    public void ACardResetsEverythingOnItAndNoKeys()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var key = ConversationCapability.KeyRowFor(
            D47.Core.Conversation.LlmProviderCatalog.Selected(D47.Core.Conversation.LlmProviderCatalog.AnthropicId));

        surface.Settings.Apply(key, "sk-ant-not-a-real-key", SettingsCaller.Panel);
        surface.Settings.Apply(ConversationCapability.ModelKey, "claude-opus-5", SettingsCaller.Panel);
        surface.Settings.Apply(ConversationCapability.WebSearchKey, "true", SettingsCaller.Panel);

        var moved = surface.Settings.ResetCard(ConversationCapability.Id, SettingsCaller.Panel);

        Assert.Equal(2, moved);
        Assert.Null(surface.Settings.Current.Llm.Model);
        Assert.False(surface.Settings.Current.Llm.WebSearch);

        // "A Commander asking for a working Speech tab is not asking to be logged out of ElevenLabs." The
        // same holds one card over.
        Assert.True(surface.Settings.HasSecret(surface.Settings.Find(key)!.SecretName));
    }

    [Fact]
    public void ACardWithNothingChangedResetsNothing()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(0, surface.Settings.ResetCard(ConversationCapability.Id, SettingsCaller.Panel));
    }

    /// <summary>
    /// Reset writes safety-critical settings, so it inherits the invariant unchanged: reachable from
    /// the panel and the keyword router, never from the tool surface.
    /// </summary>
    [Fact]
    public void TheModelCannotResetAProtectedRow()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(
            ConversationCapability.ProviderKey,
            D47.Core.Conversation.LlmProviderCatalog.OpenAiId,
            SettingsCaller.Panel);

        var reset = surface.Settings.Reset(ConversationCapability.ProviderKey, SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, reset.Status);
        Assert.Equal(
            D47.Core.Conversation.LlmProviderCatalog.OpenAiId,
            surface.Settings.Current.Llm.Provider);
    }

    /// <summary>There is no reset tool, at any scope, and there must not be one.</summary>
    [Fact]
    public void NoToolOffersAReset()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var offending = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Select(tool => tool.Name)
            .Where(name => name.Contains("reset", StringComparison.OrdinalIgnoreCase)
                           || name.Contains("default", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            offending.Count == 0,
            $"A tool offers a reset: {string.Join(", ", offending)}. Protected is a property of "
            + "the caller, and one call that reaches every protected row is the thing that rule "
            + "exists to prevent.");
    }

    /// <summary>
    /// The trap the issue named, and the reason a per-Commander row cannot reset through an ordinary
    /// write.
    /// </summary>
    [Fact]
    public void ResettingACommanderRowRevealsTheInstallationsValueRatherThanBlankingIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        // The installation's answer, set before anybody is flying.
        surface.Settings.Apply("llm.aboutMe", "the house story", SettingsCaller.Panel);

        surface.Settings.UseCommander("F1", "HADESD");
        surface.Settings.Apply("llm.aboutMe", "my own story", SettingsCaller.Panel);

        Assert.Equal("my own story", surface.Settings.Read("llm.aboutMe"));
        Assert.True(surface.Settings.IsChanged("llm.aboutMe"));

        var reset = surface.Settings.Reset("llm.aboutMe", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, reset.Status);

        // Back to the installation's, not to nothing.
        Assert.Equal("the house story", surface.Settings.Read("llm.aboutMe"));
    }

    /// <summary>
    /// And clearing by hand still means what it always did, so reset has not silently taken over the
    /// other gesture.
    /// </summary>
    [Fact]
    public void ClearingACommanderRowByHandIsStillDeliberatelyBlank()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply("llm.aboutMe", "the house story", SettingsCaller.Panel);
        surface.Settings.UseCommander("F1", "HADESD");
        surface.Settings.Apply("llm.aboutMe", null, SettingsCaller.Panel);

        Assert.Null(surface.Settings.Read("llm.aboutMe"));
    }
}
