using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Messages;

namespace D47.App.Panel;

/// <summary>
/// Everything the Stories tab reads and the few things it may do (Phase 47). <see cref="Recap"/> has the Narrator say the story
/// so far, of the short story with the key given or else the story under way, returning a refusal or null.
/// </summary>
public sealed record AdventureSurface(
    AdventureBook Book,
    AdventureGenerator Generator,
    Func<CommanderGameState?> State,
    Func<string?> Commander,
    Func<DateTimeOffset> Now,
    Action<string> Say,
    Func<bool> ModelAvailable,
    Func<bool> GalaxySearchOn,
    Func<AdventureResolver?> Resolver,
    Action OpenSettings,
    MessageStore? Messages = null,
    D47.Core.Stories.StoryDirector? Stories = null,
    Func<int?, string?>? AnswerEnding = null,
    StoryDownloader? Downloads = null,
    StoryFilterMemory? StoryFilters = null,
    D47.Core.Interface.SpeakerPictures? Pictures = null,
    Func<D47Message, string?>? PlayMessage = null,
    CastVoiceSurface? CastVoices = null,
    StoryRatingClient? Ratings = null,
    CommanderSignal? Switched = null,
    Func<string?, string?>? Recap = null,
    D47.Core.Stories.StoryBench? Bench = null);

/// <summary>Raised when the shown Commander changes, on whichever thread the journal ticks; subscribers post to the UI thread.</summary>
public sealed class CommanderSignal
{
    public event Action? Changed;

    public void Raise() => Changed?.Invoke();
}
