using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// "Settings by voice": unless otherwise noted, every setting should be settable by voice —
/// and protected rows are reachable by voice only through the model-free keyword router, so "by voice"
/// never silently means "by the LLM".
/// </summary>
public class SettingsByVoiceTests
{
    /// <summary>Rows whose value cannot be a closed phrase-to-value pair, with the reason.</summary>
    private static readonly Dictionary<string, string> NotSettableByPhrase = new(StringComparer.Ordinal)
    {
        ["listening.cancelHotkey"] =
            "A key gesture. Dictating one is worse than pressing it, and the panel binds by press.",
        ["listening.cancelButton"] =
            "A stick button, and the panel binds it by asking for a press. There is no phrase that "
            + "names one — a Commander does not know their own NonRoamableId.",
        ["listening.pushToTalkKey"] =
            "A key gesture, same as the cancel key.",
        ["llm.endpoint"] =
            "A URL. There is no closed set of them, and a misheard host is a silent misconfiguration.",
        ["persona.shipCoreShip"] =
            "A ship id, and every Commander's are different — there is no phrase-to-value pair to "
            + "write down. It is set on the Settings tab and nowhere else since #219, which "
            + "withdrew both the spoken phrases and the Ctrl+Alt+B gesture Phase 35 built.",
        ["persona.shipCore"] =
            "The value is a core and the subject is whichever ship the row above points at, so one "
            + "phrase would have to carry both. Same voice route as the row above.",
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

        // An exemption for a row that has been renamed or removed is an exemption quietly covering nothing,
        // and the next row to need one inherits a list nobody trusts.
        var stale = NotSettableByPhrase.Keys.Where(key => !keys.Contains(key)).ToArray();

        Assert.True(stale.Length == 0, "Exemptions for rows that no longer exist: " + string.Join(", ", stale));
    }

    /// <summary>And still routes with an opener in front of it.</summary>
    [Theory]
    [InlineData("switch to ")]
    [InlineData("go to ")]
    [InlineData("set ")]
    [InlineData("select the ")]
    [InlineData("show me the ")]
    public void EveryDeclaredCommandPhraseAlsoRoutesBehindAnOpener(string opener)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        foreach (var row in surface.Settings.Sections.SelectMany(section => section.Rows))
        {
            foreach (var command in row.Commands)
            {
                var match = router.MatchSetting(opener + command.Phrase);

                Assert.NotNull(match);
                Assert.Equal(row.Key, match!.Row.Key);
                Assert.Equal(command.Value, match.Value);
            }
        }
    }

    /// <summary>A phrase that itself opens with an opener still means what it declared.</summary>
    [Fact]
    public void APhraseThatBeginsWithAnOpenerIsStillMatchedAsDeclared()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        var declared = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .SelectMany(row => row.Commands.Select(command => (row, command)))
            .Where(pair => SpokenOpeners.All.Any(
                opener => pair.command.Phrase.StartsWith(opener, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.NotEmpty(declared);

        foreach (var (row, command) in declared)
        {
            var match = router.MatchSetting(command.Phrase);

            Assert.NotNull(match);
            Assert.Equal(row.Key, match!.Row.Key);
            Assert.Equal(command.Value, match.Value);
        }
    }

    /// <summary>And the opener does not become a way in for a sentence.</summary>
    [Theory]
    [InlineData("can you switch to full panel while I dock")]
    [InlineData("what does switch to full panel actually do")]
    [InlineData("switch to full panel is the phrase I keep forgetting")]
    public void AnOpenerDoesNotMakeASentenceIntoACommand(string spoken)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        Assert.Null(router.MatchSetting(spoken));
    }

    /// <summary>A word on the end does not lose the command: "start calling things out again" names its phrase plainly, and the looser keyword route must not answer it first.</summary>
    [Theory]
    [InlineData("again")]
    [InlineData("now")]
    [InlineData("please")]
    [InlineData("thanks")]
    public void EveryDeclaredCommandPhraseAlsoRoutesBehindATail(string tail)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        foreach (var row in surface.Settings.Sections.SelectMany(section => section.Rows))
        {
            foreach (var command in row.Commands)
            {
                var match = router.MatchSetting($"{command.Phrase} {tail}");

                Assert.NotNull(match);
                Assert.Equal(row.Key, match!.Row.Key);
                Assert.Equal(command.Value, match.Value);
            }
        }
    }

    /// <summary>
    /// The reported utterance itself, pinned to the row it names rather than left to the sweep above:
    /// this is the one the Commander said, and it is the one that has to keep working.
    /// </summary>
    [Fact]
    public void StartCallingThingsOutAgainTurnsTheCalloutsBackOn()
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        var match = router.MatchSetting("start calling things out again");

        Assert.NotNull(match);
        Assert.Equal(CalloutCapability.EnabledKey, match!.Row.Key);
        Assert.Equal("true", match.Value);
    }

    /// <summary>And a tail is still not a licence to match a sentence.</summary>
    [Theory]
    [InlineData("why did you stop calling things out again")]
    [InlineData("what does start calling things out again mean")]
    [InlineData("do not stop calling things out now")]
    public void ATailDoesNotMakeASentenceIntoACommand(string spoken)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        Assert.Null(router.MatchSetting(spoken));
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
            // The point of the router existing in this shape: a protected row is reachable by voice without
 // the model ever being in the path.
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

            // Protection is a property of the caller, not of the modality.
            Assert.False(result.Ok);
        }
    }
}
