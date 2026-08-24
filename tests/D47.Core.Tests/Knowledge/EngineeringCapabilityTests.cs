using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The engineering capability — the first point in Phase 14 where the tables, the roll rules and
/// the Commander's own journal meet.
/// <para>
/// Run against the <b>real shipped tables</b>, as the specification and engineer tests already
/// are. A blueprint table is only worth having if it is right, and a test that mocks it away
/// proves the formatting rather than the answer.
/// </para>
/// <para>
/// The loadout fixture carries shapes measured across the corpus rather than invented ones — a
/// finished grade, a part-rolled one, and the engineer-less module that turns out to be a module
/// bought already engineered.
/// </para>
/// </summary>
public class EngineeringCapabilityTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    internal const string Commander =
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    /// <summary>
    /// Farseer at rank 5, so a grade 5 blueprint she offers is reachable. Elvira Martuuk at rank
    /// 4, which is what makes a part-rolled grade 3 quotable: the step size is a function of
    /// <c>rank − grade</c> and of nothing else, so without a rank there is no roll count.
    /// </summary>
    internal const string Progress =
        """
        {"timestamp":"3311-01-01T00:01:00Z","event":"EngineerProgress","Engineers":[
          {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5},
          {"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Unlocked","Rank":4},
          {"Engineer":"Professor Palin","EngineerID":300220,"Progress":"Invited"}]}
        """;

    /// <summary>
    /// Six modules and five situations: a finished grade, a grade part rolled by an engineer the
    /// Commander has a rank with, the 399999 module that arrived already engineered, two modules
    /// of the same kind so that a name can be genuinely ambiguous, and one with no engineering at
    /// all.
    /// </summary>
    internal const string Loadout =
        """
        {"timestamp":"3311-01-01T00:02:00Z","event":"Loadout","Ship":"Krait_MkII","ShipID":4,
         "ShipName":"Fixture","ShipIdent":"FX-01","HullValue":1000,"ModulesValue":2000,
         "Modules":[
          {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size5_class5","On":true,"Health":1.0,
           "Engineering":{"BlueprintID":128673694,"BlueprintName":"FSD_LongRange","Level":5,"Quality":1.0,
             "Engineer":"Felicity Farseer","EngineerID":300100,
             "ExperimentalEffect":"special_fsd_heavy","ExperimentalEffect_Localised":"Mass Manager",
             "Modifiers":[
               {"Label":"Mass","Value":26.0,"OriginalValue":20.0,"LessIsGood":1},
               {"Label":"FSDOptimalMass","Value":1692.75,"OriginalValue":1050.0,"LessIsGood":0}
             ]}},
          {"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Health":1.0,
           "Engineering":{"BlueprintID":128673659,"BlueprintName":"Engine_Dirty","Level":3,"Quality":0.4,
             "Engineer":"Elvira Martuuk","EngineerID":300160,
             "Modifiers":[{"Label":"EngineOptimalMass","Value":525.0,"OriginalValue":500.0,"LessIsGood":0}]}},
          {"Slot":"Slot01_Size6","Item":"int_cargorack_size6_class1","On":true,"Health":1.0,
           "Engineering":{"BlueprintID":128731462,"BlueprintName":"CargoRack_IncreasedCapacity","Level":5,
             "Quality":1.0,"EngineerID":399999,
             "Modifiers":[{"Label":"CargoCapacity","Value":96.0,"OriginalValue":64.0,"LessIsGood":0}]}},
          {"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Health":1.0,
           "Engineering":{"BlueprintID":128731490,"BlueprintName":"ShieldBooster_HeavyDuty","Level":5,
             "Quality":1.0,"Engineer":"Felicity Farseer","EngineerID":300100,
             "Modifiers":[{"Label":"DefenceModifierShieldMultiplier","Value":29.5,"OriginalValue":20.0,"LessIsGood":0}]}},
          {"Slot":"TinyHardpoint2","Item":"hpt_shieldbooster_size0_class5","On":true,"Health":1.0,
           "Engineering":{"BlueprintID":128731490,"BlueprintName":"ShieldBooster_HeavyDuty","Level":3,
             "Quality":0.2,"Engineer":"Felicity Farseer","EngineerID":300100,
             "Modifiers":[{"Label":"DefenceModifierShieldMultiplier","Value":24.0,"OriginalValue":20.0,"LessIsGood":0}]}},
          {"Slot":"PowerPlant","Item":"int_powerplant_size6_class5","On":true,"Health":1.0}
         ]}
        """;

    private static CapabilityRegistry Registry(params string[] events)
    {
        var gameState = new GameStateStore();

        foreach (var line in events)
        {
            gameState.Apply(Event(line));
        }

        return CapabilityRegistry.Build([EngineeringCapability.Create(() => gameState.Active)]);
    }

    private static CapabilityRegistry Flying() => Registry(Commander, Progress, Loadout);

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    private static async Task<string> Ask(
        CapabilityRegistry registry,
        string tool,
        params (string Name, string Value)[] values)
    {
        var result = await registry.InvokeAsync(tool, Args(values), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    // ---- get_blueprint -------------------------------------------------------------------------

    [Fact]
    public async Task ABlueprintSaysWhatItCostsPerApplicationAtEveryGrade()
    {
        var answer = await Ask(Flying(), "get_blueprint", ("blueprint", "Increased FSD Range"));

        Assert.Contains("Increased FSD Range — Frame Shift Drive blueprint, grades 1 to 5.", answer, StringComparison.Ordinal);

        // Per application and per grade, because the recipe changes as the grade climbs and a
        // Commander gathering for grade 3 needs that row rather than the top one.
        Assert.Contains("Per application:", answer, StringComparison.Ordinal);
        Assert.Contains("Grade 5: 1 × Arsenic", answer, StringComparison.Ordinal);

        // Ingredients arrive as symbols and go out as names. A recipe calling for
        // "dataminedwake" is a recipe nobody can shop for.
        Assert.Contains("Datamined Wake Exceptions", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("dataminedwake", answer, StringComparison.Ordinal);

        Assert.Contains("Offered by: ", answer, StringComparison.Ordinal);
        Assert.Contains("Felicity Farseer to grade 5", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCostOfAFullGradeIsAnExactTotalForThisCommander()
    {
        var answer = await Ask(Flying(), "get_blueprint", ("blueprint", "Increased FSD Range"));

        // The whole point of folding rank in. At rank 5 a grade 5 is five rolls, so the answer is
        // a shopping list rather than a rate — ingredients × RollsFor, with no hedging.
        Assert.Contains("You are grade 5 with Felicity Farseer", answer, StringComparison.Ordinal);
        Assert.Contains("a full grade 5 is 5 rolls", answer, StringComparison.Ordinal);
        Assert.Contains("5 × Arsenic", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARankTooLowToRollTheGradeIsQuotedAsTheGateItIsWithItsPrice()
    {
        // Elvira Martuuk grades this to 5 and the Commander is rank 2 with her; Farseer is not
        // unlocked in this fixture, so the best reachable engineer cannot roll it at all.
        var registry = Registry(
            Commander,
            """
            {"timestamp":"3311-01-01T00:01:00Z","event":"EngineerProgress","Engineers":[
              {"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Unlocked","Rank":2}]}
            """,
            Loadout);

        var answer = await Ask(registry, "get_blueprint", ("blueprint", "Increased FSD Range"));

        Assert.Contains("You are grade 2 with Elvira Martuuk", answer, StringComparison.Ordinal);
        Assert.Contains("grade 5 needs rank 5", answer, StringComparison.Ordinal);

        // The gate is stated with how a rank is actually raised beside it. It used to be
        // priced in credits, from a wiki, and that was never how the rank went up (#26).

        // And never a shopping list, because below the rank there is no roll count at all.
        Assert.DoesNotContain("rolls, so", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoJournalTheBlueprintStillAnswersAboutTheGame()
    {
        // Capabilities are state, not guards: what a blueprint costs is true for everybody, and
        // only the paragraph about this Commander depends on a journal.
        var answer = await Ask(Registry(), "get_blueprint", ("blueprint", "Increased FSD Range"));

        Assert.Contains("Per application:", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("You are grade", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModuleKindListsWhatItCanTakeRatherThanEveryRecipe()
    {
        var answer = await Ask(Flying(), "get_blueprint", ("module", "Frame Shift Drive"));

        Assert.Contains("Frame Shift Drive takes ", answer, StringComparison.Ordinal);
        Assert.Contains("Increased FSD Range — to grade 5", answer, StringComparison.Ordinal);

        // Experimentals are per module kind rather than global, which is the thing coriolis gets
        // wrong and the reason the table carries them that way.
        Assert.Contains("Experimental effects: ", answer, StringComparison.Ordinal);
        Assert.Contains("Mass Manager", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABlueprintNobodyHasHeardOfIsRefusedWithNearMisses()
    {
        var answer = await Ask(Flying(), "get_blueprint", ("blueprint", "Increased FSD Rnge"));

        Assert.Contains("I don't know a blueprint called", answer, StringComparison.Ordinal);
        Assert.Contains("Increased FSD Range", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskedForNothingItSaysWhatItNeeds()
    {
        var answer = await Ask(Flying(), "get_blueprint");

        Assert.Contains("Name a blueprint", answer, StringComparison.Ordinal);
    }

    /// <summary>
    /// The distinction <see cref="BlueprintKind"/> exists for. Multiplying a synthesis recipe by a
    /// roll count is arithmetic on the wrong kind of thing, so the roll paragraph must not appear
    /// for one.
    /// </summary>
    [Fact]
    public async Task ARecipeThatIsNotAModificationIsNeverMultipliedByARollCount()
    {
        var synthesis = BlueprintCatalogue.All.First(blueprint => blueprint.Kind == BlueprintKind.Synthesis);

        var answer = await Ask(Flying(), "get_blueprint", ("blueprint", synthesis.Name));

        Assert.DoesNotContain("Per application:", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("a full grade", answer, StringComparison.Ordinal);
    }

    // ---- get_module_engineering ----------------------------------------------------------------

    [Fact]
    public async Task EveryEngineeredModuleIsAccountedForWithItsGradeAndProgress()
    {
        var answer = await Ask(Flying(), "get_module_engineering");

        Assert.Contains("5 of 6 modules engineered", answer, StringComparison.Ordinal);

        // The grade comes from Level and the verdict from the 0.85 band, which are two different
        // facts about the same module.
        Assert.Contains("Frame Shift Drive — FSD LongRange, grade 5, finished (1.0)", answer, StringComparison.Ordinal);
        Assert.Contains("Thrusters in Main Engines — Engine Dirty, grade 3", answer, StringComparison.Ordinal);

        // The slot is dropped where it only repeats the module's name and kept where a ship has
        // several of the thing.
        Assert.DoesNotContain("Frame Shift Drive in Frame Shift Drive", answer, StringComparison.Ordinal);
        Assert.Contains("Shield Booster in Tiny Hardpoint1", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFinishedGradeIsFinishedRatherThanNinetyThreePercentOfTheWay()
    {
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "frame shift drive"));

        Assert.Contains("FSD LongRange at grade 5, rolled by Felicity Farseer.", answer, StringComparison.Ordinal);
        Assert.Contains("The grade is finished", answer, StringComparison.Ordinal);
        Assert.Contains("0.85 is as far as the game insists", answer, StringComparison.Ordinal);

        Assert.Contains("Experimental effect: Mass Manager.", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APartRolledGradeSaysHowManyRollsAreLeft()
    {
        // Grade 3 at rank 4 fills a quarter at a time, so 0.4 leaves three rolls — counted towards a
        // full 1.0 rather than towards the 0.85 band, because the band is where the game stops
        // insisting rather than a target to aim at.
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "MainEngines"));

        Assert.Contains("The grade is part rolled, at 0.4 of 1 — 3 more rolls to fill it", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoRankBehindItTheFillIsReportedAndTheRollCountIsNot()
    {
        // The same module with nothing known about the Commander's standing. The step size is a
        // function of rank − grade and of nothing else, so a roll count here would be invented.
        var answer = await Ask(Registry(Commander, Loadout), "get_module_engineering", ("module", "MainEngines"));

        Assert.Contains("The grade is part rolled, at 0.4 of 1.", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("more roll", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRollIsReportedInRealUnitsWithTheDirectionTheRightWayRound()
    {
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "frame shift drive"));

        Assert.Contains("What the roll did:", answer, StringComparison.Ordinal);

        // The two halves of LessIsGood, and the reason it is read as 0/1 rather than as a JSON
        // boolean: read as one, every improvement on mass reports backwards.
        Assert.Contains("FSD Optimal Mass 1692.75, was 1050 (+642.75, better)", answer, StringComparison.Ordinal);
        Assert.Contains("Mass 26, was 20 (+6, worse)", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModuleThatArrivedAlreadyEngineeredIsSaidToBeOneRatherThanMisreported()
    {
        // 27 of 772 engineered modules measured carry engineer id 399999 and no name. That is a
        // real state, and reporting it as a parse failure or inventing an engineer would both be
        // wrong.
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "cargo rack"));

        Assert.Contains("No engineer is named on it", answer, StringComparison.Ordinal);
        Assert.Contains("already", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnengineeredModuleSaysSoRatherThanNotBeingFound()
    {
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "power plant"));

        Assert.Contains("Not engineered.", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguityIsAnAnswerRatherThanAGuess()
    {
        // "shield booster" names two fitted modules here, rolled to different grades. Picking one
        // silently would report about the wrong module with total confidence.
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "shield booster"));

        Assert.Contains("2 fitted modules match 'shield booster'", answer, StringComparison.Ordinal);
        Assert.Contains("Tiny Hardpoint1", answer, StringComparison.Ordinal);
        Assert.Contains("Tiny Hardpoint2", answer, StringComparison.Ordinal);

        // And each with its own state, because which of the two is worth finishing is the actual
        // question behind asking.
        Assert.Contains("grade 5, finished (1.0)", answer, StringComparison.Ordinal);
        Assert.Contains("grade 3", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingFittedMatchesIsAnsweredWithWhatIsActuallyThere()
    {
        var answer = await Ask(Flying(), "get_module_engineering", ("module", "warp core"));

        Assert.Contains("Nothing fitted matches 'warp core'", answer, StringComparison.Ordinal);
        Assert.Contains("Frame Shift Drive", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoLoadoutItNamesWhatItIsWaitingFor()
    {
        var answer = await Ask(Registry(Commander), "get_module_engineering");

        Assert.Contains("when you enter the game", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoJournalAtAllItSaysThatRatherThanReportingAnEmptyShip()
    {
        var answer = await Ask(Registry(), "get_module_engineering");

        Assert.Contains("No Elite Dangerous journal", answer, StringComparison.Ordinal);
    }
}
