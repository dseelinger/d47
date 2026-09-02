using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Air between the two things d47 says because nothing has happened
/// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>), reported off the settings
/// card as <em>"these appear to overlap"</em>.
/// <para>
/// <b>They do not overlap and were never combined.</b> A remark is d47 itself, in the core's own
/// voice, about where the Commander is. An exchange is two strangers talking to each other in cast
/// voices on a channel the Commander is deliberately not part of. What overlapped was the
/// <em>timing</em>: two <see cref="ICallout"/>s, each holding its own <c>_lastSpokenAt</c>, and a
/// cooldown in <see cref="CalloutEngine"/> keyed per announcement — so <c>ambient.supercruise</c>
/// and <c>npc.chatter.passersby</c> never tested against each other and nothing anywhere was a
/// floor between two unprompted utterances.
/// </para>
/// <para>
/// <b>And underneath that, the two were phase-locked by construction.</b> Both <c>Gap()</c> bodies
/// hashed their own pick counter with the same Knuth constant, the fraction at pick zero is
/// exactly zero, both callouts seed on the same first live tick off the same situation, and since
/// 2026-09-02 both pairs of rows carry the same numbers on purpose. So the first remark and the
/// first exchange of <em>every</em> session were due on the same tick. The floor alone would have
/// turned that into a permanent ninety-second couplet — a cadence — which is why the chatter
/// spread is now offset by one and the floor is the guarantee rather than the schedule.
/// </para>
/// <para>
/// Lore, arrivals, route progress and the milestones are not in this: they speak because something
/// <em>happened</em>, and holding one back would be spacing out the news rather than the flavour.
/// The line is drawn by <see cref="Announcement.Chatter"/> and never by
/// <see cref="CalloutUrgency"/>, which answers a different question.
/// </para>
/// </summary>
public class TwoUnpromptedVoicesKeepTheirDistanceTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The shipped floor at the shipped rows: ninety seconds, both clamps inert at 300.</summary>
    private static readonly TimeSpan Floor =
        CalloutEngine.ChatterSpacingFor(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300));

    private const StatusFlags Docked = StatusFlags.Docked | StatusFlags.InMainShip;
    private const StatusFlags Flying = StatusFlags.Supercruise | StatusFlags.InMainShip;

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext At(
        DateTimeOffset now,
        StatusFlags flags = Docked,
        IEnumerable<string>? events = null) =>
        new(
            now,
            IsPriming: false,
            State: null,
            GameStatus.Unknown with { Flags = flags },
            NavRoute.None,
            [.. (events ?? []).Select(Event)]);

    /// <summary>
    /// The two in the order <c>BuildCallouts</c> registers them — the exchange above the remark,
    /// which is the tie-break when both are due on one tick. Written here so the tests that care
    /// about the tie-break say which order they mean rather than inheriting it from the argument
    /// list; nothing in this project can reach the real registration site.
    /// </summary>
    private static CalloutEngine Shipped(AmbientCallout remarks, NpcChatterCallout exchanges) =>
        Engine(exchanges, remarks);

    private static CalloutEngine Engine(params ICallout[] callouts)
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);

        foreach (var callout in callouts)
        {
            engine.Add(callout);
        }

        return engine;
    }

    // Settle pinned to zero throughout: it is the other timing rule and it has its own tests.
    // What is under test here is what happens when two things are due at once.
    private static AmbientCallout Remarks(int least = 300, int most = 600, int settle = 0) => new()
    {
        Interval = TimeSpan.FromSeconds(least),
        Longest = TimeSpan.FromSeconds(most),
        Settle = TimeSpan.FromSeconds(settle),
    };

    private static NpcChatterCallout Exchanges(int least = 300, int most = 600, int settle = 0) => new()
    {
        Interval = TimeSpan.FromSeconds(least),
        Longest = TimeSpan.FromSeconds(most),
        Settle = TimeSpan.FromSeconds(settle),
    };

    /// <summary>
    /// A second at a time, which is a tenth of the real tick rate and far finer than a floor
    /// measured in ninety of them. Returns what was queued and the second it was queued on.
    /// </summary>
    private static List<(int Second, Announcement Said)> Drive(
        CalloutEngine engine,
        int seconds,
        Func<int, StatusFlags>? situation = null,
        Func<int, IEnumerable<string>?>? events = null)
    {
        var heard = new List<(int, Announcement)>();

        for (var second = 0; second <= seconds; second++)
        {
            engine.Tick(At(T0.AddSeconds(second), situation?.Invoke(second) ?? Docked, events?.Invoke(second)));

            foreach (var said in engine.Drain())
            {
                heard.Add((second, said));
            }
        }

        return heard;
    }

    // Ambient never remarks on the same situation twice running, so a drive that never leaves the
    // dock hears exactly one remark. Alternating is what makes the two voices both live.
    private static StatusFlags Alternating(int second) => second / 200 % 2 == 0 ? Docked : Flying;

    private static List<(int Second, Announcement Said)> Unprompted(
        IEnumerable<(int Second, Announcement Said)> heard) =>
        [.. heard.Where(entry => entry.Said.Chatter is not null)];

    // ---- The acceptance criteria ------------------------------------------------------------

    /// <summary>
    /// <b>Criterion one, over the engine rather than by listening.</b> Two hours of the shipped
    /// callouts at the shipped numbers, and no two unprompted lines land inside the floor.
    /// </summary>
    [Fact]
    public void TwoUnpromptedLinesAreNeverQueuedInsideTheFloor()
    {
        var heard = Unprompted(Drive(Engine(Remarks(), Exchanges()), seconds: 7200, situation: Alternating));

        // Enough of both that the assertion below has something to bite on. A floor that held
        // everything back would pass a "never too close" test trivially.
        Assert.True(heard.Count >= 8, $"only {heard.Count} unprompted lines in two hours");
        Assert.Contains(heard, entry => entry.Said.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal));
        Assert.Contains(heard, entry => entry.Said.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal));

        foreach (var (earlier, later) in heard.Zip(heard.Skip(1)))
        {
            Assert.True(
                later.Second - earlier.Second >= Floor.TotalSeconds,
                $"{earlier.Said.Key} at {earlier.Second}s and {later.Said.Key} at {later.Second}s "
                + $"are {later.Second - earlier.Second}s apart, inside the {Floor.TotalSeconds}s floor");
        }
    }

    /// <summary>
    /// <b>Criterion two.</b> A warning is never held. Driven with the shipped
    /// <see cref="DangerCallout"/> rather than a double, and with the collision forced onto the
    /// same tick the warning arrives on.
    /// </summary>
    [Fact]
    public void AWarningIsNeverHeldByTheFloor()
    {
        // Spreads pinned, so both unprompted callouts are due on the same tick — the worst case
        // for a warning arriving into a busy one.
        var heard = Drive(
            Engine(Remarks(300, 300), Exchanges(300, 300), new DangerCallout()),
            seconds: 302,
            events: second => second switch
            {
                300 => [Heat],
                301 => [ShieldsDown],
                _ => null,
            });

        var heat = Assert.Single(heard, entry => entry.Said.Key == "danger.heat");
        var shields = Assert.Single(heard, entry => entry.Said.Key == "danger.shields");

        // On the tick, both of them: neither waits on the floor, and the second is not held by
        // the first having spent something.
        Assert.Equal(300, heat.Second);
        Assert.Equal(301, shields.Second);

        // And the unprompted pair was genuinely colliding underneath, so this is the busy tick it
        // claims to be rather than a quiet one.
        Assert.Single(Unprompted(heard), entry => entry.Second == 300);
    }

    /// <summary>
    /// <b>Criterion three: what becomes of the one that did not go.</b> It is held, not spent —
    /// it arrives as the floor clears rather than an interval later, and it deals the pairing and
    /// the variant it would have dealt unheld.
    /// </summary>
    [Fact]
    public void TheHeldExchangeIsLateAndNotLost()
    {
        // Deliberately NOT the shipped order: it is the exchange being held that this is about,
        // because the exchange is the one whose pick counter picks a pairing and whose composition
        // costs a round trip. In the app it is held whenever a remark was queued on an earlier
        // tick rather than on this one.
        var heard = Drive(Engine(Remarks(300, 300), Exchanges(300, 300)), seconds: 500);

        var remark = Assert.Single(heard, entry => entry.Said.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal));
        var exchange = Assert.Single(heard, entry => entry.Said.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal));

        Assert.Equal(300, remark.Second);

        // Held at all — the two were due together, and one of them moved.
        Assert.True(exchange.Second > remark.Second, "the exchange was not held");

        // The floor and not a second more. An exchange that had spent its turn would arrive at
        // 600 or not at all, and a whole cycle of silence is a much worse answer than a wait.
        Assert.Equal(300 + (int)Floor.TotalSeconds, exchange.Second);
        Assert.True(exchange.Second < 600, "the exchange lost its cycle rather than waiting");

        // Nothing was spent: the marker carries the first pick, exactly as one that met no floor.
        var alone = Assert.Single(
            Drive(Engine(Exchanges(300, 300)), seconds: 320),
            entry => entry.Said.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal));

        Assert.Equal(0, exchange.Said.Variant);
        Assert.Equal(alone.Said.Variant, exchange.Said.Variant);
        Assert.Equal(alone.Said.Key, exchange.Said.Key);
    }

    /// <summary>
    /// The ambient side of the same rule, and the sharper half of it. A remark refused <em>after</em>
    /// it had yielded would have spent <c>_lastSpoken</c> as well as its clock — so the situation
    /// it was never allowed to mention would be suppressed by the never-twice-running rule, and a
    /// Commander sitting at one dock would hear nothing at all.
    /// </summary>
    [Fact]
    public void AHeldRemarkDoesNotSpendItsSituation()
    {
        // The shipped order, so this is the collision as a Commander actually meets it: the
        // exchange goes and the remark is the one held.
        var heard = Drive(Shipped(Remarks(300, 300), Exchanges(300, 300)), seconds: 500);

        var exchange = Assert.Single(heard, entry => entry.Said.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal));
        var remark = Assert.Single(heard, entry => entry.Said.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal));

        Assert.Equal(300, exchange.Second);
        Assert.True(remark.Second > exchange.Second, "the remark was not held");
        Assert.Equal(300 + (int)Floor.TotalSeconds, remark.Second);

        // And it is still about the dock it was held over, rather than a situation skipped.
        Assert.Equal("ambient.docked", remark.Said.Key);
        Assert.Equal(0, remark.Said.Variant);
    }

    // ---- The phase lock underneath -----------------------------------------------------------

    /// <summary>
    /// <b>The collision was certain, not likely.</b> Pinning both spreads reproduces exactly what
    /// shipped before the offset — identical gaps off identical counters — and shows the engine
    /// absorbing it. This is the half that fails if the floor is removed.
    /// </summary>
    [Fact]
    public void WithBothSpreadsPinnedTheCollisionIsRealAndOnlyOneIsQueued()
    {
        // Both callouts, examined alone, are due on the same second. That is the defect.
        var remarks = Remarks(300, 300);
        var exchanges = Exchanges(300, 300);

        _ = remarks.Examine(At(T0)).ToArray();
        _ = exchanges.Examine(At(T0)).ToArray();

        Assert.Single(remarks.Examine(At(T0.AddSeconds(300))).ToArray());
        Assert.Single(exchanges.Examine(At(T0.AddSeconds(300))).ToArray());

        // Through the engine, the same tick yields one.
        var together = Unprompted(Drive(Engine(Remarks(300, 300), Exchanges(300, 300)), seconds: 300));

        Assert.Single(together);
    }

    /// <summary>
    /// <b>And it is gone at its source.</b> At the shipped numbers the two no longer come due
    /// together at all, so the floor is a guarantee held in reserve rather than a thing that fires
    /// every cycle — which would be a fixed couplet, the cadence both spreads exist to prevent.
    /// </summary>
    [Fact]
    public void TheFirstRemarkAndTheFirstExchangeNoLongerLandTogether()
    {
        var heard = Unprompted(Drive(Engine(Remarks(), Exchanges()), seconds: 600));

        var remark = Assert.Single(heard, entry => entry.Said.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal));
        var exchange = Assert.Single(heard, entry => entry.Said.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal));

        Assert.Equal(300, remark.Second);

        // Comfortably past the floor on its own, so nothing was held: the hashes deal different
        // fractions now, and 0.618 of the 300-second spread puts the first exchange at 485.4 —
        // the first whole second past it on a one-second drive.
        Assert.Equal(486, exchange.Second);
        Assert.True(exchange.Second - remark.Second > Floor.TotalSeconds);
    }

    /// <summary>
    /// The two spreads are offset rather than identical, asserted without the engine so it is a
    /// statement about the callouts themselves and not about what the floor rescued afterwards.
    /// <para>
    /// The first cycle is where it is provable. The Knuth fraction at pick zero is exactly zero,
    /// so a callout whose spread starts at its own counter serves exactly its
    /// <see cref="AmbientCallout.Interval"/> the first time — and two of them, seeded on the same
    /// tick off the same rows, are then due on the same second of every session.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTwoSpreadsDoNotDealTheSameFirstGap()
    {
        var remarks = Remarks();
        var exchanges = Exchanges();

        // Seeded together, which is what the first live tick of every session does to both.
        _ = remarks.Examine(At(T0)).ToArray();
        _ = exchanges.Examine(At(T0)).ToArray();

        // The remark serves its interval exactly: pick zero, fraction zero, no spread at all.
        Assert.Empty(remarks.Examine(At(T0.AddSeconds(299))));
        Assert.Single(remarks.Examine(At(T0.AddSeconds(300))).ToArray());

        // The exchange must not, or every session opens on a collision the floor then has to
        // absorb — which is the couplet, not the fix.
        Assert.Empty(exchanges.Examine(At(T0.AddSeconds(300))));
    }

    // ---- The engine's own guard --------------------------------------------------------------

    private sealed class Scripted(string id) : ICallout
    {
        public string Id => id;

        public List<Announcement> Next { get; } = [];

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            var said = Next.ToArray();
            Next.Clear();

            return said;
        }
    }

    private static Announcement Chatter(string key, int least = 300) =>
        new(key, "something") { Cooldown = TimeSpan.FromSeconds(least), Chatter = TimeSpan.FromSeconds(least) };

    /// <summary>
    /// <b>The guarantee is the engine's, not the callouts' manners.</b> A callout that carries the
    /// flag and never looks at <see cref="CalloutContext.LastChatterAt"/> is held anyway — which
    /// also covers the case no callout-side check can see, one <c>Examine</c> yielding two.
    /// </summary>
    [Fact]
    public void TheEngineHoldsItEvenWhenACalloutForgetsToAsk()
    {
        var rude = new Scripted("rude");
        var engine = Engine(rude);

        rude.Next.AddRange([Chatter("npc.chatter.passersby"), Chatter("ambient.docked")]);
        engine.Tick(At(T0));

        Assert.Single(engine.Drain());
    }

    /// <summary>
    /// A held line spends nothing on its way out. Its key cooldown is three hundred seconds and
    /// the floor is ninety, so a guard placed below <c>_spokenAt</c> would turn a wait into a lost
    /// cycle — the same argument #136 already makes one line further down.
    /// </summary>
    [Fact]
    public void AHeldLineDoesNotBurnItsCooldown()
    {
        var voice = new Scripted("voice");
        var engine = Engine(voice);

        voice.Next.Add(Chatter("npc.chatter.passersby"));
        engine.Tick(At(T0));
        Assert.Single(engine.Drain());

        // Inside the floor: refused, and it must cost this key nothing.
        voice.Next.Add(Chatter("ambient.docked"));
        engine.Tick(At(T0.AddSeconds(30)));
        Assert.Empty(engine.Drain());

        // One second past it, the same key goes. If the refusal had stamped it, its own
        // three-hundred-second cooldown would be running and this would be silent.
        voice.Next.Add(Chatter("ambient.docked"));
        engine.Tick(At(T0.AddSeconds(91)));
        Assert.Single(engine.Drain());
    }

    /// <summary>
    /// And it does not spend the alarm spacing either — the mirror of the suppressed-repeat rule
    /// in <see cref="EveryUrgentWarningIsMarkedTests"/>. A refused line that had marked the cue
    /// would silence the alarm of a warning ten seconds behind it.
    /// </summary>
    [Fact]
    public void AHeldLineDoesNotSpendTheAlarmSpacing()
    {
        var voice = new Scripted("voice");
        var engine = Engine(voice);

        voice.Next.Add(Chatter("npc.chatter.passersby"));
        engine.Tick(At(T0));
        Assert.Single(engine.Drain());

        voice.Next.Add(Chatter("ambient.docked") with { Cue = AlertCue.UnderFire });
        engine.Tick(At(T0.AddSeconds(1)));
        Assert.Empty(engine.Drain());

        voice.Next.Add(new Announcement("danger.shields", "Shields are down.", CalloutUrgency.Urgent)
        {
            Cue = AlertCue.UnderFire,
        });

        engine.Tick(At(T0.AddSeconds(2)));

        var warning = Assert.Single(engine.Drain());

        Assert.Equal(AlertCue.UnderFire, warning.Cue);
    }

    /// <summary>
    /// <b>The news is not chatter.</b> Route progress, an arrival and a milestone are all
    /// <see cref="CalloutUrgency.Routine"/> and none of them carries
    /// <see cref="Announcement.Chatter"/> — so an engine that keyed the floor on urgency instead
    /// would quietly space these out, which is the defect this flag exists to avoid. Driven with
    /// the real keys, so collapsing the flag into the urgency fails here.
    /// </summary>
    [Fact]
    public void RoutineNewsIsNotChatterAndIsNeverHeld()
    {
        var voice = new Scripted("voice");
        var engine = Engine(voice);

        voice.Next.Add(Chatter("npc.chatter.passersby"));
        engine.Tick(At(T0));
        Assert.Single(engine.Drain());

        voice.Next.AddRange(
        [
            new Announcement("route.progress", "Three jumps to go."),
            new Announcement("arrival.home", "We are home."),
            new Announcement("materials.raw", "Iron is full."),
        ]);

        engine.Tick(At(T0.AddSeconds(1)));

        Assert.Equal(3, engine.Drain().Count);
    }

    /// <summary>
    /// And the traffic is one-way: a warning never consumes the chatter's air. The floor spaces
    /// these two things from each other and from nothing else.
    /// </summary>
    [Fact]
    public void AWarningDoesNotConsumeTheChattersAir()
    {
        var voice = new Scripted("voice");
        var engine = Engine(voice);

        voice.Next.Add(new Announcement("danger.heat", "Taking heat damage.", CalloutUrgency.Urgent));
        engine.Tick(At(T0));
        Assert.Single(engine.Drain());

        voice.Next.Add(Chatter("ambient.docked"));
        engine.Tick(At(T0.AddSeconds(1)));

        Assert.Single(engine.Drain());
    }

    /// <summary>
    /// <b>The floor never outlasts the row it is holding.</b> A Commander who asks for a line
    /// every twenty seconds is asking for twenty seconds of quiet and gets exactly that; the
    /// ninety is a ceiling on the wait, not the wait.
    /// </summary>
    [Fact]
    public void TheFloorIsNeverLongerThanEitherRowItSitsBetween()
    {
        // The number itself, pinned: it is a decision about the ear and a change to it has to be
        // argued rather than typed. Everything below derives from it.
        Assert.Equal(TimeSpan.FromSeconds(90), CalloutEngine.ChatterSpacing);

        var five = TimeSpan.FromSeconds(300);
        var twenty = TimeSpan.FromSeconds(20);

        // Neither row under it: the floor stands.
        Assert.Equal(CalloutEngine.ChatterSpacing, CalloutEngine.ChatterSpacingFor(five, five));

        // Either row under it: that row wins, whichever end of the pair it is. The waiter's rate
        // bounds it so nobody waits longer than they asked; the speaker's bounds it so no voice
        // can demand more air behind it than it leaves in front of itself.
        Assert.Equal(twenty, CalloutEngine.ChatterSpacingFor(twenty, five));
        Assert.Equal(twenty, CalloutEngine.ChatterSpacingFor(five, twenty));

        var talkative = new Scripted("talkative");
        var quick = Engine(talkative);

        talkative.Next.Add(Chatter("npc.chatter.passersby"));
        quick.Tick(At(T0));
        Assert.Single(quick.Drain());

        talkative.Next.Add(Chatter("ambient.docked", least: 20));
        quick.Tick(At(T0.AddSeconds(21)));
        Assert.Single(quick.Drain());

        // The same moment, asked for by a row that wants five minutes, is still inside its floor.
        var patient = new Scripted("patient");
        var slow = Engine(patient);

        patient.Next.Add(Chatter("npc.chatter.passersby"));
        slow.Tick(At(T0));
        Assert.Single(slow.Drain());

        patient.Next.Add(Chatter("ambient.docked"));
        slow.Tick(At(T0.AddSeconds(21)));
        Assert.Empty(slow.Drain());

        // And the same again from the other end: a line that came from a twenty-second row cannot
        // hold a five-minute one for ninety, because it is going to speak again itself long before
        // the ninety is up. This is the bound that stops a fast kind starving a slow one.
        var talkedOver = new Scripted("talked-over");
        var brief = Engine(talkedOver);

        talkedOver.Next.Add(Chatter("npc.chatter.passersby", least: 20));
        brief.Tick(At(T0));
        Assert.Single(brief.Drain());

        talkedOver.Next.Add(Chatter("ambient.docked"));
        brief.Tick(At(T0.AddSeconds(21)));
        Assert.Single(brief.Drain());
    }

    /// <summary>
    /// <b>A fast voice cannot starve a slow one.</b> The floor is bounded by the rate of whoever
    /// just spoke as well as by the rate of whoever is waiting — because a kind set to speak every
    /// minute restamps the floor faster than a ninety-second wait can ever expire, and the other
    /// kind would then be refused forever rather than spaced out.
    /// <para>
    /// Driven at the shipped ninety-second settle with the situation turning over faster than two
    /// floors, which is what makes the one-sided version fail outright rather than merely thin the
    /// traffic out.
    /// </para>
    /// </summary>
    [Fact]
    public void ATalkativeVoiceDoesNotStarveTheOther()
    {
        // In Ship turned right down — a value its own help invites, "lower is a talkative
        // companion" — with NPC chatter left at the shipped numbers.
        static StatusFlags Turning(int second) => second / 120 % 2 == 0 ? Docked : Flying;

        // The shipped registration order, which is what makes this survivable: the exchange has
        // the narrower window, so it is the one that must not lose the tie. With the two swapped
        // this drive produces 29 exchanges alone and none at all together.
        var together = Drive(
            Shipped(Remarks(60, 60, settle: 90), Exchanges(300, 600, settle: 90)),
            seconds: 14400,
            situation: Turning);

        var exchanges = Unprompted(together)
            .Count(entry => entry.Said.Key.StartsWith(NpcChatter.KeyPrefix, StringComparison.Ordinal));

        var remarks = Unprompted(together)
            .Count(entry => entry.Said.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal));

        // Both are heard. The Commander has NPC chatter switched on, and switching In Ship chatter
        // on must not be what silences it.
        Assert.True(remarks > 0, "the remarks were starved");
        Assert.True(exchanges > 0, $"the exchanges were starved: {remarks} remarks, {exchanges} exchanges");

        // And not merely one token exchange in four hours. Against what the same callout produces
        // with nothing else running, it keeps most of its traffic.
        var alone = Drive(Engine(Exchanges(300, 600, settle: 90)), seconds: 14400, situation: Turning).Count;

        Assert.True(
            exchanges * 2 >= alone,
            $"{exchanges} exchanges alongside the remarks against {alone} alone");
    }

    /// <summary>
    /// And the same from the other side, which is the half the tie-break cannot rescue. A kind
    /// speaking every twenty to forty seconds stamps the floor faster than a ninety-second wait
    /// could ever expire, so without the speaker's own rate bounding the floor the slow kind is
    /// refused forever rather than spaced out — whichever of them wins a tie.
    /// </summary>
    [Fact]
    public void ATalkativeExchangeDoesNotStarveTheRemark()
    {
        static StatusFlags Turning(int second) => second / 300 % 2 == 0 ? Docked : Flying;

        var together = Drive(
            Shipped(Remarks(300, 600, settle: 90), Exchanges(20, 40, settle: 90)),
            seconds: 14400,
            situation: Turning);

        var remarks = Unprompted(together)
            .Count(entry => entry.Said.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal));

        var alone = Drive(Engine(Remarks(300, 600, settle: 90)), seconds: 14400, situation: Turning).Count;

        Assert.True(remarks > 0, "the remarks were starved");
        Assert.True(remarks * 2 >= alone, $"{remarks} remarks alongside the exchanges against {alone} alone");
    }

    /// <summary>
    /// A Commander running one of the two cannot tell the floor exists, which is what makes it a
    /// rule between the features rather than a change to either.
    /// </summary>
    [Fact]
    public void OneVoiceAloneIsNeverHeldAtAll()
    {
        var throughTheEngine = Drive(Engine(Exchanges()), seconds: 7200, situation: Alternating)
            .Select(entry => entry.Second)
            .ToList();

        var alone = Exchanges();
        var unhindered = new List<int>();

        for (var second = 0; second <= 7200; second++)
        {
            if (alone.Examine(At(T0.AddSeconds(second), Alternating(second))).Any())
            {
                unhindered.Add(second);
            }
        }

        Assert.NotEmpty(throughTheEngine);
        Assert.Equal(unhindered, throughTheEngine);
    }

    /// <summary>
    /// The tie-break, at the order the app ships. The rarer voice goes: an exchange comes round
    /// every five to ten minutes and a remark will be along shortly, so the remark loses least by
    /// waiting — and the exchange, which has the narrower window, is the one that would starve if
    /// it always lost. See <see cref="ATalkativeVoiceDoesNotStarveTheOther"/>, which is the
    /// measurement that settled it.
    /// </summary>
    [Fact]
    public void TheExchangeGoesAndTheRemarkWaits()
    {
        var together = Unprompted(Drive(Shipped(Remarks(300, 300), Exchanges(300, 300)), seconds: 300));

        var first = Assert.Single(together);

        Assert.StartsWith(NpcChatter.KeyPrefix, first.Said.Key, StringComparison.Ordinal);
    }

    // ---- What the Commander is told ----------------------------------------------------------

    /// <summary>
    /// Both "least time" rows say the two kinds are kept apart, because that is the row the floor
    /// clamps against. The "most time" rows are left alone: they do not control the number, and a
    /// row describing something it does not set is the display saying more than the data does.
    /// </summary>
    [Theory]
    [InlineData(CalloutCapability.AmbientSecondsKey)]
    [InlineData(CalloutCapability.NpcChatterSecondsKey)]
    public void BothLeastTimeRowsSayTheTwoAreKeptApart(string key)
    {
        var help = Row(key).Help;

        Assert.Contains("kept apart", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("back to back", help, StringComparison.OrdinalIgnoreCase);

        // The rename holds (#256's tail): the clause was appended, never a rewrite.
        Assert.DoesNotContain("ambient", help, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invented exchange", help, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CalloutCapability.AmbientMaxSecondsKey)]
    [InlineData(CalloutCapability.NpcChatterMaxSecondsKey)]
    public void TheMostTimeRowsSayNothingAboutIt(string key) =>
        Assert.DoesNotContain("kept apart", Row(key).Help, StringComparison.OrdinalIgnoreCase);

    private static SettingRow Row(string key)
    {
        using var install = new TempInstall();

        return TestSurface.For(install).Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == key);
    }

    private const string Heat =
        """{"timestamp":"3311-01-01T12:00:00Z","event":"HeatDamage"}""";

    private const string ShieldsDown =
        """{"timestamp":"3311-01-01T12:00:00Z","event":"ShieldState","ShieldsUp":false}""";
}
