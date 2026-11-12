---
title: Watches
group: Knowledge
nav_order: 165
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Ask what D47 is watching for you. Say or type a next-system watch, or set up any watch on the Watches page.</p>
<section>
<h2><span class="num">1</span> Ask for the list.</h2>
<p class="say">Say "list my watches" or "what am I watching".</p>
<p class="expect">D47 names each watch with its condition and when it last fired, with no model, key or network.</p>
</section>
<section>
<h2><span class="num">2</span> Add one by voice or on the page.</h2>
<p class="say">Say or type "tell me the next time we're in a system with a material trader". Or open Commander, then Watches, and press Add stock watch or Add Community Goal watch.</p>
<p class="expect">D47 replies "Watching for a Material trader, Commander" with no model. Add a kind or pad as you speak: "a manufactured material trader with a large pad". A stock watch on the page needs a station, a commodity, Above or Below, and a number of units. A Community Goal watch needs only a name.</p>
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
to you, because it changes nothing. A stock or Community Goal watch is added, changed or deleted on the Watches page.
A next-system watch is also added by saying or typing "tell me the next time we're in a system with" a service, and
deleted with "stop watching for" a service. The services are Black Market, Interstellar Factors, Material Trader
(also "materials trader" or "mats trader"), Outfitting, Refuel, Repair, Shipyard, Technology Broker (also "tech broker"),
Universal Cartographics and Vista Genomics. A kind (Raw, Manufactured, Encoded, Human or Guardian) may come before or
after the service, and "with a large pad" sets the pad. If you are already in a system with a match, D47 names the
station and still keeps the watch for the next system. The two tools below are the Commander's own: they are refused
if the model asks for them.

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

List the Commander's background watches: each one's name, its condition, whether it is on, and the BGS tick it last fired for. Stock and Community Goal watches are set up on the Watches page.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `add_next_system_watch`

Add a next-system watch for a station service. Reached only by saying "tell me the next time we're in a system with".

```json
{"type":"object","properties":{"kind":{"type":"string","description":"Raw, Manufactured or Encoded for a material trader; Human or Guardian for a technology broker; left out for any.","enum":["Raw","Manufactured","Encoded","Guardian","Human"]},"pad":{"type":"string","description":"The smallest pad the station must have; left out for any.","enum":["Medium","Large"]},"service":{"type":"string","description":"The station service to watch for.","enum":["Black Market","Interstellar Factors","Material Trader","Outfitting","Refuel","Repair","Shipyard","Technology Broker","Universal Cartographics","Vista Genomics"]}},"required":["service"],"additionalProperties":false}
```

#### `delete_next_system_watch`

Delete a next-system watch by its service and kind. Reached only by saying "stop watching for".

```json
{"type":"object","properties":{"kind":{"type":"string","description":"Raw, Manufactured or Encoded for a material trader; Human or Guardian for a technology broker; left out for any.","enum":["Raw","Manufactured","Encoded","Guardian","Human"]},"service":{"type":"string","description":"The station service of the watch to delete.","enum":["Black Market","Interstellar Factors","Material Trader","Outfitting","Refuel","Repair","Shipyard","Technology Broker","Universal Cartographics","Vista Genomics"]}},"required":["service"],"additionalProperties":false}
```

</details>
