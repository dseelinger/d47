---
title: Commander log
group: Knowledge
nav_order: 160
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Ask whether you have met a Commander before.</p>
<section>
<h2><span class="num">1</span> Ask about the one you just targeted.</h2>
<p class="say">Target a Commander, then say "have I met this commander".</p>
<p class="expect">D47 lists your encounters with them, newest first: when, the system, their ship where known, and what happened.</p>
</section>
<section>
<h2><span class="num">2</span> Ask about a name.</h2>
<p class="say">Say "have I met Arden Vale before".</p>
<p class="expect">D47 lists that Commander's encounters, at most ten, with the total.</p>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Ask right after starting D47.</p>
<p class="expect">D47 says older journals are still being read and gives what it knows so far.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">The answer comes from your own journals.</p>
<section>
<h2><span class="num">1</span> Journals are read at start.</h2>
<p class="body">D47 reads every journal when it starts and keeps who you met, where and how. Message text is never kept.</p>
</section>
<section>
<h2><span class="num">2</span> No name means the model sees none.</h2>
<p class="body">"Have I met this commander" is answered on this machine. Asking the AI model about a name sends that name and your encounters with them, and Privacy and egress says so.</p>
</section>
</div></div>
</details>

## The details

The tool `get_commander_log` reads the Commander log, which is rebuilt from the journals at every
start and extended as you play. It is open to the AI model as well as to you, because it changes
nothing.

```text
2026-10-12 19:40 UTC, Shinrarta Dezhra, Python: targeted, interdicted you
```

Each line gives when the meeting ended, the system, the ship where known, and what happened.
Encounters are merged per system and game session. Without a name the answer is the Commander
targeted most recently this session; if none was targeted, it is the ten Commanders met most
recently, each with their last encounter.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_commander_log`

Encounters with other Commanders, read from the journals: when, the system, their ship where
known, and what happened. With a name, that Commander's encounters, newest first. Without one,
the Commander targeted most recently this session, or if none, the Commanders met most recently.
Never includes message text.

```json
{"type":"object","properties":{"name":{"type":"string","description":"Who to look up. Leave out for the one just targeted."}},"required":[],"additionalProperties":false}
```

</details>
