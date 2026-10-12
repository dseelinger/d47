using D47.App.Controls;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// <see cref="SystemNameSuggester"/> asks only from three characters with the galaxy setting on, cancels the
/// request a newer one replaces, and drops an answer that arrives after a newer request (#1087).
/// </summary>
public sealed class SuggestionsFollowOnlyTheLatestTypingTests
{
    private sealed class Lookup
    {
        public List<(string Typed, CancellationToken Token, TaskCompletionSource<IReadOnlyList<SystemNameMatch>> Answer)> Asked { get; } = [];

        public Task<IReadOnlyList<SystemNameMatch>> Ask(string typed, CancellationToken token)
        {
            var answer = new TaskCompletionSource<IReadOnlyList<SystemNameMatch>>();
            Asked.Add((typed, token, answer));

            return answer.Task;
        }
    }

    private sealed class Rig
    {
        public Lookup Lookup { get; } = new();

        public bool Enabled { get; set; } = true;

        public List<IReadOnlyList<SystemNameMatch>> Answers { get; } = [];

        public SystemNameSuggester<SystemNameMatch> Suggester { get; }

        public Rig()
        {
            Suggester = new SystemNameSuggester<SystemNameMatch>(Lookup.Ask, () => Enabled, action => action(), TimeSpan.Zero);
            Suggester.Answered += Answers.Add;
        }
    }

    private static IReadOnlyList<SystemNameMatch> Named(params string[] names) =>
        [.. names.Select((name, i) => new SystemNameMatch(name, i, new StarPosition(0, 0, 0)))];

    [Fact]
    public async Task FewerThanThreeCharactersAskNothing()
    {
        var rig = new Rig();

        await rig.Suggester.Typed("Di");
        await rig.Suggester.Typed("  D ");

        Assert.Empty(rig.Lookup.Asked);
        Assert.All(rig.Answers, Assert.Empty);
    }

    [Fact]
    public async Task WithTheGalaxySettingOffNothingIsAsked()
    {
        var rig = new Rig { Enabled = false };

        await rig.Suggester.Typed("Diaguandri");

        Assert.Empty(rig.Lookup.Asked);
        Assert.All(rig.Answers, Assert.Empty);
    }

    [Fact]
    public async Task ThreeCharactersAskAndTheAnswerIsPassedOn()
    {
        var rig = new Rig();

        var asked = rig.Suggester.Typed("Dia");
        rig.Lookup.Asked.Single().Answer.SetResult(Named("Diaguandri", "Dian"));
        await asked;

        Assert.Equal("Dia", rig.Lookup.Asked.Single().Typed);
        Assert.Equal(["Diaguandri", "Dian"], rig.Answers.Single().Select(match => match.Name));
    }

    [Fact]
    public void AFourthCharacterCancelsTheRequestForThree()
    {
        var rig = new Rig();

        _ = rig.Suggester.Typed("Dia");
        _ = rig.Suggester.Typed("Diag");

        Assert.Equal(["Dia", "Diag"], rig.Lookup.Asked.Select(asked => asked.Typed));
        Assert.True(rig.Lookup.Asked[0].Token.IsCancellationRequested);
        Assert.False(rig.Lookup.Asked[1].Token.IsCancellationRequested);
    }

    [Fact]
    public async Task AnAnswerArrivingAfterANewerRequestIsDiscarded()
    {
        var rig = new Rig();

        var older = rig.Suggester.Typed("Dia");
        var newer = rig.Suggester.Typed("Diag");

        rig.Lookup.Asked[1].Answer.SetResult(Named("Diaguthivar", "Diaguan", "Diaguandri"));
        await newer;
        rig.Lookup.Asked[0].Answer.SetResult(Named("Diaguandri", "Dian"));
        await older;

        Assert.Equal(["Diaguthivar", "Diaguan", "Diaguandri"], rig.Answers.Single().Select(match => match.Name));
    }

    [Fact]
    public async Task AFailedLookupShowsNothing()
    {
        var rig = new Rig();

        var asked = rig.Suggester.Typed("Dia");
        rig.Lookup.Asked.Single().Answer.SetException(new GalaxyUnavailableException("Spansh refused."));
        await asked;

        Assert.Empty(rig.Answers.Single());
    }

    [Fact]
    public async Task ACancelledRequestPassesNothingOn()
    {
        var rig = new Rig();

        var asked = rig.Suggester.Typed("Dia");
        rig.Suggester.Cancel();
        rig.Lookup.Asked.Single().Answer.SetResult(Named("Diaguandri"));
        await asked;

        Assert.Empty(rig.Answers);
    }
}
