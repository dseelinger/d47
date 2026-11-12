---
title: Watches
group: Knowledge
nav_order: 165
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Ask what D47 is watching for you. Watches are set up on the Watches page, never by voice.</p>
<section>
<h2><span class="num">1</span> Ask for the list.</h2>
<p class="say">Say "list my watches" or "what am I watching".</p>
<p class="expect">D47 names each watch with its condition and when it last fired, with no model, key or network.</p>
</section>
<section>
<h2><span class="num">2</span> Add one on the page.</h2>
<p class="say">Open Commander, then Watches, and press Add stock watch or Add Community Goal watch.</p>
<p class="expect">A stock watch needs a station, a commodity, Above or Below, and a number of units. A Community Goal watch needs only a name.</p>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Add a watch soon after D47 starts.</p>
<p class="expect">The watch saves. Nothing is checked until D47 has read the time of the last BGS tick.</p>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 checks your watches in the background and tells you once.</p>
<section>
<h2><span class="num">1</span> Stock watches.</h2>
<p class="body">A stock watch holds when a station's stock of a commodity is above or below your number. A rare fills in its station and commodity from a table inside D47. Any other station is looked up once, when you save, and its market ID is stored.</p>
</section>
<section>
<h2><span class="num">2</span> New Community Goal watches.</h2>
<p class="body">This watch reads GalNet's wording of a launch article, so it can miss or misread an announcement.</p>
</section>
<section>
<h2><span class="num">3</span> Once per BGS tick.</h2>
<p class="body">When a stock or Community Goal watch holds, D47 says so once and not again until the next BGS tick. A stock watch reads the market index; a Community Goal watch reads GalNet.</p>
</section>
<section>
<h2><span class="num">4</span> Next-system watches.</h2>
<p class="body">This watch names a station service and a smallest pad. On arrival in a system, D47 asks Spansh once for its stations and checks every next-system watch against them. Carriers and megaships are left out. On a match, D47 names the nearest station and turns the watch off.</p>
</section>
</div></div>
</details>

## The details

The tool `list_watches` lists the watches in `data\watches.json`. It is open to the AI model as well as
to you, because it changes nothing. A watch is added, changed or deleted on the Watches page only.

```text
Soontill Relics at Cheranovsky City: Soontill Relics above 200 at Cheranovsky City, Ngurii. 3 Nov · tick 14:07.
```

The file holds at most 32 watches. Saving a stock watch for an any-station choice sends the system and
station name to Spansh and the commodity name to Ardent once; a rare sends
nothing on save. While a next-system watch is on, each system you arrive in is sent to Spansh to list its
stations. A change to the station, commodity, comparison or number clears the stored firing, so the
watch can fire again in the same tick.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `list_watches`

List the Commander's background watches: each one's name, its condition, whether it is on, and the BGS tick it last fired for. Watches are set up on the Watches page only.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

</details>
