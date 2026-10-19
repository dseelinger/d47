using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>An activity the Commander has changed away from five times is not asked for in a short story.</summary>
public sealed class AShortStoryCannotAskForABarredActivityTests
{
    private const string Spine = """
        {"name": "The Long Way", "premise": "A debt.", "want": "To pay it.", "stake": "Whether it can be.", "turn": "It is owed.", "ending": "Forgiven."}
        """;

    private const string Beats = """
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Job", "function": "finale", "kind": "mission", "count": 2, "mission": "Mission_Courier", "line": "Do it."}
        ]}
        """;

    private static RoundScriptedLlmProvider Provider() => new(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(Beats),
        RoundScriptedLlmProvider.Saying(Repairs.Replacing(Beats, 2)),
        RoundScriptedLlmProvider.Saying(Repairs.Replacing(Beats, 2)));

    [Fact]
    public async Task ADraftWithABarredActivityIsRefused()
    {
        var provider = Provider();
        var generator = AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy());
        generator.Barred = () => ["mission:Mission_Courier"];

        var outcome = await generator.GenerateAsync(new AdventureAsk(), AdventureFixtures.Accepted, TestContext.Current.CancellationToken);

        Assert.Null(outcome.Draft);
        Assert.Contains("complete courier missions, which they have changed away from too often", outcome.Refusal);
        Assert.Contains("no objective may ask for any of them: complete courier missions", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ADraftWithoutABarIsWritten()
    {
        var generator = AdventureGeneratorTests.Generator(Provider(), new AdventureGeneratorTests.Galaxy());

        var outcome = await generator.GenerateAsync(new AdventureAsk(), AdventureFixtures.Accepted, TestContext.Current.CancellationToken);

        Assert.NotNull(outcome.Draft);
    }
}
