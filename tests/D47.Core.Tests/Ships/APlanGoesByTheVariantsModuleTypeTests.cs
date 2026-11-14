using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>A plan with a variant looks its recipes up by module type, so SCO drives and the like get a stepper and engineers (#2421).</summary>
public class APlanGoesByTheVariantsModuleTypeTests
{
    [Fact]
    public void AnScoDriveOffersGradesFiveToOneAndNamesItsEngineers()
    {
        var plan = new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, "Mass Manager", "Frame Shift Drive (SCO)")
        {
            Variant = "int_hyperdrive_overcharge_size8_class5",
        };

        Assert.Equal([5, 4, 3, 2, 1], plan.GradesOffered());
        Assert.False(plan.HasOneGrade());
        Assert.NotEmpty(plan.Recipes("Mass Manager"));

        var engineers = plan.Recipes(plan.Blueprint)
            .Where(recipe => recipe.Grade == 5)
            .SelectMany(recipe => recipe.Engineers)
            .ToList();

        Assert.Contains("Mel Brandon", engineers);
        Assert.Contains("Felicity Farseer", engineers);
        Assert.Contains("Elvira Martuuk", engineers);
    }

    [Theory]
    [InlineData("Prismatic Shield Generator", "int_shieldgenerator_size1_class5_strong", "Reinforced Shields", "Lei Cheung")]
    [InlineData("Bi-Weave Shield Generator", "int_shieldgenerator_size1_class3_fast", "Reinforced Shields", "Lei Cheung")]
    [InlineData("Enhanced Performance Thrusters", "int_engine_size2_class5_fast", "Dirty Drive Tuning", "Professor Palin")]
    public void AModuleNamedApartFromItsTypeStillGetsAStepperAndEngineers(
        string module, string variant, string blueprint, string engineer)
    {
        var plan = new SlotPlan("Slot", blueprint, 5, null, module) { Variant = variant };

        Assert.Contains(5, plan.GradesOffered());
        Assert.Contains(
            engineer,
            plan.Recipes(blueprint).Where(recipe => recipe.Grade == 5).SelectMany(recipe => recipe.Engineers));
        Assert.Contains(
            "Mel Brandon",
            plan.Recipes(blueprint).Where(recipe => recipe.Grade == 5).SelectMany(recipe => recipe.Engineers));
    }

    [Fact]
    public void APlanWithNoVariantMatchesByName()
    {
        var plan = new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, null, "Thrusters");

        Assert.Equal([5, 4, 3, 2, 1], plan.GradesOffered());
    }
}
