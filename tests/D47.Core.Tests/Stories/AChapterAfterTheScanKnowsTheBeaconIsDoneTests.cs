using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Stories.TheFinaleEndsWhereItsFirstChapterSaidTests;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter written after the beacon scan is told, in the outline and the objectives, that the beacon trip is over.</summary>
public sealed class AChapterAfterTheScanKnowsTheBeaconIsDoneTests
{
    private const string Done = "The Guardian beacon scan has already happened, and the Guardian cores came aboard with it.";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static async Task<IReadOnlyList<string>> Prompts(int? daysSinceBeacon)
    {
        var provider = Provider(Beats(Arrive("Closer")));
        var ask = Ask(finale: null) is var asked && asked.Story is { } story
            ? asked with { Story = story with { DaysSinceBeacon = daysSinceBeacon } }
            : throw new InvalidOperationException("The ask has no story.");

        Assert.True((await Generator(provider).GenerateAsync(ask, Now, CancellationToken.None)).Succeeded);

        return [.. provider.Requests.Select(request => request.Prompt.History[0].Text)];
    }

    [Fact]
    public async Task TheOutlineAndTheObjectivesAreToldTheScanHappened()
    {
        var prompts = await Prompts(daysSinceBeacon: 0);

        Assert.Equal(2, prompts.Count);
        Assert.All(prompts, prompt => Assert.Contains(Done, prompt));
    }

    [Fact]
    public async Task AChapterBeforeTheScanIsNotTold()
    {
        var prompts = await Prompts(daysSinceBeacon: null);

        Assert.All(prompts, prompt => Assert.DoesNotContain(Done, prompt));
    }
}
