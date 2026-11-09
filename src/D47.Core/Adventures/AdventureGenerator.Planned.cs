using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>A chapter whose objectives the planner builds, and whose words alone the model writes.</summary>
public sealed partial class AdventureGenerator
{
    private const string LinesStep = "lines";
    private const string LineRepairStep = "line repairs";
    private const string NotPlanned = "The chapter could not be planned: ";
    private const string Untitled = "Untitled";

    /// <summary>The index <see cref="PlainPart"/> and the plain-check bookkeeping give the title and the spine.</summary>
    private const int SpineIndex = -2;

    /// <summary>Whether a story chapter is an act-one chapter with no drawn activity, which travels and ends on the beacon or on reach.</summary>
    internal static bool Travels(AdventureAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        return ask.Story is { FinaleChapter: null, Slots: null or { Count: 0 } } story && (story.Beacon is not null || story.BeaconAway is not null);
    }

    /// <summary>The seed a chapter's plan is drawn from: the story and chapter, and the chapter it replaces when it replaces one.</summary>
    internal static uint PlanSeed(AdventureAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(ask.Story);

        return (ask.Failed ?? ask.SetAside?.Chapter)?.Previous.Key is { } replaced
            ? ChapterDraw.Seed($"{ask.Story.Id}|{replaced}", ask.Story.Chapter)
            : ChapterDraw.Seed(ask.Story.Id, ask.Story.Chapter);
    }

    /// <summary>What the model wrote for one planned objective.</summary>
    private sealed record ObjectiveWords(string? Title, string? Reason, IReadOnlyList<AdventureLine> Lines, string? Task, IReadOnlyList<string> Boards, IReadOnlyList<string> Leaves)
    {
        public static readonly ObjectiveWords None = new(null, null, [], null, [], []);
    }

    /// <summary>A lines answer: the name and spine when given, the opening and reply, and the words of each objective by its number from 1.</summary>
    private sealed record WrittenLines(string? Name, AdventureSpine? Spine, IReadOnlyList<AdventureLine> Opening, string? Reply, IReadOnlyDictionary<int, ObjectiveWords> Objectives)
    {
        public IReadOnlyList<string> Aboard { get; init; } = [];

        public IReadOnlyList<string> OpeningBoards { get; init; } = [];

        public IReadOnlyList<string> OpeningLeaves { get; init; } = [];
    }

    /// <summary>
    /// The draft as it stands, the refusals a repair round answers, and the plain-wording findings raised earlier on each part
    /// (<see cref="SpineIndex"/> for the title and spine, -1 for the opening, else an objective's head).
    /// </summary>
    private sealed record LineRepair(ReadAnswer Read, string Name, AdventureSpine Spine, IReadOnlyList<Refusal> Refusals, IReadOnlyDictionary<int, List<string>> Raised)
    {
        public bool Opening => Refusals.Any(refusal => refusal.Opening);

        public bool OfSpine => Refusals.Any(refusal => refusal.Spine);

        /// <summary>The head indexes of the objectives the repair writes again.</summary>
        public IReadOnlySet<int> Heads => Refusals.SelectMany(refusal => refusal.Objectives).Select(index => Head(Read.Beats, index)).ToHashSet();

        /// <summary>The plain-wording findings raised earlier on the parts written again that the refusals do not repeat.</summary>
        public IReadOnlyList<string> Earlier => AdventureGenerator.Earlier(Raised, Heads.Concat(Opening ? [-1] : []).Concat(OfSpine ? [SpineIndex] : []), Refusals);
    }

    private async Task<AdventureOutcome> PlannedAsync(AdventureAsk ask, Facts facts, DateTimeOffset now, List<string> notes, long started, CancellationToken cancellationToken, ShortRevision? revision = null)
    {
        var story = ask.Story;
        var rewrite = ask.Rewrite;
        var plan = ask.Plan;
        IReadOnlyList<AdventureBeat>? after = null;

        if (rewrite is not null)
        {
            if (rewrite is { Remark: not null, Constrained: false })
            {
                logger.LogInformation("Draft {Draft}, the Commander's words named no place or activity to plan from", ask.Draft);

                return new AdventureOutcome(
                    null,
                    rewrite.Reply,
                    rewrite.Unfound is { } unfound ? $"I could not find {unfound}. Name a system the galaxy search knows, or an activity." : rewrite.Reply ?? NamePlaceOrActivity,
                    notes);
            }

            var replanned = await RewritePlanAsync(ask, facts, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return AdventureOutcome.Stopped;
            }

            if (replanned.Swapped is { } swapped)
            {
                logger.LogInformation("Draft {Draft}, the closed station's step was moved to {Station}; the objective keeps its words", ask.Draft, replanned.Moved);

                return new AdventureOutcome(swapped, null, null, notes);
            }

            if (replanned.Plan is not { } remade)
            {
                logger.LogWarning("Draft {Draft}, the new objectives could not be planned under the {Rule} rule: {Refusal}", ask.Draft, replanned.Refusal!.Rule, replanned.Refusal.Text);

                return new AdventureOutcome(null, null, NotPlanned + replanned.Refusal.Text, notes);
            }

            (plan, after) = (remade, replanned.After);
            logger.LogInformation("Draft {Draft}, planned {Count} new objectives from objective {From}: {Plan}", ask.Draft, plan.Objectives.Count, rewrite.From + 1, Describe(plan));
        }
        else if (plan is null)
        {
            ChapterPlanResult planned;

            if (story is null)
            {
                planned = await ShortPlanAsync(ask, facts, now, notes, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var places = await ChapterPlacesAsync(ask, cancellationToken).ConfigureAwait(false);

                planned = ChapterPlanner.Plan(ChapterInputs.Of(state(), ask, HomeSystem?.Invoke()), places, PlanSeed(ask));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return AdventureOutcome.Stopped;
            }

            if (planned.Plan is not { } made)
            {
                logger.LogWarning("Draft {Draft}, the chapter could not be planned under the {Rule} rule: {Refusal}", ask.Draft, planned.Refusal!.Rule, planned.Refusal.Text);

                return new AdventureOutcome(null, null, (story is null ? ShortNotPlanned : NotPlanned) + planned.Refusal.Text, notes);
            }

            plan = made;
            logger.LogInformation("Draft {Draft}, planned {Count} objectives: {Plan}", ask.Draft, plan.Objectives.Count, Describe(plan));
        }
        else
        {
            logger.LogInformation("Draft {Draft}, writing the lines again against the same plan", ask.Draft);
        }

        if (rewrite is null && story is { FinaleChapter: 1, Destination: null } && plan.Destination is { } chosen)
        {
            facts = await DestinationDistanceAsync(facts, chosen, cancellationToken).ConfigureAwait(false);
        }

        var away = ShipBases.Exempts(ask) && galaxy() is { } search ? await BaseAsync(search, ask, facts, cancellationToken).ConfigureAwait(false) : null;

        Enter(ask, WriteStage.Objectives);
        var asked = await AskJsonAsync(LinesStep, LinesInstruction(ask, facts, plan, away, repair: null, after, revision), BeatsOutputCeiling, ask.Progress, cancellationToken).ConfigureAwait(false);

        if (asked.Json is null)
        {
            return Kept(Unanswered("The model did not write the chapter's lines.", notes, started, asked, cancellationToken));
        }

        Enter(ask, WriteStage.Checking);

        if (ReadWords(asked.Json) is not { } written)
        {
            return Kept(Failed(null, "The model's lines were not something I could read.", notes, started, asked));
        }

        var name = rewrite?.Chapter.Name ?? written.Name ?? string.Empty;
        var spine = rewrite is not null ? rewrite.Chapter.Spine ?? new AdventureSpine() : written.Spine ?? new AdventureSpine();
        var read = PlannedRead(plan, story, written);
        var heads = Heads(read.Beats);
        var fidelityPassed = new HashSet<int>();
        var plainPassed = new HashSet<int>();
        var raised = new Dictionary<int, List<string>>();
        var unplainRaised = new Dictionary<int, List<string>>();
        var rewrittenByCheck = new HashSet<int>();
        var objected = Check.Places;

        var resolved = await CheckAsync().ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return AdventureOutcome.Stopped;
        }

        LogRefusals(ask, "first", LinesStep, resolved.Refusals);

        Asked? unusedRepair = null;
        var rounds = new Dictionary<Check, int> { [Check.Places] = 0, [Check.Fidelity] = 0, [Check.Plain] = 0 };
        var checkRewrites = 0;

        // A round is a call to the writer. The plain check's own rewrite is put in place once per part between them, and a part
        // it objects to again goes to the writer.
        while (resolved.Refusals.Count > 0 && rounds[objected] < (objected switch { Check.Plain => PlainRepairRounds, Check.Fidelity => FidelityRepairRounds, _ => RepairRounds }))
        {
            var kind = objected;

            if (kind == Check.Plain)
            {
                var (swapped, swappedName, swappedSpine, changed, left) = PlainRewritten(read, name, spine, resolved.Refusals, rewrittenByCheck);

                if (changed.Count > 0)
                {
                    (read, name, spine) = (swapped, swappedName, swappedSpine);
                    rewrittenByCheck.UnionWith(changed);
                    Forget(changed);

                    if (left.Count == 0)
                    {
                        resolved = await CheckAsync().ConfigureAwait(false);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return AdventureOutcome.Stopped;
                        }

                        LogRefusals(ask, $"plain rewrite {(++checkRewrites).ToString(CultureInfo.InvariantCulture)}", PlainStep, resolved.Refusals);
                        continue;
                    }

                    resolved = resolved with { Refusals = left };
                }
            }

            var round = ++rounds[kind];
            var repair = new LineRepair(read, name, spine, resolved.Refusals, unplainRaised);
            var again = await AskJsonAsync(LineRepairStep, LinesInstruction(ask, facts, plan, away, repair, after, revision), BeatsOutputCeiling, ask.Progress, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return AdventureOutcome.Stopped;
            }

            if ((again.Json is null ? null : ReadWords(again.Json)) is not { } answer)
            {
                unusedRepair = again;

                if (again.Reply is { CutOff: { AtContext: true } } or { ContextExceeded: true })
                {
                    break;
                }

                continue;
            }

            unusedRepair = null;
            var replaced = new HashSet<int>();

            foreach (var head in repair.Heads)
            {
                if (answer.Objectives.TryGetValue(heads.IndexOf(head) + 1, out var words))
                {
                    read = WithWords(read, head, words);
                    replaced.Add(head);
                }
            }

            if (repair.Opening && answer.Opening.Count > 0)
            {
                read = read with { Opening = answer.Opening };
                replaced.Add(-1);
            }

            if (repair.OfSpine && answer.Name is { Length: > 0 } renamed && answer.Spine is { IsEmpty: false } respun)
            {
                (name, spine) = (renamed, respun);
                replaced.Add(SpineIndex);
            }

            Forget(replaced);
            rewrittenByCheck.ExceptWith(replaced);
            resolved = await CheckAsync().ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return AdventureOutcome.Stopped;
            }

            LogRefusals(ask, $"{kind switch { Check.Plain => "plain repair", Check.Fidelity => "fidelity repair", _ => "repair" }} {round.ToString(CultureInfo.InvariantCulture)}", again.Step, resolved.Refusals);
        }

        if (resolved.Refusals.Count > 0
            && resolved.Refusals.All(refusal => refusal.Plain && refusal.Unplain.Count > 0 && refusal.Unplain.All(found => found.HasRewrite))
            && PlainRewritten(read, name, spine, resolved.Refusals, new HashSet<int>()) is (var lastRead, var lastName, var lastSpine, _, { Count: 0 }))
        {
            LogRefusals(ask, "plain rewrites put in place after the last round", PlainStep, resolved.Refusals);
            (read, name, spine) = (lastRead, lastName, lastSpine);
            resolved = Coded();
        }

        resolved = KeepWhatTheRewriteKept(NarrateSelfNaming(resolved, ask));

        if (resolved.Refusals.Count > 0)
        {
            return Kept(Failed(
                read.Reply,
                (resolved.Refusals.Any(refusal => refusal.Plain)
                    ? NotPlain
                    : resolved.Refusals.Any(refusal => refusal.Text.StartsWith(Unconfirmed, StringComparison.Ordinal))
                        ? "The story states what its objectives do not confirm: "
                        : "The story cannot stand: ") + string.Join(" ", resolved.Refusals.Select(refusal => refusal.Text)),
                notes,
                started,
                unusedRepair));
        }

        if (rewrite is not null)
        {
            var chapter = rewrite.Chapter with { Beats = [.. rewrite.Chapter.Beats.Take(rewrite.From), .. Grouped(resolved), .. after ?? []] };

            if (story?.Sights is { Count: > 0 } seen)
            {
                chapter = chapter with { Sight = SightSeen(seen, chapter.Beats) ?? chapter.Sight };
            }

            if (story is not null)
            {
                chapter = StoryAboard.Placed(chapter, story.Due ?? []);
            }

            if (AdventureValidation.Problems(chapter) is { Count: > 0 } rewriteProblems)
            {
                return Failed(null, string.Join(" ", rewriteProblems), notes, started);
            }

            return new AdventureOutcome(chapter, rewrite.Reply, null, notes) { Heading = rewrite.Heading };
        }

        var adventure = new Adventure
        {
            Key = AdventureValidation.Key(name),
            Name = name.Trim(),
            Source = AdventureSource.Generated,
            Written = now,
            WrittenBy = personaId(),
            Spine = spine,
            OpeningLines = resolved.Opening,
            Beats = Grouped(resolved),
            Follows = revision is not null ? revision.Draft.Follows : ask.Chapter?.Previous.Key,
        };

        if (story is not null)
        {
            adventure = adventure with
            {
                StoryId = story.Id,
                Aboard = Aboard(story, written.Aboard),
                OpeningBoards = written.OpeningBoards,
                OpeningLeaves = written.OpeningLeaves,
            };

            if (story.Sights is { Count: > 0 } sights)
            {
                adventure = adventure with { Sight = SightSeen(sights, adventure.Beats) ?? adventure.Sight };
            }

            adventure = StoryAboard.Placed(adventure, story.Due ?? []);
        }

        if (AdventureValidation.Problems(adventure) is { Count: > 0 } problems)
        {
            return Kept(Failed(read.Reply, string.Join(" ", problems), notes, started));
        }

        return new AdventureOutcome(adventure, read.Reply, null, notes)
        {
            Destination = story?.FinaleChapter == 1 ? plan.Destination : null,
        };

        AdventureOutcome Kept(AdventureOutcome outcome) => outcome.Draft is null && outcome.Internal ? outcome with { Plan = plan } : outcome;

        void Forget(IEnumerable<int> changed)
        {
            foreach (var index in changed)
            {
                fidelityPassed.Remove(index);
                plainPassed.Remove(index);
            }
        }

        // A detour of one objective may have no line, as AdventureValidation.Problems allows.
        Resolved Coded() => Speakers(PlannedResolved(plan, read, name, spine, rewrite is not null, rewrite is { Chapter.Detour: true, From: 0 } && plan.Objectives.Count == 1 && after is not { Count: > 0 }), read, ask);

        // The code checks first; the fidelity check only on a draft they pass, and the plain check only on one that passes both.
        // An objective a check has passed is not refused by that check again unless its words were written again since.
        async Task<Resolved> CheckAsync()
        {
            objected = Check.Places;
            var coded = Coded();

            if (coded.Refusals.Count > 0 || cancellationToken.IsCancellationRequested)
            {
                return coded;
            }

            var objections = (await FidelityAsync(ask, facts, spine, read.Beats, cancellationToken).ConfigureAwait(false))
                .Where(objection => !objection.Objectives.Any(fidelityPassed.Contains))
                .Select(objection =>
                {
                    var index = objection.Objectives[0];
                    var reasons = objection.Reasons ?? [];
                    var before = raised.TryGetValue(index, out var known) ? known : [];
                    var earlier = before.Where(reason => !reasons.Contains(reason)).ToList();

                    raised[index] = [.. before, .. reasons.Where(reason => !before.Contains(reason))];

                    return earlier.Count == 0
                        ? objection
                        : objection with { Text = Unconfirm(heads.IndexOf(index) + 1, read.Beats[index].Title, reasons, earlier) };
                })
                .ToList();

            fidelityPassed.UnionWith(heads.Except(objections.SelectMany(objection => objection.Objectives)));

            if (objections.Count > 0)
            {
                objected = Check.Fidelity;

                return coded with { Refusals = objections };
            }

            var parts = (rewrite is null ? SpineParts(name, spine).Select(part => part with { Index = SpineIndex }) : [])
                .Concat(BeatParts(read.Opening, read.Beats))
                .ToList();
            var unplain = (await PlainAsync(ask, parts, cancellationToken).ConfigureAwait(false))
                .Select(found => (found, part: parts.First(part => part.Name == found.Part)))
                .Where(pair => !plainPassed.Contains(pair.part.Index!.Value))
                .GroupBy(pair => pair.part.Index!.Value)
                .Select(group => new Refusal(
                    $"{group.Key switch { SpineIndex => "The title and spine", -1 => "The opening", var head => $"Objective {heads.IndexOf(head) + 1} ({read.Beats[head].Title})" }} is not plain: {string.Join(" ", group.Select(pair => pair.found.Describe()))}",
                    group.Key < 0 ? [] : [group.Key])
                {
                    Plain = true,
                    Opening = group.Key == -1,
                    Spine = group.Key == SpineIndex,
                    Unplain = [.. group.Select(pair => pair.found)],
                })
                .ToList();

            plainPassed.UnionWith(parts.Select(part => part.Index!.Value).Except(unplain.Select(PartOf)));
            Raise(unplainRaised, unplain);

            if (unplain.Count > 0)
            {
                objected = Check.Plain;

                return coded with { Refusals = unplain };
            }

            return coded;
        }
    }

    /// <summary>The index of the first step of the objective the step at <paramref name="index"/> belongs to.</summary>
    private static int Head(IReadOnlyList<ReadBeat> beats, int index)
    {
        while (index > 0 && index < beats.Count && beats[index].Continues)
        {
            index--;
        }

        return index;
    }

    /// <summary>The plan's steps in order, each a read beat carrying its trigger's fields, with the words written for its objective on the first step.</summary>
    private static ReadAnswer PlannedRead(ChapterPlan plan, AdventureStory? story, WrittenLines written)
    {
        var beats = new List<ReadBeat>();

        foreach (var (objective, number) in plan.Objectives.Select((objective, index) => (objective, index + 1)))
        {
            var words = written.Objectives.TryGetValue(number, out var given) ? given : ObjectiveWords.None;

            foreach (var (trigger, step) in objective.Steps.Select((trigger, step) => (trigger, step)))
            {
                var beat = StepOf(trigger, story) with { Title = words.Title ?? Untitled, Continues = step > 0, Function = step == 0 ? objective.Function : null };

                beats.Add(step == 0
                    ? beat with { Reason = words.Reason, Lines = words.Lines, Boards = words.Boards, Leaves = words.Leaves, Task = beat.Kind == TriggerKind.Event ? words.Task : null }
                    : beat with { Task = beat.Kind == TriggerKind.Event ? words.Task : null });
            }
        }

        return new ReadAnswer(written.Opening, written.Reply, beats)
        {
            Aboard = written.Aboard,
            OpeningBoards = written.OpeningBoards,
            OpeningLeaves = written.OpeningLeaves,
        };
    }

    /// <summary>The draft with the objective whose first step is at <paramref name="head"/> given <paramref name="words"/>.</summary>
    private static ReadAnswer WithWords(ReadAnswer read, int head, ObjectiveWords words)
    {
        var beats = read.Beats.ToList();
        var title = words.Title ?? Untitled;

        beats[head] = beats[head] with
        {
            Title = title,
            Reason = words.Reason,
            Lines = words.Lines,
            Boards = words.Boards,
            Leaves = words.Leaves,
            Task = beats[head].Kind == TriggerKind.Event ? words.Task : null,
        };

        for (var index = head + 1; index < beats.Count && beats[index].Continues; index++)
        {
            beats[index] = beats[index] with { Title = title, Task = beats[index].Kind == TriggerKind.Event ? words.Task : null };
        }

        return read with { Beats = beats };
    }

    /// <summary>A planned step as a read beat: the trigger, and for an event step the phrase its drawn slot is run by.</summary>
    private static ReadBeat StepOf(AdventureTrigger trigger, AdventureStory? story) =>
        new(Untitled, null, trigger, null, [])
        {
            Say = trigger.Kind == TriggerKind.Event ? SlotOf(trigger, story)?.Say : null,
        };

    /// <summary>The drawn event slot an event step was planned from, or null.</summary>
    private static DrawnActivity? SlotOf(AdventureTrigger trigger, AdventureStory? story) =>
        (story?.Slots ?? []).FirstOrDefault(slot => slot.Kind == TriggerKind.Event && slot.Event == trigger.Event && slot.Where.Equals(trigger.Where));

    /// <summary>
    /// The plan's triggers with the words written for them, one beat per step, and a refusal for each objective with no title, line or
    /// reason, each event step whose task is missing, too long or leaves out the phrase it is run by, and, outside a rewrite, which
    /// keeps the chapter's own, a missing name or spine. With <paramref name="linesOptional"/>, an objective with no line stands.
    /// </summary>
    private static Resolved PlannedResolved(ChapterPlan plan, ReadAnswer read, string name, AdventureSpine spine, bool rewriting = false, bool linesOptional = false)
    {
        var triggers = plan.Objectives.SelectMany(objective => objective.Steps).ToList();
        var heads = Heads(read.Beats);
        var beats = new List<AdventureBeat>();
        var refusals = new List<Refusal>();

        if (!rewriting && string.IsNullOrWhiteSpace(name))
        {
            refusals.Add(new Refusal("The chapter has no \"name\".", []) { Spine = true });
        }

        if (!rewriting && new[] { spine.Premise, spine.Want, spine.Stake, spine.Turn, spine.Ending }.Any(string.IsNullOrWhiteSpace))
        {
            refusals.Add(new Refusal("The spine is missing a field: \"premise\", \"want\", \"stake\", \"turn\" and \"ending\" each need a sentence.", []) { Spine = true });
        }

        for (var index = 0; index < read.Beats.Count; index++)
        {
            var beat = read.Beats[index];
            var trigger = triggers[index];
            var head = Head(read.Beats, index);
            var where = $"Objective {(heads.IndexOf(head) + 1).ToString(CultureInfo.InvariantCulture)} ({beat.Title})";

            if (!beat.Continues)
            {
                if (beat.Title == Untitled)
                {
                    refusals.Add(new Refusal($"{where} has no title.", [index]));
                }

                if (!linesOptional && beat.Lines.All(line => string.IsNullOrWhiteSpace(line.Text)))
                {
                    refusals.Add(new Refusal($"{where} has no line.", [index]));
                }

                if (string.IsNullOrWhiteSpace(beat.Reason))
                {
                    refusals.Add(new Refusal(
                        $"{where} has no \"reason\"; give it one plain sentence saying why the Commander goes: what they go to do or get and how it serves the chapter, "
                        + "and where in the story that comes from, never an order from someone unnamed.",
                        [index]));
                }
            }

            if (trigger.Kind == TriggerKind.Event)
            {
                var task = beat.Task?.Trim();

                if (string.IsNullOrEmpty(task))
                {
                    refusals.Add(new Refusal($"{where} has an event step and no \"task\"; give a phrase of at most {AdventureTrigger.MaxTaskLength.ToString(CultureInfo.InvariantCulture)} characters saying what the Commander does.", [head]));
                }
                else if (task.Length > AdventureTrigger.MaxTaskLength)
                {
                    refusals.Add(new Refusal($"{where}'s \"task\" is {task.Length.ToString(CultureInfo.InvariantCulture)} characters; at most {AdventureTrigger.MaxTaskLength.ToString(CultureInfo.InvariantCulture)}.", [head]));
                }
                else if (beat.Say is { } say && !task.Contains(say, StringComparison.OrdinalIgnoreCase))
                {
                    refusals.Add(new Refusal($"{where} is a tool run, and its \"task\" has to hold the phrase the Commander says, \"{say}\".", [head]));
                }

                trigger = trigger with { Task = task };
            }

            beats.Add(new AdventureBeat
            {
                Title = beat.Title,
                Function = beat.Function,
                Steps = [new BeatStep { Trigger = trigger, Reason = beat.Reason }],
                Lines = beat.Lines,
                Boards = beat.Boards,
                Leaves = beat.Leaves,
            });
        }

        return new Resolved(beats, refusals, plan.Destination)
        {
            Stood = [.. Enumerable.Range(0, beats.Count)],
            Joins = [.. read.Beats.Select(beat => beat.Continues)],
        };
    }

    /// <summary>
    /// The draft with each plain refusal's rewrites put in place, on the title and spine or on the opening and objectives, the
    /// indexes changed (<see cref="SpineIndex"/> for the spine, -1 for the opening), and the refusals left for the writer. A
    /// refusal on a part in <paramref name="once"/> is left for the writer whatever rewrite it gives.
    /// </summary>
    private static (ReadAnswer Read, string Name, AdventureSpine Spine, IReadOnlySet<int> Changed, IReadOnlyList<Refusal> Left) PlainRewritten(
        ReadAnswer read, string name, AdventureSpine spine, IReadOnlyList<Refusal> refusals, IReadOnlySet<int> once)
    {
        var changed = new HashSet<int>();
        var left = refusals.Where(refusal => once.Contains(PartOf(refusal))).ToList();

        refusals = [.. refusals.Where(refusal => !once.Contains(PartOf(refusal)))];

        foreach (var refusal in refusals.Where(refusal => refusal.Spine))
        {
            if (!refusal.Plain || refusal.Unplain.Count == 0)
            {
                left.Add(refusal);
                continue;
            }

            var (swappedName, swappedSpine, unswapped) = Rewritten(name, spine, refusal.Unplain);

            if (unswapped.Count > 0)
            {
                left.Add(refusal);
                continue;
            }

            (name, spine) = (swappedName, swappedSpine);
            changed.Add(SpineIndex);
        }

        var (swapped, beatsChanged, beatsLeft) = Rewritten(read, [.. refusals.Where(refusal => !refusal.Spine)]);

        changed.UnionWith(beatsChanged);
        left.AddRange(beatsLeft);

        return (swapped, name, spine, changed, left);
    }

    /// <summary>The answer to a lines or repair call, or null when it has no objectives to read.</summary>
    private static WrittenLines? ReadWords(string json) => ReadJson(json, allowTrailingCommas: true, root =>
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("objectives", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var objectives = new Dictionary<int, ObjectiveWords>();

        foreach (var element in array.EnumerateArray())
        {
            if (Integer(element, "objective") is { } number && !objectives.ContainsKey(number))
            {
                objectives[number] = new ObjectiveWords(
                    Text(element, "title"),
                    Text(element, "reason"),
                    element.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array ? LinesOf(lines) : [],
                    Text(element, "task"),
                    Ids(element, "boards"),
                    Ids(element, "leaves"));
            }
        }

        var spine = new AdventureSpine
        {
            Premise = Text(root, "premise"),
            Want = Text(root, "want"),
            Stake = Text(root, "stake"),
            Turn = Text(root, "turn"),
            Ending = Text(root, "ending"),
        };

        return new WrittenLines(Text(root, "name"), spine.IsEmpty ? null : spine, ReadOpening(root), Text(root, "reply"), objectives)
        {
            Aboard = Ids(root, "aboard"),
            OpeningBoards = Ids(root, "openingBoards"),
            OpeningLeaves = Ids(root, "openingLeaves"),
        };
    });

    /// <summary>The plan as one line for the log.</summary>
    private static string Describe(ChapterPlan plan) =>
        string.Join("; ", plan.Objectives.Select((objective, index) =>
            $"{(index + 1).ToString(CultureInfo.InvariantCulture)}. {objective.Function}: {string.Join(", then ", objective.Steps.Select(step => step.Describe()))}"));

    /// <summary>One step as the lines writer is told it: an event step by its drawn slot, any other by its trigger.</summary>
    private static string StepLine(AdventureTrigger trigger, AdventureStory? story) =>
        trigger.Kind == TriggerKind.Event && SlotOf(trigger, story) is { } slot ? EventSlotLine(slot) : trigger.Describe();

    /// <summary>The held mission whose destination a planned step goes to, or null.</summary>
    private static Mission? MissionAt(AdventureTrigger trigger, Facts facts) =>
        trigger.Kind is TriggerKind.Arrive or TriggerKind.Dock && trigger.System is { } system
            ? (facts.Missions ?? []).FirstOrDefault(mission =>
                string.Equals(mission.DestinationSystem, system, StringComparison.OrdinalIgnoreCase)
                && (trigger.Kind == TriggerKind.Arrive || mission.DestinationStation is not { Length: > 0 } station || string.Equals(station, trigger.Station, StringComparison.OrdinalIgnoreCase)))
            : null;

    /// <summary>What a planned objective is for, as the lines writer is told it; empty for a drawn activity or a story stop.</summary>
    private static string PurposeLine(PlannedObjective objective, AdventureStory? story) => objective.Purpose switch
    {
        PlannedPurpose.Sight => " It goes to see one of the chapter's sights" + (story?.Sights?.FirstOrDefault(sight => objective.Steps.Any(step => AtSight(sight, step))) is { } seen ? $": {seen.Name}." : "."),
        PlannedPurpose.Goal => story?.Goals?.FirstOrDefault(goal => goal.Objective is { } wanted && objective.Steps.Any(step => step.Kind == wanted.Kind)) is { } goal
            ? $" It is the chapter's Goals objective, working the Commander's goal \"{goal.Name}\"."
            : " It is the chapter's Goals objective.",
        PlannedPurpose.Band => " It is the chapter's last stop, nearer Sol.",
        PlannedPurpose.Leg => " It is the chapter's last stop, on the way to where the chapter is heading.",
        PlannedPurpose.Closer => " It is the chapter's last stop, closer to the finale's destination.",
        PlannedPurpose.Destination => " It is the landing on the finale's destination, where the hidden layer's end takes place.",
        PlannedPurpose.Swap => $" It is the swap into a ship the Commander owns that can make the trip to the Guardian beacon{(story?.Beacon?.SwapTo is { } swap ? $": {swap}" : string.Empty)}.",
        PlannedPurpose.Beacon => " It is collecting the Ancient Key at the Guardian beacon, which ends act one, when the story's Guardian cores come aboard.",
        PlannedPurpose.Reach => " It is met once the Commander's ship can make the trip to the Guardian beacon.",
        PlannedPurpose.Ship => " It is the board into the ship the Commander asked to fly.",
        _ => string.Empty,
    };

    /// <summary>
    /// The lines call for <paramref name="plan"/>: a new chapter's words, or a rewrite's new objectives. <paramref name="after"/> holds the
    /// objectives a rewrite that moved one objective keeps after it; it is null where the rewrite replaced every objective to the end.
    /// </summary>
    private static StoryCall LinesInstruction(AdventureAsk ask, Facts facts, ChapterPlan plan, BaseStep? away, LineRepair? repair, IReadOnlyList<AdventureBeat>? after = null, ShortRevision? revision = null)
    {
        var story = ask.Story;
        var rewriting = ask.Rewrite is not null;
        var text = new StringBuilder();
        var task = plan.Objectives.Any(objective => objective.Steps.Any(step => step.Kind == TriggerKind.Event));

        if (ask.Rewrite is { } rewrite)
        {
            AppendRewrite(text, ask, rewrite, facts, plan, after);
            AppendAway(text, away, ask, facts);
        }
        else if (story is null)
        {
            AppendShortStory(text, ask, facts, revision);
        }
        else
        {
            AppendNewChapter(text, ask, facts, plan, away);
        }

        text.AppendLine();
        text.AppendLine(rewriting
            ? "The new objectives, in order and numbered from 1, each with its dramatic function and what the Commander does, step by step:"
            : "The chapter's objectives, in order, each with its dramatic function and what the Commander does, step by step:");

        foreach (var (objective, index) in plan.Objectives.Select((objective, index) => (objective, index)))
        {
            text.AppendLine($"{(index + 1).ToString(CultureInfo.InvariantCulture)}. {objective.Function}: {string.Join(", then ", objective.Steps.Select(step => StepLine(step, story)))}.{PurposeLine(objective, story)}");

            foreach (var mission in objective.Steps.Select(step => MissionAt(step, facts)).OfType<Mission>().Distinct())
            {
                text.AppendLine(
                    $"   This stop is the destination of a mission the Commander holds: {StoryMissionAsides.Describe(mission)}. Its lines may tie the mission to the story "
                    + "but never change it: no new cargo, target, reward or deadline, and never ask the Commander to complete, fail or abandon it.");
            }
        }

        text.AppendLine(
            "The functions: the setup opens the chapter's want; a complication pushes against it; the "
            + $"{ChapterFunctions.TurnAt(ChapterInputs.StageOf(story?.Stage))} is where the chapter stops being what it looked like; a setback costs the Commander something; "
            + "the resolution's lines are the chapter's ending.");

        if (plan.Objectives.Any(objective => objective.Function == ChapterFunctions.Opening))
        {
            text.AppendLine(
                "The opening objective sets the scene before the setup; the hesitation is where the Commander could still turn back, just before the turn; "
                + "the closing comes after the resolution and leaves the Commander with what the story meant.");
        }

        if (!rewriting)
        {
            text.AppendLine(
                "The spine is built on these objectives: the premise and the want are events that happen to the Commander and the cast, the want and the turn rest on what the "
                + "Commander does at them, and the turn falls on the objective marked for it. Neither the premise nor the want lists the objectives.");
        }

        text.AppendLine();
        text.Append(TriggerConfirms.Section([.. plan.Objectives.SelectMany(objective => objective.Steps).Select(step => step.Kind.ToString().ToLowerInvariant()).Distinct()], story: story is not null));
        AppendLineRules(text, task);

        var due = Due(ask, facts);
        var switches = due.Count > 0
            ? new StorySwitches([.. due.Where(entry => entry.Boards).Select(entry => entry.Id)], [.. due.Where(entry => !entry.Boards).Select(entry => entry.Id)], !rewriting)
            : null;
        IReadOnlyList<string>? speakers = story is null ? null : [.. Speakers(story)];

        if (story is not null)
        {
            AppendSpeakers(text, story, due, opening: !rewriting, facts);
        }

        if (repair is not null)
        {
            AppendLineRepair(text, plan, repair, rewriting);
            text.AppendLine();
            text.AppendLine(
                "Answer with one JSON object and nothing else: " + StoryAnswer.LineRepairsShape(task, repair.Opening, repair.OfSpine, switches, speakers is not null)
                + ". \"objective\" is the number of an objective named above as unable to stand; give each of them, and no other.");

            return new StoryCall(text.ToString(), StoryAnswer.LineRepairsSchema(speakers, task, repair.Opening, repair.OfSpine, switches));
        }

        if (ask.LastDraftRefused is { Length: > 0 } refused)
        {
            text.AppendLine();
            text.AppendLine($"An earlier draft of this {(story is null ? "story" : "chapter")}'s words could not be used, for this reason. Write this draft so that it does not happen again:");
            text.Append("- ").AppendLine(refused);
        }

        if (rewriting)
        {
            text.AppendLine();
            text.AppendLine(
                "Answer with one JSON object and nothing else: " + StoryAnswer.LineRepairsShape(task, opening: false, spine: false, switches, speakers is not null)
                + ". \"objectives\" has one entry for each new objective above, numbered as above.");

            return new StoryCall(text.ToString(), StoryAnswer.LineRepairsSchema(speakers, task, opening: false, spine: false, switches));
        }

        var what = story is null ? "story" : "chapter";

        text.AppendLine();
        text.AppendLine(
            "Answer with one JSON object and nothing else: " + StoryAnswer.LinesShape(task, story?.Cast is { Count: > 0 }, switches, speakers is not null)
            + $". \"name\" is the {what}'s title, a few words; \"premise\" is one paragraph; \"want\" is the outer goal; \"stake\" is the belief the {what} tests "
            + "and what it would cost to be wrong; \"turn\" is where it stops being what it looked like; \"ending\" is what the last objective means; each under 600 characters. "
            + "\"objectives\" has one entry for each objective above, numbered as above. \"reply\" is what you say to the Commander, in your own voice, as you hand "
            + $"them the {what} — one or two sentences, no summary of the plot.");

        return new StoryCall(
            text.ToString(),
            StoryAnswer.LinesSchema(speakers, task, story?.Cast is { Count: > 0 } members ? [.. members.Select(member => member.Id)] : null, switches));
    }

    /// <summary>A new chapter's head: what the call writes, the chapter before, a failed or set-aside draft, the facts, the story and its journey.</summary>
    private static void AppendNewChapter(StringBuilder text, AdventureAsk ask, Facts facts, ChapterPlan plan, BaseStep? away)
    {
        var story = ask.Story!;

        text.AppendLine(
            "You are writing a chapter of a story the Commander will fly in Elite Dangerous, told by you as their companion. "
            + "d47 has already built the chapter's objectives: what the Commander does at each, where, and how many times. "
            + "You write the words: the chapter's title and spine, its opening, and each objective's title, reason and lines. "
            + "You do not choose, move or change a place, a kind of objective or a count; write the words for the objectives as they are.");

        if (ask.Chapter is { } chapter)
        {
            AppendChapter(text, chapter, facts, stock: true, ask.Failed is not null || ask.SetAside is not null);
        }

        if (ask.Failed is { } failed)
        {
            AppendFailed(text, failed);
        }

        if (ask.SetAside is { } setAside)
        {
            AppendSetAside(text, setAside);
        }

        text.AppendLine();
        text.Append(facts.Describe());
        AppendStory(text, story);

        AppendJourney(text, ask, facts, plan);
        AppendAway(text, away, ask, facts);
    }

    /// <summary>The rules every line, reason, title and task of a planned objective is written to.</summary>
    private static void AppendLineRules(StringBuilder text, bool task)
    {
        text.AppendLine("Rules for the lines: show the place and what is in it; never tell the Commander what they feel. Two to four sentences each, spoken in a cockpit. Foreshadow the turn and the ending in the earlier objectives' lines — you know how it ends and the voice that will read these lines to the Commander does not, so anything the Commander is to suspect early must be in the line itself. A line that sets up something for later makes sense when heard alone. The opening is said when the chapter begins and before the first objective; the last objective's lines are the ending. An objective's lines are spoken when its last step is done.");
        text.AppendLine(PlainWords.Rule + " Whoever says a line, it is written this way. Do not repeat the spine's premise, want, stake, turn or ending as a line.");
        text.AppendLine("A line never gives the Commander a task. The game has no way to find, meet, question or watch a person — so a line may say what somebody did, signed or left behind, but never \"ask the clerk\", \"find the pilot\" or \"see what their face does\". What the Commander does next is always the next objective, and the line may point them at it.");
        text.AppendLine(
            "Give every objective a \"reason\": one plain sentence, said before the Commander sets off, saying why they go — what they go to do or get "
            + "there and how it serves the chapter — and where in the story that comes from, such as a record, a note, a cast member, or what an earlier "
            + "objective found. The reason never says what the line will say, and never attributes the objective to someone unnamed (\"I'm told\"). "
            + "The Narrator reads the reason aloud as narration: write it in the third person, about the Commander, never as \"I\", \"me\" or \"we\"; it "
            + "never speaks to the Commander, and never offers or asks.");
        text.AppendLine("Give each objective a short title — a chapter name, never a number.");

        if (task)
        {
            text.AppendLine(
                "An objective with an event step also has a \"task\": a phrase of at most " + AdventureTrigger.MaxTaskLength.ToString(CultureInfo.InvariantCulture)
                + " characters saying what the Commander does, such as \"analyse a Stratum sample\". Where the event is run by a phrase the Commander says, the task holds that phrase. Every other objective's task is null.");
        }
    }

    /// <summary>Where the chapter travels and why, as its words are to tell it: toward the finale's destination, a target, home, in the black or back toward Sol.</summary>
    private static void AppendJourney(StringBuilder text, AdventureAsk ask, Facts facts, ChapterPlan plan)
    {
        var story = ask.Story!;

        if (story.FinaleChapter is { } finale && plan.Destination is { } destination)
        {
            var left = story.FinaleChapters - finale;

            text.AppendLine();
            text.Append($"The story's finale ends on {destination.Body}, in {destination.System}");
            text.Append(facts.DestinationLightYears is { } distance ? $", {distance.ToString("0", CultureInfo.InvariantCulture)} light years from the Commander's position. " : ". ");
            text.AppendLine(left switch
            {
                <= 0 => "This is the last finale chapter, and its last objective lands there.",
                1 => "One finale chapter is left after this one.",
                _ => $"{left.ToString(CultureInfo.InvariantCulture)} finale chapters are left after this one.",
            });

            return;
        }

        var leg = ChapterPull.Of(ask, facts.Position);

        if (leg is { Arrived: false } && facts.Position is { } position)
        {
            var far = position.DistanceTo(leg.There).ToString("0", CultureInfo.InvariantCulture);

            text.AppendLine();
            text.AppendLine(
                leg.Chosen
                    ? $"The Commander asked to head for {leg.Target}, {far} light years away, and has not got there. This chapter travels toward it, and its words say why."
                : leg.Back
                    ? $"This part of the story, in the black, heads back toward where it set out, {far} light years away. Its words say why the chapter travels."
                : leg.Drawn
                    ? $"This part of the story travels out into the black, toward an uncharted point {far} light years away. Its words say why the chapter travels."
                : leg.Home
                    ? $"The Commander is {far} light years from {leg.Target} and has turned down the last chapters out here, so the story heads home to {leg.Target}. Its words say why the chapter travels."
                    : $"The Commander is {far} light years from {leg.Target}, and this part of the story travels toward {leg.Target}, in the black. Its words say why the chapter travels.");

            return;
        }

        if (story.InTheBlack)
        {
            text.AppendLine();

            if (leg is { Arrived: true, Drawn: false })
            {
                text.AppendLine($"The Commander has arrived at {leg.Target}.");
            }

            text.AppendLine("This chapter of the story takes place in the black. Its stops may be far from the bubble.");

            return;
        }

        if (ChapterBand.Of(facts.Position, ask) is not null && facts.Position is { } here)
        {
            text.AppendLine();
            text.AppendLine($"The Commander is {here.DistanceTo(StarPosition.Origin).ToString("0", CultureInfo.InvariantCulture)} light years from Sol, outside the bubble, and the story works its way back toward populated space.");
        }
    }

    /// <summary>The draft's words, objective by objective, why some cannot stand, and which the answer writes again.</summary>
    private static void AppendLineRepair(StringBuilder text, ChapterPlan plan, LineRepair repair, bool rewriting)
    {
        var heads = Heads(repair.Read.Beats);

        text.AppendLine();

        if (rewriting)
        {
            text.AppendLine("Your draft of the new objectives' words:");
        }
        else
        {
            text.AppendLine("Your draft of the chapter's words:");
            AppendSpine(text, repair.Name, repair.Spine);
            text.AppendLine($"Opening: {Quoted(repair.Read.Opening)}");
        }

        foreach (var (head, index) in heads.Select((head, index) => (head, index)))
        {
            var beat = repair.Read.Beats[head];

            text.AppendLine($"{(index + 1).ToString(CultureInfo.InvariantCulture)}. {beat.Title} ({plan.Objectives[index].Function}) — reason: \"{beat.Reason}\"{(beat.Task is { } task ? $" — task: \"{task}\"" : string.Empty)} — lines: {Quoted(beat.Lines)}{Switched(beat.Boards, beat.Leaves)}");
        }

        text.AppendLine();
        text.AppendLine("These cannot stand:");

        foreach (var refusal in repair.Refusals)
        {
            text.Append("- ").AppendLine(refusal.Text);
        }

        AppendEarlier(text, repair.Earlier);

        var numbers = repair.Heads.Select(head => heads.IndexOf(head) + 1).Order().Select(number => number.ToString(CultureInfo.InvariantCulture)).ToList();

        var again = new List<string>();

        if (numbers.Count > 0)
        {
            again.Add($"the title, reason and lines of objective{(numbers.Count == 1 ? string.Empty : "s")} {string.Join(", ", numbers)}");
        }

        if (repair.Opening)
        {
            again.Add("the opening");
        }

        if (repair.OfSpine)
        {
            again.Add("the chapter's name and spine");
        }

        text.AppendLine();
        text.AppendLine(
            $"Write again {string.Join(", and ", again)}, so that what is refused does not happen again. "
            + "Every objective's steps and places stay as they are; only the words change.");
    }
}
