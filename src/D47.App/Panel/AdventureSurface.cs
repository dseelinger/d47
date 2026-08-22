using D47.Core.Adventures;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// Everything the Adventures tab reads and the few things it may do (list.md Phase 47).
/// <para>
/// A record of delegates rather than the host, so the page owns no composition root and a test can
/// hand it a book and nothing else. <paramref name="Say"/> is the one way the tab makes a sound:
/// the opening on Begin, the core's reply to an ask, a refusal — each spoken and recorded exactly
/// as a timer going off is.
/// </para>
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
    Action OpenSettings);
