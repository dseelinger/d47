using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// "Settings by voice" (list.md Phase 6): unless otherwise noted, every setting should be
/// settable by voice — and protected rows are reachable by voice <em>only</em> through the
/// model-free keyword router, so "by voice" never silently means "by the LLM".
/// <para>
/// The two halves of that are tested separately, because they fail in opposite directions. An
/// unprotected row is reachable through <c>set_setting</c> and needs nothing declared. A
/// protected row is reachable only if somebody wrote a phrase for it, and forgetting to is
/// invisible — the row simply cannot be set by voice, and nothing says so.
/// </para>
/// </summary>
public class SettingsByVoiceTests
{
    /// <summary>
    /// Rows whose value cannot be a closed phrase-to-value pair, with the reason. The router
    /// deliberately does not extract values from free text — one that guesses at values is one
    /// that changes the wrong setting with total confidence — so a row whose value space is
    /// open is set from the panel and is "otherwise noted" here rather than left silently
    /// unreachable.
    /// </summary>
    private static readonly Dictionary<string, string> NotSettableByPhrase = new(StringComparer.Ordinal)
    {
        ["speech.shutUpHotkey"] =
            "A key gesture. Dictating one is worse than pressing it, and the panel binds by press.",
        ["listening.pushToTalkKey"] =
            "A key gesture, same as the silence key.",
        ["llm.endpoint"] =
            "A URL. There is no closed set of them, and a misheard host is a silent misconfiguration.",
    };

    [Fact]
    public void EveryProtectedRowWithAClosedValueSetIsReachableByVoice()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var unreachable = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.Protected)
            .Where(row => row.Kind is SettingKind.Toggle or SettingKind.Choice)
            .Where(row => row.Commands.Count == 0)
            .Where(row => !NotSettableByPhrase.ContainsKey(row.Key))
            .Select(row => row.Key)
            .ToArray();

        Assert.True(
            unreachable.Length == 0,
            "A protected row is unreachable from the tool surface by design, so a row with no command "
            + "phrase cannot be set by voice at all — and nothing reports that. Add phrases, or record "
            + "the reason in NotSettableByPhrase: " + string.Join(", ", unreachable));
    }

    [Fact]
    public void EveryExemptionNamesARowThatStillExists()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var keys = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Select(row => row.Key)
            .ToHashSet(StringComparer.Ordinal);

        // An exemption for a row that has been renamed or removed is an exemption quietly
        // covering nothing, and the next row to need one inherits a list nobody trusts.
        var stale = NotSettableByPhrase.Keys.Where(key => !keys.Contains(key)).ToArray();

        Assert.True(stale.Length == 0, "Exemptions for rows that no longer exist: " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryDeclaredCommandPhraseActuallyRoutes()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        foreach (var row in surface.Settings.Sections.SelectMany(section => section.Rows))
        {
            foreach (var command in row.Commands)
            {
                var match = router.MatchSetting(command.Phrase);

                // A phrase declared and not matched is a phrase the Commander says into silence.
                Assert.NotNull(match);
                Assert.Equal(row.Key, match!.Row.Key);
                Assert.Equal(command.Value, match.Value);
            }
        }
    }

    [Fact]
    public void ASettingPhraseNeverReachesTheModelPath()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        var phrases = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .SelectMany(row => row.Commands)
            .Select(command => command.Phrase)
            .ToArray();

        Assert.NotEmpty(phrases);

        foreach (var phrase in phrases)
        {
            // The point of the router existing in this shape: a protected row is reachable by
            // voice without the model ever being in the path (architecture.md §7).
            Assert.NotNull(router.MatchSetting(phrase));
        }
    }

    [Fact]
    public void ProtectedRowsRefuseTheModelEvenWhenItNamesThemExactly()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        foreach (var row in surface.Settings.Sections
                     .SelectMany(section => section.Rows)
                     .Where(row => row.Protected && row.Binding?.Write is not null))
        {
            var result = surface.Settings.Apply(row.Key, "false", SettingsCaller.Model);

            // Protection is a property of the caller, not of the modality. The same key applied
            // from the panel or the router succeeds; from the model it does not.
            Assert.False(result.Ok);
        }
    }
}
