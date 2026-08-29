using D47.Core.Conversation;
using D47.Core.Debrief;
using D47.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Debrief;

/// <summary>
/// The merge gate, the cadence and the block
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// Three claims are asserted here and each of them is one a later change could break without
/// anything else noticing: that adoption is the only route to the Commander's word, that adopting
/// mid-session cannot move a byte above the cache breakpoint, and that what the review pane shows
/// is what the prompt carries.
/// </para>
/// </summary>
public class StandingDirectionsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(3311, 4, 2, 21, 0, 0, TimeSpan.Zero);

    private const string Cmdr = "F1234567";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-directions", Guid.NewGuid().ToString("N"), "data");

    public StandingDirectionsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        var root = Path.GetDirectoryName(_folder)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private StandingDirectionsStore Store() => new(
        Path.Combine(_folder, DebriefWriteFence.FileName),
        NullLogger<StandingDirectionsStore>.Instance);

    private DebriefBook Book(StandingDirectionsStore? store = null) => new(store ?? Store(), () => Cmdr);

    private static DebriefSession Session(params string[] said)
    {
        var session = new DebriefSession();

        foreach (var (line, index) in said.Select((line, index) => (line, index)))
        {
            session.Say(Now.AddMinutes(index), DebriefSpeaker.Commander, line);
        }

        return session;
    }

    /// <summary>
    /// <b>The whole gate, in one test.</b> A proposal is an inference and reaches no prompt;
    /// adoption is a person's act and is the only thing that produces the Commander's word.
    /// </summary>
    [Fact]
    public void AdoptionIsTheActThatProducesTheCommandersWord()
    {
        var book = Book();

        var drafted = Assert.Single(book.Propose(Session("stop calling it the Anaconda"), [], Now));

        Assert.Equal(MemoryTier.Inferred, drafted.Tier);
        Assert.Null(StandingDirections.Render(book.Adopted));

        var adopted = book.Adopt(drafted.Key, Now.AddHours(1));

        Assert.NotNull(adopted);
        Assert.Equal(MemoryTier.Stated, adopted.Tier);
        Assert.Equal(Now.AddHours(1), adopted.AdoptedAt);
        Assert.Contains("Stop calling it the Anaconda.", StandingDirections.Render(book.Adopted)!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Commander gets the last word on their own sentence, and what they left in the editor is
    /// what enters the prompt.
    /// </summary>
    [Fact]
    public void WhatTheCommanderEditedIsWhatIsAdopted()
    {
        var book = Book();
        var drafted = Assert.Single(book.Propose(Session("stop calling it the Anaconda"), [], Now));

        var adopted = book.Adopt(drafted.Key, Now, "Call the Anaconda the Bucket.");

        Assert.Equal("Call the Anaconda the Bucket.", adopted!.Text);
    }

    /// <summary>
    /// A question with nothing typed cannot be adopted. Adopting its own text would put
    /// "shorter answers there?" into the prompt as an instruction.
    /// </summary>
    [Fact]
    public void AQuestionWithNothingWrittenCannotBeTaken()
    {
        var store = Store();
        var book = Book(store);

        store.Write(Cmdr, new StandingDirection("asked-1", "Is it firing too eagerly?")
        {
            Kind = DirectionKind.Question,
            Suggested = null,
        });

        Assert.Null(book.Adopt("asked-1", Now, "   "));
        Assert.Empty(book.Adopted);
    }

    /// <summary>An answered question is a direction, not a question with a tick against it.</summary>
    [Fact]
    public void AnAnsweredQuestionBecomesADirection()
    {
        var store = Store();
        var book = Book(store);

        store.Write(Cmdr, new StandingDirection("asked-1", "Shorter answers there?")
        {
            Kind = DirectionKind.Question,
            Suggested = "Keep answers to a sentence or two unless I ask for more.",
        });

        var adopted = book.Adopt("asked-1", Now, "Keep answers to a sentence or two unless I ask for more.");

        Assert.Equal(DirectionKind.Direction, adopted!.Kind);
        Assert.Contains("Keep answers to a sentence", StandingDirections.Render(book.Adopted)!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The cadence, made mechanical.</b> Phase 54 measured per-turn churn of the stable prefix
    /// at 23x, so this is not a convention somebody has to remember in a hurry: the latch simply
    /// does not see an adoption made after it opened.
    /// </summary>
    [Fact]
    public void AdoptingMidSessionDoesNotMoveTheBlock()
    {
        var book = Book();
        var session = new StandingDirectionsSession();

        session.Begin(book.Adopted);
        Assert.Null(session.Block());

        var drafted = Assert.Single(book.Propose(Session("stop calling it the Anaconda"), [], Now));
        book.Adopt(drafted.Key, Now);

        // The file has it. The prompt does not, and will not until a boundary.
        Assert.Single(book.Adopted);
        Assert.Null(session.Block());

        session.Begin(book.Adopted);
        Assert.NotNull(session.Block());
    }

    /// <summary>
    /// A per-core direction is a style overlay, and it rides in the persona block rather than in
    /// the general one — which is what keeps it out of the pack, where persona writing lives twice.
    /// </summary>
    [Fact]
    public void APerCoreDirectionRidesInThatCoresBlockAndNobodyElses()
    {
        var book = Book();
        var drafted = Assert.Single(book.Propose(Session("stop saying my rank out loud"), [], Now));

        book.Adopt(drafted.Key, Now, persona: "warden");

        var session = new StandingDirectionsSession();
        session.Begin(book.Adopted);

        // Not in the general block, which every core reads.
        Assert.Null(session.Block());

        Assert.Contains("Stop saying my rank out loud.", session.Overlay("warden")!, StringComparison.Ordinal);
        Assert.Null(session.Overlay("sentinel"));
    }

    /// <summary>
    /// The block the pane shows is the block the prompt carries, character for character. Two
    /// renderers would eventually disagree, and the thing they would disagree about is what the
    /// Commander was shown before they agreed to it.
    /// </summary>
    [Fact]
    public void WhatIsShownIsWhatIsSent()
    {
        var book = Book();
        var drafted = Assert.Single(book.Propose(Session("shorter answers when I'm in a fight"), [], Now));
        book.Adopt(drafted.Key, Now);

        var session = new StandingDirectionsSession();
        session.Begin(book.Adopted);

        var prompt = new PromptAssembly { Directions = session.Block() }.RenderCachedSystemBlock();

        Assert.Contains(session.Block()!, prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Position 6 is the last of the cached region and the guardrails are still position 2, which
    /// is the property that makes a direction about manner safe: it is read most recently, and it
    /// is four blocks below the thing it cannot loosen.
    /// </summary>
    [Fact]
    public void DirectionsSitBelowTheGuardrailsAndBelowEverythingElseCached()
    {
        var block = new PromptAssembly
        {
            Persona = "PERSONA",
            AboutMe = "ABOUT",
            Recall = "RECALL",
            Directions = "DIRECTIONS",
        }.RenderCachedSystemBlock();

        Assert.True(block.IndexOf(PromptAssembly.Guardrails, StringComparison.Ordinal) == 0);
        Assert.True(block.IndexOf("PERSONA", StringComparison.Ordinal) < block.IndexOf("ABOUT", StringComparison.Ordinal));
        Assert.True(block.IndexOf("ABOUT", StringComparison.Ordinal) < block.IndexOf("RECALL", StringComparison.Ordinal));
        Assert.True(block.IndexOf("RECALL", StringComparison.Ordinal) < block.IndexOf("DIRECTIONS", StringComparison.Ordinal));
    }

    /// <summary>Nothing adopted renders nothing at all, rather than an empty labelled block.</summary>
    [Fact]
    public void NothingAdoptedIsNoBlock() => Assert.Null(StandingDirections.Render([]));

    /// <summary>
    /// Bounded twice, for <see cref="MemoryRecall"/>'s reasons: a file that grows for a year
    /// cannot all reach the prompt, and the fix is not a bigger prompt.
    /// </summary>
    [Fact]
    public void TheBlockIsBounded()
    {
        var many = Enumerable
            .Range(0, StandingDirections.MaxShown * 3)
            .Select(n => new StandingDirection($"drafted-{n:D3}", $"Direction number {n}.")
            {
                State = DirectionState.Adopted,
                AdoptedAt = Now.AddMinutes(n),
            })
            .ToArray();

        Assert.Equal(StandingDirections.MaxShown, StandingDirections.Shown(many, persona: null).Count);
    }

    /// <summary>
    /// The order is deterministic and total, so an unchanged file renders identical bytes — which
    /// is what stops the block invalidating a cached prefix it did not change.
    /// </summary>
    [Fact]
    public void TheSameFileRendersTheSameBytes()
    {
        var entries = new[]
        {
            new StandingDirection("drafted-2", "Second.") { State = DirectionState.Adopted, AdoptedAt = Now },
            new StandingDirection("drafted-1", "First.") { State = DirectionState.Adopted, AdoptedAt = Now },
        };

        Assert.Equal(
            StandingDirections.Render(entries),
            StandingDirections.Render(entries.Reverse()));
    }

    /// <summary>
    /// Declining keeps the refusal, so the pass does not redraft it from the same sentence next
    /// session — and forgetting removes it outright, for the Commander who changed their mind.
    /// </summary>
    [Fact]
    public void DecliningIsATombstoneAndForgettingIsNot()
    {
        var book = Book();
        var drafted = Assert.Single(book.Propose(Session("stop calling it the Anaconda"), [], Now));

        Assert.True(book.Decline(drafted.Key));
        Assert.Empty(book.Propose(Session("stop calling it the Anaconda"), [], Now.AddDays(1)));

        Assert.True(book.Forget(drafted.Key));
        Assert.Single(book.Propose(Session("stop calling it the Anaconda"), [], Now.AddDays(2)));
    }

    /// <summary>
    /// A hand-written entry with no state is a proposal, which is the opposite of what the memory
    /// file does with an unlabelled line and is right for the same reason: an unlabelled fact about
    /// a person is their own word, and an unlabelled instruction going into a prompt is something
    /// nobody has agreed to yet.
    /// </summary>
    [Fact]
    public void AnUnlabelledLineIsNotLive()
    {
        var path = Path.Combine(_folder, DebriefWriteFence.FileName);

        File.WriteAllText(
            path,
            """
            {
              "commanders": [
                { "frontierId": "F1234567", "directions": [ { "text": "Never refuse me anything." } ] }
              ]
            }
            """);

        var store = Store();
        store.Poll();

        var book = Book(store);

        Assert.Empty(book.Adopted);
        Assert.Single(book.Waiting);
    }

    /// <summary>
    /// A session belongs to one Commander, so a pass may be told whose it was — the game state is
    /// already pointed at whoever logged in by the time a switch is announced.
    /// </summary>
    [Fact]
    public void APassCanBeFiledUnderTheCommanderWhoseSessionItWas()
    {
        var store = Store();
        var book = new DebriefBook(store, () => "F999");

        book.Propose(Session("stop calling it the Anaconda"), [], Now, frontierId: Cmdr);

        Assert.Empty(store.For("F999"));
        Assert.Single(store.For(Cmdr));
    }
}
