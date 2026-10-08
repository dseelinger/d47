---
title: Bookmarks
group: Acting on the game
nav_order: 137
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Five steps to a name that plots a course.</p>
<section>
<h2><span class="num">1</span> Target a system, then bookmark it.</h2>
<p class="say">Target a system or station in the map, then say "bookmark this".</p>
<p class="expect">D47 says, for example, "Bookmarked Jameson Memorial, in Shinrarta Dezhra."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a bookmark command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">bookmark this</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It saves at once, under a name taken from the destination.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Add "as Current CG" to choose the name.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Set a course for it by name.</h2>
<p class="say">Say "set course for Jameson Memorial".</p>
<p class="expect">D47 plots to the system the bookmark points at.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a course command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">set course for Jameson Memorial</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It works like naming any other system.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> List what you have.</h2>
<p class="say">Say "what are my bookmarks".</p>
<p class="expect">D47 names each bookmark and the system it points at.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a list command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what are my bookmarks</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Each bookmark names the system it points at.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Rename or delete one.</h2>
<p class="say">Say "rename bookmark Current CG to Colonia Bridge", or "delete bookmark Current CG".</p>
<p class="expect">The name changes and the system stays the same, or the bookmark is removed.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a rename command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">rename bookmark Current CG to Colonia Bridge</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The system it points at does not change.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"delete bookmark Current CG" removes it.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> Target the system, not a body in another system.</h2>
<p class="say">Target the system itself when the destination is in another system.</p>
<p class="expect">D47 refuses otherwise, saying "That is in another system, and Elite does not name the system."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="A destination in another system is refused">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">bookmark this</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Elite does not name the system of a body elsewhere.</text>
</svg>
</section>
</div></div>
</details>

## The details

A bookmark is d47's own, not Elite's — it is not one of the game's own galaxy-map bookmarks, and
it is not written anywhere Elite reads. It is a name you give a system, kept in
`data/bookmarks.json`, so you can say the name later and get a course plotted to it.

A bookmark points at a system, never at a body or a station inside one. Bookmarking a station
targeted as your destination stores the system it is in.

### Making one

Target a system or a station in the galaxy map or the system map, then say:

> "bookmark this"

That saves it at once, under a name chosen from what Elite calls the destination:

```text
Bookmarked Jameson Memorial, in Shinrarta Dezhra. Say 'set course for Jameson Memorial' to go there.
```

The name is cleaned so it can be said out loud — anything that is not a letter, digit or space
becomes a space, runs of spaces collapse, and it is cut to 40 characters at a word boundary. A
procedural name such as *Col 285 Sector AB-C d1-23* becomes *Col 285 Sector AB C d1 23*. Where
Elite gives no sayable name at all, the bookmark is called *Bookmark 1*, *Bookmark 2* and so on.

Procedural names are hard to say, and speech recognition will not reliably turn "LTT 7786" back
into text from a spoken command. The confirmation line states the exact phrase to use, so if the
chosen name does not come out right when you say it back, rename the bookmark instead.

To give it a name from the start, say it as part of the command:

> "bookmark this as Current CG"

### Using one

> "set course for Current CG"

works exactly like naming any other system, with the destination filled in from what was stored.

### Seeing what you have

> "what are my bookmarks"
> "list my bookmarks"

names each one and the system it points at.

### Renaming one

Through conversation:

> "rename bookmark Current CG to Colonia Bridge"

The system it points at does not change.

### Deleting one

> "delete bookmark Current CG"
> "forget bookmark Current CG"

Deleting is never reachable by the model, only by one of these phrases, the panel or a hotkey —
a misheard name passed to the model would remove a bookmark with no way back.

### The one thing it cannot do

A bookmark only records what `Status.json` says the destination is. Where the destination is a
body or a station in a system other than the one you are in, Elite's own status file does not name
that system, so bookmarking it is refused:

```text
That is in another system, and Elite does not name the system. Target the system itself.
```

Target the system itself instead.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `bookmark_destination`

Save the current destination (from Status.json) as a bookmark, so 'set course for <name>' returns
to it later. Without a name it is saved at once under the destination's own name, which can be
renamed afterwards.

```json
{"type":"object","properties":{"name":{"type":"string","description":"What to call the bookmark. Optional \u2014 omit to name it after the destination."}},"required":[],"additionalProperties":false}
```

#### `list_bookmarks`

List the Commander's bookmarks, each with the system it points at.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `rename_bookmark`

Rename an existing bookmark. The system it points at does not change.

```json
{"type":"object","properties":{"name":{"type":"string","description":"The bookmark\u0027s current name."},"new_name":{"type":"string","description":"The name to give it instead."}},"required":["name","new_name"],"additionalProperties":false}
```

#### `delete_bookmark`

Delete a bookmark by name.

```json
{"type":"object","properties":{"name":{"type":"string","description":"The bookmark\u0027s name, exactly as listed."}},"required":["name"],"additionalProperties":false}
```

</details>
