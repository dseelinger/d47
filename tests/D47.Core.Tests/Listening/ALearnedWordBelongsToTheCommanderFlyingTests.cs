using D47.Core.Conversation;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

public class ALearnedWordBelongsToTheCommanderFlyingTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 20, 0, 0, TimeSpan.Zero);

    private readonly string _root = Directory.CreateTempSubdirectory("d47-wording").FullName;
    private readonly HeardNamesStore _names;
    private readonly LearnedPhrasesStore _phrases;
    private IReadOnlyCollection<string> _reserved = [];
    private string? _flying;

    public ALearnedWordBelongsToTheCommanderFlyingTests()
    {
        _names = new HeardNamesStore(NamesFile, NullLogger<HeardNamesStore>.Instance);
        _phrases = new LearnedPhrasesStore(PhrasesFile, NullLogger<LearnedPhrasesStore>.Instance);
        _names.RememberNames(
            new Dictionary<string, SpokenNames>(StringComparer.Ordinal)
            {
                ["F1"] = SpokenNames.Empty.With(["Eurybia"]),
                ["F2"] = SpokenNames.Empty.With(["Eurybia"]),
            },
            At);
    }

    private string NamesFile => Path.Combine(_root, "heard-names.json");

    private string PhrasesFile => Path.Combine(_root, "phrases.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LearnedWording Wording() =>
        new(_names, _phrases, () => _flying, () => _reserved, () => At);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingIsHeardOrLearnedBeforeTheCommanderIsKnown(string? flying)
    {
        _flying = flying;
        var wording = Wording();
        var namesBefore = File.ReadAllText(NamesFile);

        wording.LearnCorrection("Eurebia", "Eurybia");
        wording.LearnPhrase("half throttle please", "throttle to fifty");

        Assert.Equal("go to Eurebia", wording.HeardAsMeant("go to Eurebia"));
        Assert.Null(wording.LearnedPhraseFor("half throttle please"));
        Assert.StartsWith("Nothing yet.", wording.LearnedCorrections(), StringComparison.Ordinal);
        Assert.Equal(namesBefore, File.ReadAllText(NamesFile));
        Assert.False(File.Exists(PhrasesFile));
    }

    [Fact]
    public void ACorrectionAppliesOnlyWhileTheCommanderWhoLearnedItFlies()
    {
        var wording = Wording();
        _flying = "F1";
        wording.LearnCorrection("Eurebia", "Eurybia");

        Assert.Equal("go to Eurybia", wording.HeardAsMeant("go to Eurebia"));

        _flying = "F2";

        Assert.Equal("go to Eurebia", wording.HeardAsMeant("go to Eurebia"));
    }

    [Fact]
    public void APhraseAppliesOnlyWhileTheCommanderWhoTaughtItFlies()
    {
        var wording = Wording();
        _flying = "F1";
        wording.LearnPhrase("half throttle please", "throttle to fifty");

        Assert.Equal("throttle to fifty", wording.LearnedPhraseFor("half throttle please"));

        _flying = "F2";

        Assert.Null(wording.LearnedPhraseFor("half throttle please"));
    }

    [Fact]
    public void TheReservedListIsReadWhenACorrectionIsLearned()
    {
        var wording = Wording();
        _flying = "F1";
        _reserved = ["boost eurebia now"];

        wording.LearnCorrection("Eurebia", "Eurybia");

        Assert.Equal("go to Eurebia", wording.HeardAsMeant("go to Eurebia"));
    }

    [Fact]
    public void TheCommanderFlyingIsReadOnEveryCall()
    {
        _flying = "F1";
        var wording = Wording();
        wording.LearnCorrection("Eurebia", "Eurybia");

        _flying = null;

        Assert.Equal("go to Eurebia", wording.HeardAsMeant("go to Eurebia"));
    }

    [Fact]
    public void ForgettingDropsOnlyTheCorrectionsOfTheCommanderFlying()
    {
        var wording = Wording();
        _flying = "F1";
        wording.LearnCorrection("Eurebia", "Eurybia");
        _flying = "F2";
        wording.LearnCorrection("Eurebia", "Eurybia");

        _flying = "F1";
        wording.ForgetCorrections();

        Assert.Equal("go to Eurebia", wording.HeardAsMeant("go to Eurebia"));

        _flying = "F2";

        Assert.Equal("go to Eurybia", wording.HeardAsMeant("go to Eurebia"));
    }
}
