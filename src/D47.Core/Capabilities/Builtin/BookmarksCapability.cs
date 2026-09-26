using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Naming the current destination so a Commander can return to it by voice (#489).</summary>
public static class BookmarksCapability
{
    public const string Id = "bookmarks";

    public const string BookmarkTool = "bookmark_destination";

    public const string ListTool = "list_bookmarks";

    public const string RenameTool = "rename_bookmark";

    public const string DeleteTool = "delete_bookmark";

    private const string NoCommander = "Nobody is flying, so there is nothing to bookmark.";

    private const string NothingTargeted = "Nothing is targeted. Select a system or a station first.";

    private const string AnotherSystem =
        "That is in another system, and Elite does not name the system. Target the system itself.";

    private const string HowToMakeOne = "Say 'bookmark this' with a system or station targeted.";

    public static CapabilityDescriptor Create(
        BookmarkStore? store,
        Func<string> frontierId,
        Func<GameStatus>? status,
        Func<CommanderGameState?> commander,
        Func<PhraseBook> phraseBook,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Bookmarks",
        Summary = "Name the current destination, so 'set course for' the name returns to it later.",
        Examples = ["bookmark this", "what are my bookmarks", "set course for Jameson Memorial"],
        Display = new CapabilityDisplay { PanelTitle = "Bookmarks", Order = 51 },
        Tools =
        [
            new ToolDefinition
            {
                Name = BookmarkTool,
                Description =
                    "Save the current destination (from Status.json) as a bookmark, so 'set course for "
                    + "<name>' returns to it later. Without a name it is saved at once under the "
                    + "destination's own name, which can be renamed afterwards.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "What to call the bookmark. Optional — omit to name it after the destination.",
                        Required = false,
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("bookmark this", new Dictionary<string, string>(StringComparer.Ordinal)),
                    new ToolCommandPhrase(
                        "bookmark the destination", new Dictionary<string, string>(StringComparer.Ordinal)),
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(Bookmark(store, frontierId(), status, commander(), phraseBook(), now(), arguments)),
            },

            new ToolDefinition
            {
                Name = ListTool,
                Description = "List the Commander's bookmarks, each with the system it points at.",
                Commands =
                [
                    new ToolCommandPhrase(
                        "what are my bookmarks", new Dictionary<string, string>(StringComparer.Ordinal)),
                    new ToolCommandPhrase(
                        "list my bookmarks", new Dictionary<string, string>(StringComparer.Ordinal)),
                ],
                Handler = (_, _) => Task.FromResult(List(store, frontierId())),
            },

            new ToolDefinition
            {
                Name = RenameTool,
                Description = "Rename an existing bookmark. The system it points at does not change.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "The bookmark's current name.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "new_name",
                        Type = ToolParameterType.String,
                        Description = "The name to give it instead.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(Rename(store, frontierId(), phraseBook(), arguments)),
            },

            new ToolDefinition
            {
                Name = DeleteTool,
                Description = "Delete a bookmark by name.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "The bookmark's name, exactly as listed.",
                        Required = true,
                    },
                ],

                // A misheard name passed by the model would delete the wrong bookmark with no way back —
                // only the panel, a hotkey or the Commander's own "delete"/"forget" phrase reach it.
                Protected = true,
                Handler = (arguments, _) => Task.FromResult(Delete(store, frontierId(), arguments)),
            },
        ],
    };

    /// <summary>"Delete bookmark X" and "forget bookmark X", one pair per bookmark this Commander has made.</summary>
    public static IEnumerable<DynamicCommand> Phrases(BookmarkStore? store, Func<string> frontierId)
    {
        var fid = frontierId();

        if (store is null || fid.Length == 0)
        {
            yield break;
        }

        foreach (var bookmark in store.For(fid))
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = bookmark.Name };

            yield return new DynamicCommand($"delete bookmark {bookmark.Name}", Id, DeleteTool, arguments);
            yield return new DynamicCommand($"forget bookmark {bookmark.Name}", Id, DeleteTool, arguments);
        }
    }

    private static ToolResult Bookmark(
        BookmarkStore? store,
        string frontierId,
        Func<GameStatus>? status,
        CommanderGameState? commander,
        PhraseBook phraseBook,
        DateTimeOffset at,
        ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error(NoCommander);
        }

        var (elitesName, system, error) = ResolveDestination(status?.Invoke(), commander?.Location);

        if (error is not null)
        {
            return ToolResult.Error(error);
        }

        var existingNames = store.For(frontierId).Select(bookmark => bookmark.Name).ToList();
        var taken = TakenPhrases(phraseBook);

        string name;

        if (arguments.TryGetString("name", out var given) && !string.IsNullOrWhiteSpace(given))
        {
            var trimmed = given.Trim();

            if (BookmarkValidation.Problem(trimmed, existingNames, taken) is { } problem)
            {
                return ToolResult.Error(problem);
            }

            name = trimmed;
        }
        else
        {
            name = BookmarkNaming.FromDestination(elitesName, existingNames, taken);
        }

        if (!store.Add(frontierId, name, system!, at))
        {
            return ToolResult.Error($"You already have a bookmark called \"{name}\".");
        }

        return ToolResult.Ok($"Bookmarked {name}, in {system}. Say 'set course for {name}' to go there.");
    }

    /// <summary>
    /// The destination's own name, which naming reads from, and the system a bookmark points at: the current
    /// system for a body or station in it.
    /// </summary>
    private static (string? ElitesName, string? System, string? Error) ResolveDestination(
        GameStatus? status, JournalLocation? location)
    {
        if (status?.Destination is not { } destination)
        {
            return (null, null, NothingTargeted);
        }

        if (destination.Body == 0)
        {
            return string.IsNullOrWhiteSpace(destination.Name)
                ? (null, null, NothingTargeted)
                : (destination.Name, destination.Name, null);
        }

        if (location?.SystemAddress == destination.System && location?.StarSystem is { } current)
        {
            return (destination.Name, current, null);
        }

        return (null, null, AnotherSystem);
    }

    private static ToolResult List(BookmarkStore? store, string frontierId)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Ok(HowToMakeOne);
        }

        var bookmarks = store.For(frontierId);

        if (bookmarks.Count == 0)
        {
            return ToolResult.Ok(HowToMakeOne);
        }

        var lines = bookmarks.Select(bookmark => $"{bookmark.Name}, in {bookmark.System}");

        return ToolResult.Ok(string.Join(". ", lines) + ".");
    }

    private static ToolResult Rename(
        BookmarkStore? store, string frontierId, PhraseBook phraseBook, ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error(NoCommander);
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("Which bookmark? Renaming needs its current name.");
        }

        if (!arguments.TryGetString("new_name", out var newName) || string.IsNullOrWhiteSpace(newName))
        {
            return ToolResult.Error("Renaming needs a new name to give it.");
        }

        var trimmedName = name.Trim();
        var trimmedNewName = newName.Trim();

        var existingNames = store.For(frontierId)
            .Where(bookmark => !string.Equals(bookmark.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
            .Select(bookmark => bookmark.Name)
            .ToList();

        var taken = BookmarkValidation.Less(TakenPhrases(phraseBook), trimmedName);

        if (BookmarkValidation.Problem(trimmedNewName, existingNames, taken) is { } problem)
        {
            return ToolResult.Error(problem);
        }

        return store.Rename(frontierId, trimmedName, trimmedNewName)
            ? ToolResult.Ok($"Renamed \"{trimmedName}\" to \"{trimmedNewName}\".")
            : ToolResult.Error($"There is no bookmark called \"{trimmedName}\".");
    }

    private static ToolResult Delete(BookmarkStore? store, string frontierId, ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error(NoCommander);
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("Which bookmark? Deleting needs its name.");
        }

        var trimmed = name.Trim();

        return store.Delete(frontierId, trimmed)
            ? ToolResult.Ok($"Deleted \"{trimmed}\".")
            : ToolResult.Error($"There is no bookmark called \"{trimmed}\".");
    }

    /// <summary>Every phrase already in use, so a new bookmark cannot take one (#489).</summary>
    private static IReadOnlyCollection<string> TakenPhrases(PhraseBook phraseBook) =>
        [.. phraseBook.Entries
            .Where(entry => entry.Source != PhraseSource.SpokenKeyword)
            .Select(entry => entry.Phrase)];
}

/// <summary>Choosing a default bookmark name from what Elite calls the destination (#489).</summary>
public static class BookmarkNaming
{
    /// <summary>
    /// The name a bookmark gets when none is given: the destination's name, cleaned and de-duplicated, or
    /// "Bookmark N" where Elite gave no sayable name.
    /// </summary>
    public static string FromDestination(
        string? elitesName, IReadOnlyCollection<string> existingNames, IReadOnlyCollection<string> taken)
    {
        var placeholder = elitesName is null || elitesName.TrimStart().StartsWith('$');
        var cleaned = placeholder ? string.Empty : Clean(elitesName!);

        var baseName = cleaned.Length == 0
            ? LowestFreeBookmarkNumber(existingNames, taken)
            : cleaned;

        if (BookmarkValidation.Problem(baseName, existingNames, taken) is null)
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = WithSuffix(baseName, suffix);

            if (BookmarkValidation.Problem(candidate, existingNames, taken) is null)
            {
                return candidate;
            }
        }
    }

    private static string LowestFreeBookmarkNumber(
        IReadOnlyCollection<string> existingNames, IReadOnlyCollection<string> taken)
    {
        for (var n = 1; ; n++)
        {
            var candidate = $"Bookmark {n}";

            if (BookmarkValidation.Problem(candidate, existingNames, taken) is null)
            {
                return candidate;
            }
        }
    }

    /// <summary>Every character but a letter, digit or space becomes a space, collapsed and cut at a word boundary.</summary>
    private static string Clean(string name)
    {
        var scrubbed = new string([.. name.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ')]);
        var collapsed = string.Join(' ', scrubbed.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return CutAtWordBoundary(collapsed, BookmarkValidation.MaxNameLength);
    }

    private static string CutAtWordBoundary(string name, int maxLength)
    {
        if (name.Length <= maxLength)
        {
            return name;
        }

        var cut = name[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');

        return lastSpace > 0 ? cut[..lastSpace] : cut;
    }

    private static string WithSuffix(string baseName, int suffix)
    {
        var tail = $" {suffix}";
        var roomForBase = BookmarkValidation.MaxNameLength - tail.Length;
        var trimmedBase = baseName.Length > roomForBase ? baseName[..roomForBase].TrimEnd() : baseName;

        return trimmedBase + tail;
    }
}
