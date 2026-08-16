using System.Globalization;
using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Checklists;

/// <summary>What a proposal is asking to do.</summary>
public enum ProposalKind
{
    /// <summary>Add lines to a list.</summary>
    Add,

    /// <summary>Tick a line the Commander wrote.</summary>
    Complete,

    /// <summary>Un-tick one. The same act in the other direction, and equally theirs to make.</summary>
    Reopen,

    /// <summary>Take one off the list. Changing your mind, which is not the same as finishing.</summary>
    Remove,

    /// <summary>Replace one plan's items in one scope — a revision, applied as a diff.</summary>
    Plan,
}

/// <summary>
/// Something d47 would like to do to the Commander's checklist, waiting for them to agree.
/// <para>
/// <b>The distinction that makes this work is between observing and asserting.</b> A build intent
/// naming a blueprint and a grade is checked against the <c>Loadout</c> and the result is simply
/// stated, because that is reading the journal back to the Commander. A line they wrote in their
/// own words is something no table can settle, so d47 proposes and waits.
/// </para>
/// </summary>
public sealed record ChecklistProposal
{
    public required string Id { get; init; }

    public required string CommanderFid { get; init; }

    public required ProposalKind Kind { get; init; }

    /// <summary>What d47 would do, in the sentence the Commander is agreeing to.</summary>
    public required string Summary { get; init; }

    public ChecklistScope Scope { get; init; } = ChecklistScope.Universal;

    public ChecklistSource Source { get; init; } = ChecklistSource.Commander;

    /// <summary>The lines to add, or the plan's whole item set. Empty for a completion.</summary>
    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];

    /// <summary>Which item a <see cref="ProposalKind.Complete"/> is about.</summary>
    public string? TargetKey { get; init; }
}

/// <summary>
/// The proposals file — <c>data/checklist-proposals.json</c> — and the whole of the trust
/// boundary this phase turns on (list.md Phase 17, "LLM Ship AI may propose that a checklist item
/// is done").
/// <para>
/// <b>Proposing is model-callable and committing is not, into two different files so the boundary
/// is inspectable by looking at <c>data/</c>.</b> A malformed model write then cannot corrupt the
/// Commander's own list, and the worst a hostile in-game message achieves is a proposal that gets
/// declined. That is the same rule <see cref="Capabilities.SettingRow.Protected"/> already
/// enforces for safety-critical settings, arrived at from the other direction: there the caller
/// is refused, and here the caller is given somewhere else to write.
/// </para>
/// <para>
/// Same shape as <see cref="ChecklistStore"/> — polled on write time, saved through
/// <see cref="AtomicFile"/>, problems reported rather than swallowed — because it is the same
/// kind of thing and a second shape would be a second set of bugs.
/// </para>
/// </summary>
public sealed class ChecklistProposalStore(string path, ILogger<ChecklistProposalStore> logger)
{
    private readonly Lock _gate = new();

    private IReadOnlyList<ChecklistProposal> _pending = [];
    private IReadOnlyList<ChecklistProblem> _problems = [];
    private DateTime _stamp;

    public string Path => path;

    public event Action? Changed;

    public IReadOnlyList<ChecklistProposal> Pending
    {
        get
        {
            lock (_gate)
            {
                return _pending;
            }
        }
    }

    public IReadOnlyList<ChecklistProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    public IReadOnlyList<ChecklistProposal> PendingFor(string? fid) =>
        [.. Pending.Where(proposal => string.Equals(proposal.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal))];

    public bool Poll()
    {
        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                if (_stamp == default)
                {
                    return false;
                }

                lock (_gate)
                {
                    _pending = [];
                    _problems = [];
                    _stamp = default;
                }

                Changed?.Invoke();
                return true;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat the proposals file");
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        Reload(written);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Records a proposal. Refuses once there are already too many outstanding — a model that
    /// proposed forever would bury the one the Commander was about to answer, and this is the one
    /// store an untrusted path can grow.
    /// </summary>
    public string? Add(ChecklistProposal proposal)
    {
        Poll();

        var pending = Pending;

        if (pending.Count >= ChecklistLimits.MaxPendingProposals)
        {
            return $"There are already {pending.Count} proposals waiting. "
                   + "Accept or decline those first.";
        }

        var id = "p-" + Next(pending).ToString(CultureInfo.InvariantCulture);

        Write([.. pending, proposal with { Id = id }]);

        return null;
    }

    /// <summary>
    /// Takes the proposals for one Commander off the file, leaving anybody else's alone. The
    /// committing half calls this; nothing model-callable can.
    /// </summary>
    public IReadOnlyList<ChecklistProposal> Take(string? fid)
    {
        Poll();

        var mine = PendingFor(fid);

        if (mine.Count == 0)
        {
            return [];
        }

        Write([.. Pending.Except(mine)]);

        return mine;
    }

    /// <summary>Removes one proposal, whichever way it was answered.</summary>
    public ChecklistProposal? Take(string? fid, string id)
    {
        Poll();

        var found = PendingFor(fid)
            .FirstOrDefault(proposal => string.Equals(proposal.Id, id, StringComparison.OrdinalIgnoreCase));

        if (found is null)
        {
            return null;
        }

        Write([.. Pending.Where(proposal => !ReferenceEquals(proposal, found))]);

        return found;
    }

    public void Write(IReadOnlyList<ChecklistProposal> proposals)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(
            path,
            JsonSerializer.Serialize(new ProposalFile { Proposals = [.. proposals] }, ChecklistStore.Json));

        _stamp = default;
        Poll();
    }

    private static int Next(IEnumerable<ChecklistProposal> pending)
    {
        var taken = pending
            .Select(proposal => proposal.Id.StartsWith("p-", StringComparison.OrdinalIgnoreCase)
                                && int.TryParse(proposal.Id[2..], CultureInfo.InvariantCulture, out var number)
                ? number
                : 0)
            .ToHashSet();

        var next = 1;

        while (taken.Contains(next))
        {
            next++;
        }

        return next;
    }

    private void Reload(DateTime written)
    {
        ProposalFile? file;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            file = JsonSerializer.Deserialize<ProposalFile>(stream, ChecklistStore.Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            lock (_gate)
            {
                _pending = [];
                _problems = [new ChecklistProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _stamp = written;
            }

            logger.LogWarning(ex, "The checklist proposals file could not be read");
            return;
        }

        var accepted = new List<ChecklistProposal>();
        var problems = new List<ChecklistProblem>();

        foreach (var proposal in file?.Proposals ?? [])
        {
            if (Problem(proposal) is { } reason)
            {
                problems.Add(new ChecklistProblem(proposal?.Summary ?? "a proposal", reason));
                continue;
            }

            accepted.Add(proposal);
        }

        lock (_gate)
        {
            _pending = accepted;
            _problems = problems;
            _stamp = written;
        }

        logger.LogInformation(
            "Loaded {Count} checklist proposals from {Path} ({Problems} refused)",
            accepted.Count,
            path,
            problems.Count);
    }

    /// <summary>
    /// A proposal is checked exactly as hard as the list it wants to change — every item it
    /// carries goes through <see cref="ChecklistValidation"/>. A proposal that could not be
    /// applied is one the Commander would accept and watch do nothing.
    /// </summary>
    private static string? Problem(ChecklistProposal? proposal)
    {
        if (proposal is null)
        {
            return "The proposal could not be read at all.";
        }

        if (string.IsNullOrWhiteSpace(proposal.Id))
        {
            return "It has no id, so nothing can accept or decline it.";
        }

        if (string.IsNullOrWhiteSpace(proposal.Summary))
        {
            return "It does not say what it would do.";
        }

        if (proposal.Summary.Length > ChecklistLimits.MaxTextLength)
        {
            return $"It is {proposal.Summary.Length} characters; the most is {ChecklistLimits.MaxTextLength}.";
        }

        var aboutOneItem = proposal.Kind
            is ProposalKind.Complete or ProposalKind.Reopen or ProposalKind.Remove;

        if (aboutOneItem && string.IsNullOrWhiteSpace(proposal.TargetKey))
        {
            return "It proposes changing something and does not say what.";
        }

        if (!aboutOneItem && proposal.Items.Count == 0)
        {
            return "It proposes adding nothing.";
        }

        foreach (var item in proposal.Items)
        {
            if (ChecklistValidation.Problem(item) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }

    private sealed class ProposalFile
    {
        public List<ChecklistProposal> Proposals { get; set; } = [];
    }
}
