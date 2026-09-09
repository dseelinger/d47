using D47.Core.Adventures;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Everything the Adventures tab reads and the few things it may do (Phase 47).</summary>
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
    Action OpenSettings);
