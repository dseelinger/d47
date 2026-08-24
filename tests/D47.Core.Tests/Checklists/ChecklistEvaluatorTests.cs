using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Progress is a diff against live journal state, so nobody ever types in what they have already
/// done (list.md Phase 17). These are the answers that are not "open", because each one is a
/// different next action.
/// </summary>
public class ChecklistEvaluatorTests
{
    private const string Loadout =
        """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
          "Modules":[
            { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true,
              "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
                             "Engineer":"Felicity Farseer","EngineerID":300100} },
            { "Slot":"PowerPlant", "Item":"int_powerplant_size6_class5", "On":true,
              "Engineering":{"BlueprintName":"PowerPlant_Armoured","Level":3,"Quality":0.4,
                             "Engineer":"Felicity Farseer","EngineerID":300100} },
            { "Slot":"Slot01_Size4", "Item":"int_shieldgenerator_size4_class5", "On":true } ]
        }
        """;

    private const string Progress =
        """
        { "timestamp":"2026-08-16T09:00:00Z", "event":"EngineerProgress",
          "Engineers":[ {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5} ] }
        """;

    private static CommanderGameState State(params string[] extra)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     Progress,
                     Loadout,
                 }.Concat(extra))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistItem Item(ChecklistIntent intent, string? hull = "krait_mkii") => new()
    {
        Key = ChecklistKeys.For(intent),
        Scope = ChecklistScope.Ship(12),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = "an intent",
        Intent = intent,
        Hull = hull,
    };

    [Fact]
    public void APlanNamingTheJournalsOwnSpellingIsConfirmedOutright()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
            {
                Detail = "Engine_Dirty",
                Grade = 5,
            }),
            State());

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
    }

    [Fact]
    public void APlanNamingTheTablesSpellingIsNowConfirmedRatherThanUnverifiable()
    {
        // **This used to assert the opposite**, and it was right to. Frontier writes the blueprint
        // as `Engine_Dirty` and the shipped table calls it "Dirty Drive Tuning", and nothing d47
        // shipped carried both spellings — so the honest answer was Unverified and a sentence
        // saying why. Guessing "not done" would have told a Commander their finished module was
        // unfinished.
        //
        // Remediation 15 item 10: the symbol Elite writes is coriolis-data's own key for the same
        // blueprint, and `Blueprints.tsv` now carries it against all 940 recipes. So the join
        // exists, the plan is confirmed outright, and the sentence that admitted the gap is gone.
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
            {
                Detail = "Dirty Drive Tuning",
                Grade = 5,
            }),
            State());

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
        Assert.DoesNotContain("cannot confirm", verdict.Value.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABlueprintTheTableDoesNotCarryIsStillUnverifiedRatherThanWrong()
    {
        // The remainder, and the reason null is kept. Four blueprints EDSY offers have no recipe in
        // the table, and Frontier can add one at any time — so a symbol nothing knows still answers
        // "I cannot check this" rather than being guessed either way.
        const string unknown =
            """
            { "timestamp":"2026-08-19T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
              "Modules":[
                { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true,
                  "Engineering":{"BlueprintName":"Engine_NotAThing","Level":5,"Quality":1.0,
                                 "Engineer":"Felicity Farseer","EngineerID":300100} } ]
            }
            """;

        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
            {
                Detail = "Dirty Drive Tuning",
                Grade = 5,
            }),
            State(unknown));

        Assert.Equal(ChecklistState.Unverified, verdict!.Value.State);
        Assert.Contains("no recipe under that name", verdict.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AWildcardGradeIsMetByWhateverIsInTheSlot()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")),
            State());

        // Null grade and null blueprint are both wildcards, so there is nothing left to be
        // unsure about — this must be Done rather than permanently unmeetable.
        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
    }

    [Fact]
    public void APartFinishedGradeIsOpenAndQuotesTheRollsLeft()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "PowerPlant")
            {
                Detail = "PowerPlant_Armoured",
                Grade = 3,
                Engineer = "Felicity Farseer",
            }),
            State());

        Assert.Equal(ChecklistState.Open, verdict!.Value.State);
        Assert.Contains("part-way through grade 3", verdict.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AGradeTheRankCannotReachIsBlockedRatherThanShort()
    {
        const string rankThree =
            """
            { "timestamp":"2026-08-16T09:30:00Z", "event":"EngineerProgress",
              "Engineers":[ {"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Unlocked","Rank":3} ] }
            """;

        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size4")
            {
                Detail = "Reinforced",
                Grade = 5,
                Engineer = "Elvira Martuuk",
            }),
            State(rankThree));

        // Not a slow route — no route. And the block has a published price, so the answer names it
        // rather than shrugging at the blocker.
        Assert.Equal(ChecklistState.Blocked, verdict!.Value.State);
        Assert.Contains("no amount of gathering fixes that", verdict.Value.Reason, StringComparison.Ordinal);
        Assert.Contains("compounds", verdict.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AModuleTheCommanderOwnsAndHasStoredIsElsewhereRatherThanOpen()
    {
        const string stored =
            """
            { "timestamp":"2026-08-16T09:45:00Z", "event":"StoredModules", "StarSystem":"Deciat",
              "MarketID":1, "StationName":"Garay Terminal",
              "Items":[ { "Name":"$int_guardianpowerplant_size6_name;",
                          "Name_Localised":"Guardian Power Plant", "StarSystem":"Deciat",
                          "TransferCost":2100000 } ] }
            """;

        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "Hardpoint9")
            {
                Detail = "Guardian Power Plant",
            }),
            State(stored));

        // A completely different next action from "go and grind it", which is why it is its own
        // state and carries the transfer cost.
        Assert.Equal(ChecklistState.Elsewhere, verdict!.Value.State);
        Assert.Contains("Deciat", verdict.Value.Reason, StringComparison.Ordinal);
        Assert.Contains("2,100,000", verdict.Value.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AShipIdNowReportingADifferentHullIsStaleRatherThanQuietlyDiffed()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(
                new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Grade = 5 },
                hull: "cutter"),
            State());

        Assert.Equal(ChecklistState.Stale, verdict!.Value.State);
    }

    [Fact]
    public void APlanForAShipTheCommanderIsNotFlyingSaysNothingAtAll()
    {
        var item = Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Grade = 5 })
            with { Scope = ChecklistScope.Ship(99) };

        // Null rather than Open. Loadout describes one ship, and resetting an item because the
        // evidence is absent would be an answer d47 invented.
        Assert.Null(ChecklistEvaluator.Evaluate(item, State()));
    }

    [Fact]
    public void AnExperimentalEffectIsCheckedExactlyBecauseEliteLocalisesThatOne()
    {
        const string withEffect =
            """
            { "timestamp":"2026-08-16T10:05:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
              "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true,
                "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
                               "ExperimentalEffect_Localised":"Drag Drives"} } ] }
            """;

        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Experimental, "MainEngines") { Detail = "Drag Drives" }),
            State(withEffect));

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
    }

    [Fact]
    public void AnAuthoredItemIsNeverEvaluated()
    {
        var item = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = "buy limpets",
        };

        Assert.Null(ChecklistEvaluator.Evaluate(item, State()));
    }

    [Fact]
    public void AnUnengineeredModuleIsSaidToBeUnengineeredRightNow()
    {
        // Remediation 15 item 5. "Point Defence is not engineered" reads as a fact about Point
        // Defence; "is not currently engineered" reads as a fact about right now, which is the
        // only thing d47 knows. The verdict is a reading taken at a moment — the plan carries the
        // journal's verdict with its date — and it ships as Open, meaning still to do.
        //
        // In Core, so it lands on the spoken path as well as the drawn one.
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "Slot01_Size4")
            {
                Detail = "Reinforced",
                Grade = 1,
                Engineer = "Felicity Farseer",
            }),
            State());

        Assert.Equal(ChecklistState.Open, verdict!.Value.State);
        Assert.Contains("is not currently engineered", verdict.Value.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plan for a ship in another dock now gets a verdict, from the loadout d47 remembers —
    /// reported 2026-08-23, and the comment that used to sit over this said it "cannot be diffed at
    /// all", which stopped being true when list.md Phase 37 started remembering them.
    /// <para>
    /// The line already read <i>"5A Thrusters"</i> off that remembered loadout while the verdict
    /// under it refused to look at the same place, which is the asymmetry that made one report read
    /// as d47 being unable to see a ship it could describe.
    /// </para>
    /// </summary>
    [Fact]
    public void AParkedShipIsDiffedFromTheLoadoutD47Remembers()
    {
        // Aboard the Krait, then aboard something else: the Krait's modules are remembered.
        var state = State(
            """
            { "timestamp":"2026-08-16T12:00:00Z", "event":"Loadout", "Ship":"anaconda", "ShipID":51,
              "Modules":[] }
            """);

        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
            {
                Detail = "Engine_Dirty",
                Grade = 5,
            }),
            state);

        Assert.NotNull(verdict);
        Assert.Equal(ChecklistState.Done, verdict!.Value.State);

        // And it says which moment it is a fact about. A month-old snapshot presented as current
        // is the one way this change can do harm.
        Assert.Contains("As last seen, 16 Aug 2026.", verdict.Value.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ship being flown says nothing of the kind, because there is nothing to date: the
    /// loadout is what Elite is reporting right now.
    /// </summary>
    [Fact]
    public void TheShipUnderYouIsNotDated()
    {
        var verdict = ChecklistEvaluator.Evaluate(
            Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
            {
                Detail = "Engine_Dirty",
                Grade = 5,
            }),
            State());

        Assert.DoesNotContain("As last seen", verdict!.Value.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A ship d47 has never been aboard still gets silence rather than a guess. Saying "open"
    /// because the evidence is absent would be an answer d47 invented.
    /// </summary>
    [Fact]
    public void AShipNobodyHasEverSatInStillGetsNoVerdict()
    {
        var item = Item(new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        }) with { Scope = ChecklistScope.Ship(99) };

        Assert.Null(ChecklistEvaluator.Evaluate(item, State()));
    }
}
