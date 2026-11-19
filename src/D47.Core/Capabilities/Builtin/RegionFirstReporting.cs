using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Sends a recorded region first to Canonn, on the Commander's word only.</summary>
public sealed class RegionFirstReporting(
    Func<CommanderGameState?> commander,
    RegionFirstBook? book,
    IRegionFirstReport? sender,
    string clientVersion,
    Func<DateTimeOffset> now)
{
    public const string Tool = "report_region_first";

    /// <summary>The ending of the region-first and galaxy-first lines.</summary>
    public const string Offer = "Say 'report it to Canonn' and I'll send it.";

    public const string Sent = "Sent to Canonn.";

    public const string Unreachable = "I could not reach Canonn just now.";

    public const string NothingWaiting = "There is no region first waiting to be reported.";

    public const string NoName = "I need your Commander name from the game first.";

    public const string AlreadySent = "That region first was already sent to Canonn.";

    public const string NotFound = "I have no region first for that entry in that region.";

    public const string NothingToSend = "That region first has no journal line or position to send.";

    public const string Sending = "A region first is being sent to Canonn now.";

    /// <summary>1 while a send is in flight; one send at a time, so a record is not sent twice.</summary>
    private int _sending;

    /// <summary>The tool, which the panel, a hotkey and the router reach and the model does not.</summary>
    public static ToolDefinition Definition(RegionFirstReporting? reporting) => new()
    {
        Name = Tool,
        Exercise =
            "After a possible region first is announced, say \"report it to Canonn\". D47 sends the find to "
            + "Canonn with your Commander name and says \"Sent to Canonn.\" Saying it again says there is no "
            + "region first waiting. The model cannot do this: it is refused if it asks.",
        Description =
            "Send a possible region or galaxy first to Canonn's codex record, credited to the Commander by name. "
            + "With no arguments, the newest one not yet sent; with entry_id and region_id, that one. The "
            + "Commander's own act: reached only by saying \"report it to Canonn\" and the like, not offered to "
            + "the model, and refused if it asks.",
        Protected = true,
        RefusalExample = "report it to Canonn",
        Parameters =
        [
            ToolParameter.Integer("entry_id", "The EntryID of the codex entry; with region_id, reports that record."),
            ToolParameter.Integer("region_id", "The number of the galactic region, 1 to 42; with entry_id, reports that record."),
        ],
        Commands =
        [
            .. new[] { "canonn", "cannon", "canon" }.SelectMany(name => new[]
            {
                new ToolCommandPhrase($"report it to {name}"),
                new ToolCommandPhrase($"report that to {name}"),
                new ToolCommandPhrase($"report my region first to {name}"),
            }),
            new ToolCommandPhrase("report my region first"),
        ],
        Handler = (arguments, cancellationToken) => reporting is null
            ? Task.FromResult(ToolResult.Error("Nothing here keeps region firsts."))
            : reporting.ReportAsync(arguments, cancellationToken),
    };

    /// <summary>Finds the record, sends it once, and marks it reported on a 200.</summary>
    public async Task<ToolResult> ReportAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (book is null || sender is null)
        {
            return ToolResult.Error("Nothing here keeps region firsts.");
        }

        if (commander() is not { Identity: { FrontierId.Length: > 0, Name: { } name } } state
            || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error(NoName);
        }

        var frontierId = state.Identity.FrontierId;
        var entryId = arguments.Int32("entry_id");
        var regionId = arguments.Int32("region_id");

        if (Interlocked.Exchange(ref _sending, 1) == 1)
        {
            return ToolResult.Ok(Sending);
        }

        try
        {
            RegionFirstRecord? record;

            if (entryId is { } entry && regionId is { } region)
            {
                record = book.Find(frontierId, entry, region);

                if (record is null)
                {
                    return ToolResult.Error(NotFound);
                }

                if (record.Reported is not null)
                {
                    return ToolResult.Ok(AlreadySent);
                }

                if (record is not { Journal.Length: > 0, StarPos: not null })
                {
                    return ToolResult.Error(NothingToSend);
                }
            }
            else
            {
                record = book.NewestUnreported(frontierId);

                if (record is null)
                {
                    return ToolResult.Ok(NothingWaiting);
                }
            }

            var report = new RegionFirstReport(record, name.Trim(), state.Session.Odyssey ?? false, clientVersion);

            if (!await sender.SendAsync(report, cancellationToken).ConfigureAwait(false))
            {
                return ToolResult.Error(Unreachable);
            }

            book.MarkReported(frontierId, record.EntryId, record.RegionId, now());
            return ToolResult.Ok(Sent);
        }
        finally
        {
            Volatile.Write(ref _sending, 0);
        }
    }
}
