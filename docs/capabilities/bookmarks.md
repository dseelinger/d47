---
title: Bookmarks
group: Acting on the game
nav_order: 136
---

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
