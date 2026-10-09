using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Only the Commander answers a story's ending; the model is refused, and the answer is kept on the story.</summary>
[Trait("Category", "Integration")]
public sealed class AnEndingChoiceIsTheCommandersTests
{
    private static StoryFixtures Finished()
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-400),
            State = StoryState.Finished,
            StoppedAt = Now,
        });

        fixtures.Director.EndingPosted("F1", Id, Now);
        return fixtures;
    }

    private static CapabilityRegistry Registry(StoryFixtures fixtures, List<StoryAnswer> answers) =>
        CapabilityRegistry.Build([AdventureCapability.Create(endingAnswer: new AdventureCapability.EndingAnswer
        {
            Answer = option =>
            {
                var answer = fixtures.Director.Answer("F1", option);
                answers.Add(answer);
                return answer;
            },
        })]);

    [Fact]
    public async Task TheModelCannotAnswerIt()
    {
        using var fixtures = Finished();
        var answers = new List<StoryAnswer>();
        var result = await Registry(fixtures, answers).InvokeAsync(
            AdventureCapability.AnswerEndingTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(answers);
        Assert.Null(fixtures.Stories.Find("F1", Id)!.EndingChoice);
    }

    [Fact]
    public async Task TheCommanderAnswersItAndTheChoiceIsKept()
    {
        using var fixtures = Finished();
        var answers = new List<StoryAnswer>();
        var result = await Registry(fixtures, answers).InvokeAsync(
            AdventureCapability.AnswerEndingTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("keep", fixtures.Stories.Find("F1", Id)!.EndingChoice);
        Assert.Equal("She stays.", Assert.Single(answers).After);
    }

    [Fact]
    public void AnEndingIsAnsweredOnce()
    {
        using var fixtures = Finished();

        Assert.Null(fixtures.Director.Answer("F1", 1).Refusal);
        Assert.NotNull(fixtures.Director.Answer("F1", 1).Refusal);
    }

    [Fact]
    public void AnOptionThatIsNotOfferedIsRefused()
    {
        using var fixtures = Finished();

        Assert.NotNull(fixtures.Director.Answer("F1", 2).Refusal);
        Assert.Null(fixtures.Stories.Find("F1", Id)!.EndingChoice);
    }
}
