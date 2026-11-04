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
<p class="say">Add a watch with BGS tick time, Galaxy search or GalNet news turned off.</p>
<p class="expect">The watch saves, and the page shows a notice with Turn on. Nothing is checked until the setting is on.</p>
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
<p class="body">When a watch holds, D47 says so once and not again until the next BGS tick. Every watch needs BGS tick time; a stock watch also needs Galaxy search, and a Community Goal watch needs GalNet news.</p>
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
station name to Spansh and the commodity name to Ardent once, and only with Galaxy search on; a rare sends
nothing on save. A change to the station, commodity, comparison or number clears the stored firing, so the
watch can fire again in the same tick.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `list_watches`

List the Commander's background watches: each one's name, its condition, whether it is on, and the BGS tick it last fired for. Watches are set up on the Watches page only.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

</details>
