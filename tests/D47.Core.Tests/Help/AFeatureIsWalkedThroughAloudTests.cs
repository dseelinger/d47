using System.Reflection;
using D47.Core.Help;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Help;

public class AFeatureIsWalkedThroughAloudTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 20, 0, 0, TimeSpan.Zero);

    private static HelpSection StepOf(int number) => new()
    {
        Number = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Heading = $"Heading {number}",
        Say = $"Say {number}.",
        Expect = $"Expect {number}.",
    };

    private static Walkthrough ThreeSteps() => new(() =>
    [
        new HelpArticle
        {
            CapabilityId = "widgets",
            Title = "Widgets",
            Intro = string.Empty,
            Sections = [StepOf(1), StepOf(2), StepOf(3)],
        },
    ]);

    [Fact]
    public void WalkMeThroughEngineersSaysTheTitleTheCountAndStepOne()
    {
        var engineers = HelpLibrary.ParseHowTo(HelpLibrary.PageFor("engineers"), "engineers")!;
        var steps = engineers.Sections.Where(section => section.Say is { Length: > 0 }).ToArray();

        var said = new Walkthrough().Take("Walk me through engineers.", Start);

        Assert.Equal($"Engineers. {steps.Length} steps. Step 1. {steps[0].Say}", said);
    }

    [Theory]
    [InlineData("walk me through widgets")]
    [InlineData("show me how widgets works")]
    [InlineData("how do I use the widget")]
    public void EachStartingPhraseStartsIt(string phrase) =>
        Assert.Equal("Widgets. 3 steps. Step 1. Say 1.", ThreeSteps().Take(phrase, Start));

    [Fact]
    public void EachMovingPhraseSpeaksTheRightLine()
    {
        var walk = ThreeSteps();
        walk.Take("walk me through widgets", Start);

        Assert.Equal("Step 2. Say 2.", walk.Take("next", Start));
        Assert.Equal("Step 2. Say 2.", walk.Take("again", Start));
        Assert.Equal("Expect 2.", walk.Take("what should I see?", Start));
        Assert.Equal("Step 1. Say 1.", walk.Take("back", Start));
        Assert.Equal("This is the first step. Step 1. Say 1.", walk.Take("back", Start));
        Assert.Equal("Step 2. Say 2.", walk.Take("skip", Start));
        Assert.Equal("Step 3. Say 3.", walk.Take("Next.", Start));
        Assert.Equal("That was the last step. The walkthrough is over.", walk.Take("next", Start));
        Assert.Null(walk.Take("next", Start));
    }

    [Fact]
    public void AQuestionMidWalkthroughFallsThroughAndTheStepStays()
    {
        var walk = ThreeSteps();
        walk.Take("walk me through widgets", Start);

        Assert.Null(walk.Take("where am I", Start));
        Assert.Equal("Step 2. Say 2.", walk.Take("next", Start));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("Cancel.")]
    public void StopOrCancelEndsIt(string phrase)
    {
        var walk = ThreeSteps();
        walk.Take("walk me through widgets", Start);

        Assert.Equal(Walkthrough.Stopped, walk.Take(phrase, Start));
        Assert.Null(walk.Take("back", Start));
    }

    [Fact]
    public void TenMinutesWithoutAWalkthroughPhraseEndsIt()
    {
        var walk = ThreeSteps();
        walk.Take("walk me through widgets", Start);
        walk.Take("what is the weather", Start.AddMinutes(9));

        Assert.Equal("Step 2. Say 2.", walk.Take("next", Start.AddMinutes(9.5)));
        Assert.True(walk.IsRunning(Start.AddMinutes(19)));
        Assert.Null(walk.Take("next", Start.AddMinutes(19.5)));
    }

    [Fact]
    public void AnUnknownNameFallsThrough() =>
        Assert.Null(ThreeSteps().Take("walk me through gadgets", Start));

    [Fact]
    public void WithNothingRunningTheMovingPhrasesAreNotTaken()
    {
        var walk = ThreeSteps();

        Assert.Null(walk.Take("back", Start));
        Assert.Null(walk.Take("stop", Start));
    }

    [Fact]
    public void StartingAnotherReplacesIt()
    {
        var walk = new Walkthrough();
        walk.Take("walk me through engineers", Start);
        walk.Take("next", Start);

        Assert.StartsWith("Help. ", walk.Take("walk me through help", Start));
        Assert.Equal("Step 2. Press HELP on a settings card.", walk.Take("next", Start));
    }

    [Fact]
    public void ItHoldsNoStoreAndNoModel()
    {
        // Every field is a step, a position or a time; nothing that could write to data/ or call a model.
        var allowed = new[]
        {
            typeof(HelpArticle), typeof(IReadOnlyList<HelpSection>), typeof(int), typeof(DateTimeOffset),
            typeof(Lazy<IReadOnlyList<HelpArticle>>),
        };

        var fields = typeof(Walkthrough).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotEmpty(fields);
        Assert.All(fields, field => Assert.Contains(Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType, allowed));
        Assert.DoesNotContain(fields, field => typeof(ILlmProvider).IsAssignableFrom(field.FieldType));
    }
}
