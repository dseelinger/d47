using D47.Core.Campaigns;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The Nemesis arc's secret is written once, refused unless every field a reveal needs is there and no text states a game
/// fact, kept with the arc, and revealed a piece at a time only when the faction wins a conflict or sees off a coup threat.
/// </summary>
public sealed class TheArcSecretIsRevealedOnlyByVictoriesTests
{
    private const string Rival = "Lave Purple Gang";

    private const string Squadron = "4401";

    private const string Id = "F1";

    private const string Culprit = "Deputy Arlen Voss";

    private static readonly CampaignArc Arc = new(ArcKind.Nemesis, Rival, "Director Varga", Now.AddDays(-10), 0);

    private static readonly string[] Pieces =
    [
        "A cipher key turned up in the faction's own archive.",
        "The key was signed out by someone on the inner council.",
        $"The signature belongs to {Culprit}, who has sold every plan.",
    ];

    private readonly MemoryFileSystem _files = new();

    private static ArcSecret Secret(int from = 0, params RevealPoint[] revealed) =>
        new(Rival, SecretKind.Traitor, Culprit, $"{Culprit} has been passing plans to {Rival}.", Pieces, from, revealed);

    private static ChronicleEntry Entry(string kind = "Conflict", string outcome = "Won", string system = "Lave") =>
        new(kind, system, kind == "Conflict" ? Rival : null, outcome, [Now.AddDays(-2)], []);

    private static string Answer(string? culprit = Culprit, string truth = "A traitor sold the plans.", params string[] reveals) =>
        System.Text.Json.JsonSerializer.Serialize(new { culprit, secret = truth, reveals = reveals.Length == 0 ? Pieces : reveals });

    [Fact]
    public void TimePassingRevealsNothing()
    {
        var secret = Secret();

        Assert.Empty(CampaignSecrets.Step(secret, Arc, Us, [], Now.AddYears(1)).Lines);
    }

    [Fact]
    public void AConflictWonGivesTheFirstRevealPinnedToItsEntry()
    {
        var step = CampaignSecrets.Step(Secret(), Arc, Us, [Entry()], Now);

        var line = Assert.Single(step.Lines);
        Assert.Equal(ArcAct.Reveal, line.Act);
        Assert.Equal(CampaignLiaison.Speaker, line.Leader);
        Assert.Contains($"{Us} won the conflict in Lave against {Rival}.", line.Facts);
        Assert.Contains($"In its aftermath this came to light: {Pieces[0]}", line.Facts);
        Assert.DoesNotContain(line.Facts, fact => fact.Contains(Culprit, StringComparison.Ordinal));
        Assert.Equal([new RevealPoint(0, "Lave", Now)], step.Secret!.Revealed);
    }

    [Fact]
    public void ACoupThreatSeenOffGivesAReveal()
    {
        var line = Assert.Single(CampaignSecrets.Step(Secret(), Arc, Us, [Entry("Coup threat", "Resolved", "Diso")], Now).Lines);

        Assert.Contains("The coup threat in Diso has passed.", line.Facts);
    }

    [Theory]
    [InlineData("Conflict", "Lost")]
    [InlineData("Conflict", "Ended")]
    [InlineData("Conflict", "Resolved")]
    [InlineData("Retreat", "Resolved")]
    [InlineData("Thin margin", "Resolved")]
    [InlineData("Bad state", "Resolved")]
    public void OtherResolutionsRevealNothing(string kind, string outcome)
    {
        Assert.Empty(CampaignSecrets.Step(Secret(), Arc, Us, [Entry(kind, outcome)], Now).Lines);
    }

    [Fact]
    public void EntriesBeforeTheSecretWasKeptDoNotCount()
    {
        Assert.Empty(CampaignSecrets.Step(Secret(from: 2), Arc, Us, [Entry(), Entry()], Now).Lines);
        Assert.Single(CampaignSecrets.Step(Secret(from: 2), Arc, Us, [Entry(), Entry(), Entry()], Now).Lines);
    }

    [Fact]
    public void TwoVictoriesInOneLookGiveOneRevealAndTheNextLookGivesTheOther()
    {
        var first = CampaignSecrets.Step(Secret(), Arc, Us, [Entry(), Entry(system: "Diso")], Now);
        var second = CampaignSecrets.Step(first.Secret, Arc, Us, [Entry(), Entry(system: "Diso")], Now.AddMinutes(5));
        var third = CampaignSecrets.Step(second.Secret, Arc, Us, [Entry(), Entry(system: "Diso")], Now.AddMinutes(10));

        Assert.Single(first.Lines);
        Assert.Contains($"In its aftermath this came to light: {Pieces[1]}", Assert.Single(second.Lines).Facts);
        Assert.Equal([0, 1], second.Secret!.Revealed.Select(point => point.Entry));
        Assert.Empty(third.Lines);
    }

    [Fact]
    public void TheLastRevealNamesTheCulpritAndTellsTheWholeTruthThenNothingMore()
    {
        var secret = Secret(0, new RevealPoint(0, "Lave", Now), new RevealPoint(1, "Lave", Now));
        var chronicle = new[] { Entry(), Entry(), Entry(), Entry() };

        var last = CampaignSecrets.Step(secret, Arc, Us, chronicle, Now);
        var after = CampaignSecrets.Step(last.Secret, Arc, Us, chronicle, Now);

        var line = Assert.Single(last.Lines);
        Assert.Contains(line.Facts, fact => fact.Contains(Culprit, StringComparison.Ordinal));
        Assert.Contains($"This is the last piece. The whole truth: {secret.Truth}", line.Facts);
        Assert.Equal(CampaignSecrets.RevealCount, last.Secret!.Revealed.Count);
        Assert.Empty(after.Lines);
    }

    [Fact]
    public void NoSecretOrAnotherRivalsSecretRevealsNothing()
    {
        Assert.Empty(CampaignSecrets.Step(null, Arc, Us, [Entry()], Now).Lines);
        Assert.Empty(CampaignSecrets.Step(Secret(), null, Us, [Entry()], Now).Lines);
        Assert.Empty(CampaignSecrets.Step(Secret(), Arc with { Rival = "Diso Ascendancy" }, Us, [Entry()], Now).Lines);
    }

    [Fact]
    public void TheKindIsTheSameForTheSameRivalAndSeedKey()
    {
        Assert.Equal(CampaignSecrets.KindFor(Rival, Squadron), CampaignSecrets.KindFor(Rival, Squadron));
    }

    [Fact]
    public void AWellFormedAnswerIsRead()
    {
        var draft = CampaignSecrets.Read("```json\n" + Answer() + "\n```", Us, Rival, ["Lave", "Diso"]);

        Assert.NotNull(draft);
        Assert.Equal(Culprit, draft.Culprit);
        Assert.Equal(Pieces, draft.Reveals);
    }

    [Fact]
    public void TheFactionsNamesAreNotTakenForTheSystemsInThem()
    {
        var reveals = new[] { Pieces[0], $"A courier for {Rival} was seen at the council chambers.", Pieces[2] };

        Assert.NotNull(CampaignSecrets.Read(Answer(reveals: reveals), Us, Rival, ["Lave"]));
    }

    [Fact]
    public void AnAnswerThatIsNotTheJsonAskedForIsRefused()
    {
        Assert.Null(CampaignSecrets.Read("The traitor is Arlen Voss.", Us, Rival, []));
        Assert.Null(CampaignSecrets.Read("{\"culprit\": \"Arlen\"", Us, Rival, []));
        Assert.Null(CampaignSecrets.Read(Answer(culprit: null), Us, Rival, []));
        Assert.Null(CampaignSecrets.Read(Answer(truth: " "), Us, Rival, []));
    }

    [Fact]
    public void AnAnswerWithTheWrongNumberOfRevealsIsRefused()
    {
        Assert.Null(CampaignSecrets.Read(Answer(reveals: [Pieces[0], Pieces[2]]), Us, Rival, []));
        Assert.Null(CampaignSecrets.Read(Answer(reveals: [Pieces[0], "", Pieces[2]]), Us, Rival, []));
    }

    [Fact]
    public void AnAnswerThatNamesTheCulpritEarlyOrNotAtTheEndIsRefused()
    {
        Assert.Null(CampaignSecrets.Read(Answer(reveals: [$"{Culprit} signed a cipher key.", Pieces[1], Pieces[2]]), Us, Rival, []));
        Assert.Null(CampaignSecrets.Read(Answer(reveals: [Pieces[0], Pieces[1], "The traitor is unmasked at last."]), Us, Rival, []));
    }

    [Fact]
    public void AnAnswerWithADigitIsRefused()
    {
        Assert.Null(CampaignSecrets.Read(Answer(reveals: ["Ledger 7 was altered.", Pieces[1], Pieces[2]]), Us, Rival, []));
        Assert.Null(CampaignSecrets.Read(Answer(truth: "Voss took 40,000 credits."), Us, Rival, []));
    }

    [Fact]
    public void AnAnswerThatNamesASystemIsRefused()
    {
        Assert.Null(CampaignSecrets.Read(Answer(reveals: ["A cipher key turned up in Diso.", Pieces[1], Pieces[2]]), Us, Rival, ["Diso"]));
        Assert.Null(CampaignSecrets.Read(Answer(truth: "Voss met them at Lave."), Us, Rival, ["Lave"]));
    }

    [Fact]
    public void AnOverlongAnswerIsRefused()
    {
        Assert.Null(CampaignSecrets.Read(Answer(truth: new string('a', CampaignSecrets.MostCharacters + 1)), Us, Rival, []));
    }

    private CampaignStore Store()
    {
        var store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);

        store.Load();
        store.SetFaction(Id, Us, clearIntents: false);
        store.SetArc(Id, Arc);

        return store;
    }

    [Fact]
    public void TheSecretIsKeptOnceFromTheChroniclesCountThenAndSurvivesAReload()
    {
        var store = Store();
        store.Choose(Id, FrontKind.Conflict, "Lave", Now.AddDays(-3));
        store.Resolve(Id, FrontKind.Conflict, "Lave", Entry());

        Assert.True(store.SetSecret(Id, Arc, Secret(from: 99, new RevealPoint(0, "Lave", Now))));
        Assert.False(store.SetSecret(Id, Arc, Secret()));

        var reloaded = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        var kept = reloaded.For(Id).Secret!;
        Assert.Equal((1, 0), (kept.From, kept.Revealed.Count));
        Assert.Equal((Culprit, SecretKind.Traitor), (kept.Culprit, kept.Kind));
        Assert.Equal(Pieces, kept.Reveals);
    }

    [Fact]
    public void ASecretForAnArcThatHasChangedIsNotKept()
    {
        var store = Store();

        Assert.False(store.SetSecret(Id, Arc with { Since = Now }, Secret()));
        Assert.Null(store.For(Id).Secret);
    }

    [Fact]
    public void TheArcsStepDoesNotOverwriteTheSecretAndNamingAnotherFactionClearsIt()
    {
        var store = Store();
        store.SetSecret(Id, Arc, Secret());

        store.SetArc(Id, Arc with { Answered = 3 });
        Assert.NotNull(store.For(Id).Secret);

        store.SetFaction(Id, "Another Faction", clearIntents: false);
        Assert.Null(store.For(Id).Secret);
    }

    [Fact]
    public void ARevealIsKeptOnlyOverTheSecretItWasStepFrom()
    {
        var store = Store();
        store.SetSecret(Id, Arc, Secret());
        var held = store.For(Id).Secret!;
        var next = held with { Revealed = [new RevealPoint(0, "Lave", Now)] };

        Assert.True(store.SetRevealed(Id, held, next));
        Assert.False(store.SetRevealed(Id, held, next with { Revealed = [] }));
        Assert.Single(store.For(Id).Secret!.Revealed);
    }

    [Fact]
    public void OpeningAnArcIsRaisedOnce()
    {
        var store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        store.SetFaction(Id, Us, clearIntents: false);
        var opened = 0;
        store.ArcOpened += _ => opened++;

        store.SetArc(Id, Arc);
        store.SetArc(Id, Arc with { Answered = 1 });

        Assert.Equal(1, opened);
    }

    [Fact]
    public async Task TheModelIsAskedOnceAndARefusedAnswerKeepsNothing()
    {
        var store = Store();
        var asked = new List<string>();

        var refused = await CampaignSecrets.RunAsync(store, Id, Us, Squadron, ["Lave"], (prompt, _) =>
        {
            asked.Add(prompt);
            return Task.FromResult<string?>(Answer(truth: "They met at Lave."));
        }, null, CancellationToken.None);

        Assert.False(refused);
        Assert.Null(store.For(Id).Secret);

        var kept = await CampaignSecrets.RunAsync(store, Id, Us, Squadron, ["Lave"], (_, _) => Task.FromResult<string?>(Answer()), null, CancellationToken.None);
        var again = await CampaignSecrets.RunAsync(store, Id, Us, Squadron, ["Lave"], (prompt, _) =>
        {
            asked.Add(prompt);
            return Task.FromResult<string?>(Answer());
        }, null, CancellationToken.None);

        Assert.True(kept);
        Assert.False(again);
        Assert.Single(asked);
        Assert.Contains(Rival, asked[0], StringComparison.Ordinal);
        Assert.Equal(CampaignSecrets.KindFor(Rival, Squadron), store.For(Id).Secret!.Kind);
    }

    [Fact]
    public async Task TheChroniclesSystemsAreNotNamedEither()
    {
        var store = Store();
        store.Choose(Id, FrontKind.Conflict, "Leesti", Now.AddDays(-3));
        store.Resolve(Id, FrontKind.Conflict, "Leesti", Entry(system: "Leesti"));

        var kept = await CampaignSecrets.RunAsync(store, Id, Us, Squadron, [], (_, _) => Task.FromResult<string?>(Answer(truth: "They met at Leesti.")), null, CancellationToken.None);

        Assert.False(kept);
    }
}
