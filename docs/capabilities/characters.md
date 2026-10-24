---
title: Characters
group: Ship
nav_order: 164
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a character you can talk to.</p>
<section>
<h2><span class="num">1</span> Add one on Asset Mgmt › Characters.</h2>
<p class="say">Open Asset Mgmt, press Characters, press ADD A CHARACTER, type a name and press SAVE.</p>
<p class="expect">The name joins YOURS in the sidebar. Its page opens with the picture, voice, notes and where they are.</p>
</section>
<section>
<h2><span class="num">2</span> Say their name.</h2>
<p class="say">Say the character's name and a question, such as "Harlan, are you there".</p>
<p class="expect">The character answers in their own voice, shaped by your notes, and keeps the line for your next question.</p>
</section>
<section>
<h2><span class="num">3</span> Say where they are.</h2>
<p class="say">Check Aboard for someone on your ship, or leave it clear and type the system they are in.</p>
<p class="expect">An aboard character is clear on the intercom. One elsewhere is fainter the further away they are, and out of range beyond 500 light years.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">One page for everyone you can talk to, with the same controls for each.</p>
<section>
<h2><span class="num">1</span> Three kinds of character share one page.</h2>
<p class="body">The sidebar lists your carrier's captain and tower, the cast of the story you are following, and the characters you made. Each page has a picture, a name, a voice, personality notes and where they are. What you can edit depends on who owns the value.</p>
</section>
<section>
<h2><span class="num">2</span> The captain and tower keep their settings.</h2>
<p class="body">Their names and voices are settings, the same for every Commander on this PC. Your notes on them are yours alone, kept with your other characters.</p>
</section>
<section>
<h2><span class="num">3</span> A story sets its own cast.</h2>
<p class="body">A story gives its characters their names, voices and pictures. You can add notes, kept for that story only.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="crew.html"><span class="ct">Crew →</span><span class="cd">The pilots you hired in the game.</span></a>
<a class="card" href="carrier.html"><span class="ct">Carrier →</span><span class="cd">The captain and tower, and the carrier they speak for.</span></a>
</div>
</div>
</div></div>

## The details

Asset Mgmt › Characters has a sidebar and a detail page, in the window and on the headset's big panel.

```text
CARRIER   the captain, then the tower
STORY     the running story's introduced cast, in the order they were introduced
YOURS     your own characters, in the order you added them, then ADD A CHARACTER
```

Each character's page has the same rows:

| Row | Your character | Captain and tower | Story cast |
|---|---|---|---|
| Picture | change or use the default | change or use the default | shown |
| Name | text box | the setting `speech.carrierCaptainName` or `speech.towerName` | read-only |
| Voice | voice picker | the setting `speech.carrierCaptainVoice` or `speech.towerVoice` | read-only |
| Personality notes | up to 600 characters | up to 600 characters | up to 600 characters |
| Where | Aboard, or a system | the carrier's callsign, system and distance | Aboard, set by the story |

An existing character saves each field as you change it. A value that breaks a rule is not saved, and the
field shows the reason:

- the name is empty, or longer than 32 characters;
- you already have a character with that name;
- the character is not aboard and has no system.

ADD A CHARACTER opens an empty NEW CHARACTER with SAVE and CANCEL. REMOVE CHARACTER asks first, in place, and
cannot be undone: the name, voice, picture and notes are forgotten.

Away from the ship, the distance is looked up with Galaxy search. With Galaxy search off the page says the
distance is unknown, and the character is clear on the radio. A character is stored in `data\characters.json`
for each Commander; their picture is `character.<id>` in the pictures folder.

The page has no tool and no setting of its own. Say a character's name to reach them; see
[Crew](crew.html) for the same way of addressing a hired pilot.
