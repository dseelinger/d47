---
title: Campaign
group: Knowledge
nav_order: 166
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Keep a plan for one minor faction, a system at a time.</p>
<section>
<h2><span class="num">1</span> Name the faction, if it is not your squadron's.</h2>
<p class="say">Say "our campaign faction is Hutton Truckers".</p>
<p class="expect">D47 checks the name with galaxy search and says "The campaign faction is Hutton Truckers."</p>
</section>
<section>
<h2><span class="num">2</span> Say what you mean to do in a system.</h2>
<p class="say">Say "hold Shinrarta Dezhra", "grow Lave" or "leave Lave alone", or step the intent on Commander › Campaign.</p>
<p class="expect">D47 confirms the system and the intent, such as "Lave: grow."</p>
</section>
<section>
<h2><span class="num">3</span> Ask for the plan.</h2>
<p class="say">Say "what is our campaign".</p>
<p class="expect">D47 names the faction and how many of its systems are set to each intent.</p>
</section>
<section>
<h2><span class="num">4</span> Ask what needs doing.</h2>
<p class="say">Say "what are our fronts".</p>
<p class="expect">D47 names the three most pressing fronts, each with its system and why, and counts the rest.</p>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Give an intent for a system D47 has not placed the faction in.</p>
<p class="expect">Nothing is set. Ask "what is our campaign" first, which fetches every system the faction is in.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A campaign is one faction and what you mean to do in each of its systems.</p>
<section>
<h2><span class="num">1</span> The faction is your squadron's unless you name another.</h2>
<p class="body">D47 already knows your squadron's faction. Naming another replaces it for the campaign, and the intents set for the old faction's systems are cleared.</p>
</section>
<section>
<h2><span class="num">2</span> Each system has one intent from a fixed list.</h2>
<p class="body">Hold, grow, expand from here, keep under seventy-five percent, take control, or leave alone. A system with no intent is hold where the faction controls it and leave alone where it does not.</p>
</section>
<section>
<h2><span class="num">3</span> The plan is kept per Commander.</h2>
<p class="body">It is saved in <code>data/campaigns.json</code> and is still there after a restart. The model can read it and cannot change it.</p>
</section>
<section>
<h2><span class="num">4</span> D47 works out the fronts, not the model.</h2>
<p class="body">After each BGS tick D47 fetches the faction's systems, uses your journal readings in place of its figures where they are newer, and lists what needs attention, most pressing first. The model only reads the list.</p>
</section>
</div></div>
</details>

## The details

A campaign belongs to one Commander and holds the campaign faction, an intent for each system the
faction is in, and the chronicle of resolved fronts. It is kept in `data/campaigns.json`, keyed by
the Commander's Frontier id.

### The faction

The campaign faction is your squadron's faction until you name another:

> "our campaign faction is Hutton Truckers"

D47 checks the name with galaxy search, which sends it to the galaxy search service, and stores the
spelling the service gives. Naming a different faction clears the intents set for the previous one.

### Intents

Each intent is spoken with the system's name:

| Say | Intent |
|---|---|
| "hold Lave" | Hold |
| "grow Lave" | Grow |
| "expand from Lave" | Expand from here |
| "keep Lave under seventy-five" | Keep under 75% |
| "take control of Lave" | Take control |
| "leave Lave alone" | Leave alone |

None of these needs the language model. They work for a system you have seen the campaign faction in
during the last week, a system that already has an intent, and every system the galaxy service listed for the
faction when you last named it or asked for the campaign this session. A system with no intent is
Hold where the faction controls it and Leave alone elsewhere:

```csharp
public SystemIntent IntentFor(string system, bool controls) =>
    Intents.TryGetValue(system, out var set) ? set : DefaultIntent(controls);

public static SystemIntent DefaultIntent(bool controls) => controls ? SystemIntent.Hold : SystemIntent.LeaveAlone;
```

### Reading it back

> "what is our campaign"

The answer names the faction and counts the systems by intent. The model reads the full list, one
line per system, with "(default)" after an intent nobody set, and the chronicle's latest three
fronts. Asking fetches the faction's systems from galaxy search; when that is not available, only
the systems with an intent set are listed.

### The Campaign page

The page is **Commander › Campaign**, on the desktop window and on the headset's big panel. At the
top are the campaign faction, how many systems it is in and how many it controls, when the fronts were
computed, and a **CHANGE FACTION** tile.

**CHANGE FACTION** opens **Campaign faction**, a list with your squadron's faction first, then every
faction your journal has seen and where. You can also type a name. **Use this** sets the faction exactly
as "our campaign faction is" does. With no campaign faction known, the tile reads **CHOOSE FACTION**.

**Systems** lists every system in the faction's last galaxy report, most influence first, with your
newer journal readings in place of the report's figures. Each row shows:

- the system, and whether the faction controls it;
- the faction's influence;
- its lead over the nearest rival in points, marked "frozen" during a war, civil war or election;
- its state;
- its intent.

System, Influence and Lead sort when you press the heading. A report more than 30 days old shows its
age and OLD REPORT, and the rest of the row is grey. On the big panel the table shows 25 systems to
a page.

The intent is a stepper over the six intents, in this order: Hold, Grow, Expand from here, Stay
under 75%, Take control, Leave alone. A system you have not set shows its default in grey, such as
"Hold · default". A step sets the intent exactly as the phrase does, and every row carries the
protected bar because the model cannot change it. The rows come from code:

```csharp
public static IReadOnlyList<CampaignSystemRow> Of(
    FactionReport report,
    SystemStandings standings,
    IReadOnlyDictionary<string, SystemIntent> intents,
    DateTimeOffset now)
```

Above the sections the page shows the front you are working. With one chosen, **Chosen front** gives
its kind, its system with a copy glyph, how many BGS ticks it has spanned, the sentence that says why
it is a front, and **NEXT**: the one thing to do, with at most one grey line saying what does not
count there. In a conflict you have visited it also gives the days won, in cyan with when you saw
them. **NEXT FRONT** and **DROP THE FRONT** sit under it, with the phrases that do the same.

With no front chosen, **Proposed front** shows the one D47 proposes in the same place, with **TAKE IT**
and **NEXT FRONT**. With neither, the block is left out. Each tile calls the protected tool its
phrase calls, as you, so "take it", "next front" and "drop the front" and the tiles do the same thing.
**NEXT FRONT** with a front chosen shows the tool's reply, "There is no front to propose: one is
already chosen, or the faction has none."

When the last fetch failed, a warning at the top says when the fronts were computed, and **Fronts
computed** is in the warning colour.

Under that, **Fronts**, **Systems** and **Chronicle** are one row of three, with a count on each on
the desktop and none in the headset.

**Fronts** lists the fronts in rank order, each with its kind, system, the reason, and how old its
data is, "you saw it" for your own reading. A report more than 30 days old is marked OLD REPORT. Each
row ends in **TAKE**, which chooses that front as "take the coup threat in Lave" does, or **CHOSEN**
on the front you are working. The rows come from code:

```csharp
public static IReadOnlyList<FrontRow> FrontRows(IReadOnlyList<Front> fronts, ChosenFront? chosen) =>
[
    .. fronts.Select((front, index) => new FrontRow(
        index + 1,
        front,
        chosen is not null && chosen.Kind == front.Kind && string.Equals(chosen.System, front.System, StringComparison.OrdinalIgnoreCase))),
];
```

**Chronicle** lists the resolved fronts, newest first: when it ended, the kind, the system and
opponent, the outcome (Won, Lost, Ended or Resolved), the ticks it spanned, and your own work while
it ran, as influence marks and credits. A front ends on the last tick D47 saw it span, shown as a dash
when it spanned none. The page claims no share of the outcome.

### The chronicle

Each resolved front is kept with its kind, the system, the opponent if there was one, the outcome,
the ticks it spanned and your own BGS work on it. It records what happened and claims no share of
the outcome.

### Fronts

> "what are our fronts"

A front is a system where the campaign faction has something to win, defend or check. D47 computes
them in code from the faction's galaxy report and your own journal readings, so the list is the same
every time it is asked for the same data. The model reads it and never ranks it.

| Front | When |
|---|---|
| Conflict | A war, civil war or election, pending or active. Days won and stakes come from your journal when you have visited. |
| Coup threat | Another faction at 50% or more in a system the faction controls; at 60% it can take control. |
| Retreat | Retreat pending or active. |
| Thin margin | In a controlled system, the lead over the nearest rival is under the intent's margin. Skipped during a conflict, when influence is frozen. |
| Bad state | Lockdown, civil unrest, famine, blight, drought, outbreak, infrastructure failure, pirate attack, terrorist attack or bust. |
| Unwanted expansion | Influence of 70% or more where the intent is keep under 75%. |
| Expansion under way | Expansion pending or active. |
| Stale report | The newest report of a system that is not left alone is more than 30 days old. |

They are ranked in that order. Within a kind, a conflict with a stake comes first, then a system the
faction controls, then the more populous system, then the smaller margin. The thresholds are in one
table, each with its source:

```csharp
/// <summary>Influence at which a faction expands (p.65).</summary>
public const double Expansion = 0.75;

/// <summary>Influence at which a faction that does not control a system can take it by a coup (p.60).</summary>
public const double Coup = 0.60;
```

The margins a thin margin is measured against are D47's own: 10 points for hold, grow and keep under
75%, 15 for expand from here, and none for take control or leave alone.

D47 fetches the report when it first knows the campaign faction, again after each BGS tick, and
again when you name another faction. The fetch runs in the background and sends the faction's name
to the galaxy search service, as the Faction search on the Search tab does. When it fails, the fronts from the last
report are kept, the answer says when they were computed, and D47 tries again after half an hour.
Asking for the fronts never fetches.

### One front at a time

Naming a front to work keeps you on it. While none is chosen, D47 proposes the most pressing front
after each fetch and at the start of a session. A proposal writes nothing.

| Say | Effect |
|---|---|
| "take it" | Works the front D47 proposes. Heard only while one is proposed. |
| "next front" | Proposes the next front in the ranking, wrapping to the top. |
| "take the war in Lave" | Works that front. The noun is the front's kind: "war", "civil war", "election", "coup threat", "retreat", "bad state" and so on. |
| "take the front in Lave" | Works the front there, whatever its kind. |
| "drop the front" | Stops working it. Nothing is added to the chronicle. |
| "what should I do for the faction" | One sentence naming the work, then at most one sentence naming work that does not count. |

A chosen front stays chosen across restarts until it resolves or you drop it. Naming another campaign
faction drops it. D47 notes each BGS tick the front spans. A front resolves when the fronts computed
after you chose it no longer hold it: the conflict ended, the state is gone, the margin is restored.
A thin margin that disappears because a conflict or coup threat now covers the system has not resolved.
D47 then adds the front to the chronicle, with the BGS work you did for the faction there, and clears the choice.
A conflict that ended with a result in your journal is recorded as won or lost.

The next action is built in code from the front and from the bounties and combat bonds your journal
shows the faction paid you for in the last week and no redemption has followed:

```csharp
public sealed record CampaignNextAction(Front Front, string Sentence, string? NotCounting)
```

A war says that trade does not count, an election that combat does not count and that firing a weapon
may void some election missions, a Lockdown that only bounties help, and an Outbreak that bounty
vouchers do nothing. The model reads the answer and does not compose it.

The first sentence of the next action also shows on the NEXT strip under every tab of the full panel, with a Story and Campaign toggle; see [The NEXT strip](adventures.md#the-next-strip).

### The Liaison

The campaign faction has a Liaison, who speaks to you over comms about the front you work. D47 decides every
fact the Liaison states; the language model only words them, and is told to add nothing.

| When | What the Liaison says |
|---|---|
| You choose a front | The briefing: the situation, the margin, what is at stake and the next action, with what does not count there. |
| A BGS tick passes, and your journal has read the front's system since | The dispatch: what you handed in for the faction there since the tick, and what moved by your newest reading, with its age. |
| A BGS tick passes, and your journal has not read the system since | A request to fly in to the system for the morning report. The dispatch follows once you have. |
| You hand in work that counts on the front | A short acknowledgement, at most once every 30 minutes. |
| The faction wins its first day in a conflict, or a margin is back within its threshold | A short acknowledgement. |
| The front resolves | The outcome, then the next front proposed. |
| A BGS tick passes, and d47 has the faction's report | The conflicts briefing: every war, civil war and election the faction is in, active or pending, once per tick, as soon as d47 has the report. Each states its type, system, status and opponent (or that the opponent is not yet known), with days won where you have read the system and the faction's stake where it has one. Nothing is said without conflicts. |

The dispatch keeps your work and the movement as two separate facts. It never says how much of the change
your work caused, because the BGS does not report that. In a conflict influence is frozen, so the Liaison
speaks of days won and never of influence. Each tick has one dispatch, kept across restarts.

The Liaison's part follows the front: a field officer in a war, a campaign manager in an election, a relief
coordinator in a bad state, and a liaison officer otherwise. Its manner follows the faction's government. It
speaks whatever core is aboard, a stock core included, and waits while you are in a fight, in supercruise or
a jump, or in an Operation. Its voice is picked by the faction's name, so it is the same in every session; the
Liaison voice row chooses another. The Campaign Liaison row turns it off; see
[Campaign Liaison](callouts.md#campaign-liaison).

```csharp
public sealed record CampaignLiaisonLine(
    LiaisonBeat Beat,
    string Faction,
    string Part,
    string Manner,
    IReadOnlyList<string> Facts,
    bool Conflict)
```

### Arcs

An arc is fiction around the campaign's facts. D47 picks the arc, who is in it and when each act is due; the language
model writes the act. The model may invent people, motives, documents, history and grudges, and never a game fact: every
number, state, outcome and place comes from D47. Every act follows a change in the fronts or the chronicle.

The arcs speak at most once every two hours, one arc's lines at a time, such as the council's two arguments. Lines
about a war or civil war come first, then lines about an election, then the rest. The arcs may speak right after D47
starts. Lines that wait are kept and spoken later. The Liaison's briefings, dispatches and acknowledgements do not wait
for the arcs.

The first arc is the **Nemesis**, a rival faction with an invented leader. D47 ranks the rivals: the one that has fought
the campaign faction most, counting the chronicle and current conflicts, first; then the one in the most of the faction's
systems; then the one closest to it in influence in a shared system:

```csharp
.OrderByDescending(score => score.Fought)
.ThenByDescending(score => score.Shared)
.ThenBy(score => score.Gap ?? double.MaxValue)
.ThenBy(score => score.Name, StringComparer.Ordinal),
```

The Nemesis is the highest-ranked rival leading a front against the faction (a conflict against it, a coup threat by it,
or a thin margin over it), fixed the first time one does. Its leader is named then, a title and a surname drawn from the rival's name and your squadron, so
everyone in a squadron meets the same leader; outside a squadron the campaign faction stands in for the squadron. The
Nemesis and its leader are kept in `data/campaigns.json` until you name another campaign faction.

| When | What the Nemesis says |
|---|---|
| It first leads a front against the faction | The introduction: the leader, the front and why it is a front. |
| It wins a day in a conflict you have visited | A broadcast with the days won. |
| It moves ahead of the faction in a system where it leads a front | A broadcast with its lead in points. |
| The faction wins a conflict against it for the first time in that system | A concession. |
| The faction wins a conflict against it in a system they have contested before | A vow of revenge. |

Each act counts the grudge: in a system the two factions have contested before, by the chronicle, the act says which
time this is, such as "This is the third time Lave Purple Gang and Kestrel Reach Compact have contested Lave."

The leader speaks on the NPC voices' provider in a voice picked by the rival's name, through a broadcast link of strength
0.9; when that is the Liaison's voice and the provider has another, the next one is used. Like the Liaison, the Nemesis
waits while you are in a fight, in supercruise or a jump, or in an Operation. The first fronts computed after d47 starts
are what later broadcasts are measured against, so a lead or a day the Nemesis already held is not broadcast; an
introduction that is due is said. The Campaign Nemesis row turns it off;
see [Campaign Nemesis](callouts.md#campaign-arcs).

```csharp
public sealed record CampaignArcLine(ArcAct Act, string Faction, string Rival, string Leader, string System, IReadOnlyList<string> Facts)
```

The second arc is **Leaked plans**. An Informant, invented once with a call sign drawn from your squadron the way the
Nemesis's leader is, delivers an intercepted plan when d47 computes a rising threat from the rival's influence over the
last readings: your own readings of the system over the past week, or else the faction's previous and current galaxy
reports. Three threats open a leak, in this rank:

| Threat | When |
|---|---|
| Coup | A rival is rising and between 50% and 60% in a system your faction controls. |
| Expansion | A rival has an Expansion pending in a system where it leads a front against your faction. |
| Spread | A rival is rising in three or more of your faction's systems. The leak names the one where it is strongest. |

```csharp
public enum LeakKind
{
    Coup,
    Expansion,
    Spread,
}
```

A system already in a conflict is left out, since the threat has become one. The leak names a real system and states
only what d47 computed: the rival's influence, its rise in points and the 60% line. At most one leak is given at a
look. The same threat leaks again, a level higher, when the rival's influence has risen five points since the last leak,
to a most of three levels, and each later leak says what the earlier one found. A threat that passes is forgotten, so
its return is a new leak. The Informant and its leaks are kept in `data/campaigns.json` until you name another campaign
faction.

The Informant speaks in a pool voice picked by its name, stepped past the Liaison's and the Nemesis's, through a weak
link of strength 0.55 that thins the voice and drops it out. It waits as the Nemesis does, and the Campaign Nemesis row
turns it off.

The third arc is **Ideology**. It opens when your faction's government differs from that of a rival leading a front
against it; both governments come from the faction report. The rival's leader, the same one the Nemesis would have,
broadcasts propaganda naming both governments and the front. The arc then acts on three things, and only on these:

| Act | When |
|---|---|
| Propaganda | A front against the rival appears in a system it has not yet broadcast on. The fronts there when the arc opened are the baseline. One a look. |
| Defection | A front your faction won against the rival enters the chronicle. A defector from the rival speaks over a hailing frequency, link strength 0.7. |
| Election | An election is active or pending in a system both factions are in. The Liaison frames it as a contest of ideas and speaks for your government's idea of rule. Once for each of active and pending. One a look. |

The arc says nothing while the two governments are the same or either is unknown.

The fourth arc is **Internal politics**. When two or more fronts are open at once in different systems (every front but a
stale one), your faction's council gets two members, invented once from your squadron the way the Nemesis's leader is: a
Hawk and a Dove. The Hawk urges the first conflict or coup front, else the first front; the Dove urges another front.
Each argues once for theirs. When you then choose a front, a conflict or coup front sides with the Hawk and any other
with the Dove, and the one passed over speaks: what they had urged, and, from the second time, how many times they have
been passed over and the system of the last. A front chosen before the council opened is not sided. The Hawk and the Dove
speak in pool voices picked by their names, stepped past the Liaison's and each other's, over a clear link.

```csharp
public enum CouncilSide
{
    Hawk,
    Dove,
}
```

Both arcs are kept in `data/campaigns.json` until you name another campaign faction, wait as the Nemesis does, and the
Campaign Nemesis row turns them off.

The fifth arc is **Crisis and blame**. It opens when a bad state (Lockdown, Civil unrest, Famine, Blight, Drought, Outbreak,
Infrastructure failure, Pirate attack, Terrorist attack or Bust) becomes active in a system your faction controls and the
previous report did not have it. The Liaison announces it and names the rival with the most influence there as the one
blamed; the relief effort and the refugees are the Liaison's invention. When the blamed rival's influence in that system
then rises, its leader speaks once, exploiting the crisis. When the state is no longer active the Liaison says the crisis
is over and it closes. A state already active when d47 starts is the baseline, not news. The open crises are kept in
`data/campaigns.json`:

```csharp
public sealed record OpenCrisis(string System, string State, string? Blamed, DateTimeOffset Since, bool Exploited);
```

The sixth arc is **Legend**. A Chronicler, whose name d47 invents once from your squadron the way the Nemesis's leader is,
writes each chronicle entry once as faction history with you in it, from the entry's facts only: the kind of front, the
system, the opponent, the outcome and the ticks it spanned. The passage is stored on the entry and never written again.
It is written in the background on the turn model after a front resolves and at launch for entries that have none, and a
failed write is tried again at the next launch or resolved front. A passage that gives a number the facts did not, or runs
over 700 characters, is refused. The passage shows under its entry in the Chronicle section of the Campaign page and is read
aloud only on request:

> "read the chronicle"

D47 reads the latest three passages, newest first, and counts the older ones.

The Nemesis arc carries a **secret**, revealed over its acts. When the Nemesis is first named, or once a faction report
is computed for a Nemesis named earlier, the turn model writes the secret once in the background: either a traitor
inside your faction passing its plans to the rival, or someone other than the rival's leader directing its plans, picked
from the rival and your squadron so a squadron shares the kind. The model invents the culprit, the whole truth and three
reveals, the last naming the culprit. D47 checks the answer before keeping it and refuses it when a field is missing or
empty, there are not exactly three reveals, a text runs over 300 characters, any text has a digit, a reveal before the
last names the culprit or the last does not, or any text names a system in the faction report, its fronts or the
chronicle. A refused or failed write is tried again at the next launch.

The secret is kept with the Nemesis in `data/campaigns.json` and never shown. Time passing reveals nothing: a reveal is
due only when a front resolves into the chronicle after the secret was kept, and only these resolutions count:

```csharp
return (entry.Kind == CampaignFronts.Describe(FrontKind.Conflict) && entry.Outcome == "Won")
    || (entry.Kind == CampaignFronts.Describe(FrontKind.Coup) && entry.Outcome == "Resolved");
```

That is, a conflict your faction won, or a coup threat that has passed. Each such entry gives the next reveal, one at a
look, and the entry it was timed to is kept with it. The Liaison speaks the reveal in its own voice, naming the real
victory first; the last reveal also tells the whole truth. After the third, no reveals remain. Naming another campaign
faction clears it, and the Campaign Nemesis row turns the reveals off with the rest of the arc.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

`set_campaign_faction`, `set_system_intent`, `take_campaign_front`, `next_campaign_front` and `drop_campaign_front` are protected: the phrases above reach them, and the
model is refused.

#### `get_campaign`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `get_campaign_fronts`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `read_campaign_chronicle`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `next_campaign_action`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `take_campaign_front`

```json
{"type":"object","properties":{"kind":{"type":"string","description":"The kind of front there, when the system has more than one.","enum":["Conflict","Coup","Retreat","ThinMargin","BadState","UnwantedExpansion","Expansion","Stale"]},"system":{"type":"string","description":"The star system of the front; leave out to take the proposed front."}},"required":[],"additionalProperties":false}
```

#### `next_campaign_front`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `drop_campaign_front`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `set_campaign_faction`

```json
{"type":"object","properties":{"faction":{"type":"string","description":"The minor faction\u0027s exact name, as the Commander said it."}},"required":["faction"],"additionalProperties":false}
```

#### `set_system_intent`

```json
{"type":"object","properties":{"intent":{"type":"string","description":"What the Commander means to do there.","enum":["Hold","Grow","ExpandFromHere","StayUnder75","TakeControl","LeaveAlone"]},"system":{"type":"string","description":"The star system."}},"required":["intent","system"],"additionalProperties":false}
```

</details>
