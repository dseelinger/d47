using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Listening;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Where the correction is applied, and where the asking happens
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
/// <para>
/// <b>The rewrite is to the sentence, not to a tool argument</b>, and that is the whole reason it
/// sits at the top of the turn: a correction learned from <i>"how far is Eurebia"</i> has to fix
/// <i>"the Eurebia Blue Mafia"</i> too, and it only does if every road out of the turn — the
/// keyword router, a settings phrase, the model — reads the corrected words.
/// </para>
/// </summary>
public class TheCorrectionReachesTheWholeSentenceTests
{
    private static TurnLoop Loop(TempInstall install, ILlmProvider provider, Func<string, string> heard)
    {
        var registry = TestSurface.For(install).Registry;

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
            Heard = heard,
        };
    }

    private static async Task RunAsync(TurnLoop loop, string input, InputSource source)
    {
        await foreach (var _ in loop.RunAsync(
            input, source, TestContext.Current.CancellationToken))
        {
        }
    }

    /// <summary>
    /// <b>Spoken input is corrected before anything reads it.</b> What reaches the model is the
    /// sentence the Commander meant, in full — not a tool argument patched after the fact.
    /// </summary>
    [Fact]
    public async Task ASpokenSentenceIsCorrectedBeforeTheModelSeesIt()
    {
        using var install = new TempInstall();

        var learned = SoundsLike.Empty.Learn(
            "Eurebia", "Eurybia", new DateTimeOffset(2026, 8, 27, 20, 0, 0, TimeSpan.Zero));

        var provider = FakeLlmProvider.Answering("About sixty light years.");
        var loop = Loop(install, provider, learned.Apply);

        await RunAsync(loop, "who runs the Eurebia Blue Mafia", InputSource.Spoken);

        Assert.NotNull(provider.LastRequest);

        Assert.Contains(
            "Eurybia Blue Mafia",
            provider.LastRequest.Prompt.History[^1].Text,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Eurebia",
            provider.LastRequest.Prompt.History[^1].Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Typed input is left exactly as it was typed.</b> A Commander who types a word can see
    /// what they typed, and rewriting it would be d47 correcting their spelling unasked — which is
    /// a different feature, and not one anybody has asked for.
    /// </summary>
    [Fact]
    public async Task ATypedSentenceIsLeftAlone()
    {
        using var install = new TempInstall();

        var learned = SoundsLike.Empty.Learn(
            "Eurebia", "Eurybia", new DateTimeOffset(2026, 8, 27, 20, 0, 0, TimeSpan.Zero));

        var provider = FakeLlmProvider.Answering("About sixty light years.");
        var loop = Loop(install, provider, learned.Apply);

        await RunAsync(loop, "who runs the Eurebia Blue Mafia", InputSource.Typed);

        Assert.NotNull(provider.LastRequest);

        Assert.Contains(
            "Eurebia",
            provider.LastRequest.Prompt.History[^1].Text,
            StringComparison.Ordinal);
    }

    /// <summary>Answers about one system and nothing else, so a lookup can be made to fail.</summary>
    private sealed class OneSystem(string known) : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(query.ReferenceSystem ?? string.Empty, 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult(
                string.Equals(to, known, StringComparison.OrdinalIgnoreCase) ? 60.4 : (double?)null);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not what these tests drive.");

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not what these tests drive.");

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not what these tests drive.");
    }

    /// <summary>
    /// <b>The reported turn, through the real tool.</b> A name that will not resolve comes back
    /// asking rather than shrugging — and the answer that follows teaches the correction, which is
    /// the retry and the remembering in one exchange.
    /// </summary>
    [Fact]
    public async Task TheFailingLookupAsksAndTheAnswerTeachesTheCorrection()
    {
        using var install = new TempInstall();

        var surface = TestSurface.For(install);

        // Off out of the box, and every one of these is about what a lookup says when it
        // fails rather than about the switch.
        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var watch = new MishearingWatch();
        var learned = new List<(string Heard, string Meant)>();

        var heard = new SpokenNamesSurface(
            () => SpokenNames.Empty.With(["Eurybia", "Sol", "Deciat"]),
            watch,
            (from, to) => learned.Add((from, to)));

        var galaxy = GalaxyCapability.Create(
            new OneSystem("Eurybia"),
            () => "Sol",
            surface.Settings,
            heard: heard);

        var distance = galaxy.Tools.Single(tool => tool.Name == "distance_between");

        var missed = await distance.Handler(
            new ToolArguments(new Dictionary<string, string> { ["to"] = "Eurebia" }), TestContext.Current.CancellationToken);

        Assert.True(missed.IsError);
        Assert.Contains("Did you mean Eurybia?", missed.Content, StringComparison.Ordinal);
        Assert.Empty(learned);

        // The Commander says which, the model runs it again, and that is the confirmation.
        var found = await distance.Handler(
            new ToolArguments(new Dictionary<string, string> { ["to"] = "Eurybia" }), TestContext.Current.CancellationToken);

        Assert.False(found.IsError);
        Assert.Equal(("Eurebia", "Eurybia"), Assert.Single(learned));
    }

    /// <summary>
    /// <b>A second failure asks for the letters rather than offering another list.</b> The
    /// correction is itself spoken and can itself be misheard.
    /// </summary>
    [Fact]
    public async Task ASecondFailureAsksRatherThanLooping()
    {
        using var install = new TempInstall();

        var surface = TestSurface.For(install);

        // Off out of the box, and every one of these is about what a lookup says when it
        // fails rather than about the switch.
        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var galaxy = GalaxyCapability.Create(
            new OneSystem("Eurybia"),
            () => "Sol",
            surface.Settings,
            heard: new SpokenNamesSurface(
                () => SpokenNames.Empty.With(["Eurybia", "Sol"]),
                new MishearingWatch(),
                (_, _) => { }));

        var distance = galaxy.Tools.Single(tool => tool.Name == "distance_between");

        await distance.Handler(
            new ToolArguments(new Dictionary<string, string> { ["to"] = "Eurebia" }), TestContext.Current.CancellationToken);

        var again = await distance.Handler(
            new ToolArguments(new Dictionary<string, string> { ["to"] = "Yourebia" }), TestContext.Current.CancellationToken);

        Assert.Contains("Spell it out", again.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Did you mean", again.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no catalogue behind it the wording is what it always was, so a machine with no
    /// journals read yet is no worse off than before.
    /// </summary>
    [Fact]
    public async Task WithNoCatalogueTheOldWordingStands()
    {
        using var install = new TempInstall();

        var surface = TestSurface.For(install);

        // Off out of the box, and every one of these is about what a lookup says when it
        // fails rather than about the switch.
        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var galaxy = GalaxyCapability.Create(new OneSystem("Eurybia"), () => "Sol", surface.Settings);

        var distance = galaxy.Tools.Single(tool => tool.Name == "distance_between");

        var missed = await distance.Handler(
            new ToolArguments(new Dictionary<string, string> { ["to"] = "Eurebia" }), TestContext.Current.CancellationToken);

        Assert.Contains("couldn't find one of those systems", missed.Content, StringComparison.Ordinal);
    }
}
