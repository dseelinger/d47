using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Messages;

namespace D47.App.Panel;

/// <summary>Everything the Stories tab reads and the few things it may do (Phase 47).</summary>
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
    StoryRatingClient? Ratings = null);
