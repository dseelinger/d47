# Changelog

<!--
  Placeholders, not release history. The entries below record the file's shape — headings newest
  first, `## <version> — <title>` — and nothing about what shipped. `D47.Core.csproj` embeds this
  file as the `D47.Core.Changelog` resource for the About dialog; nothing else parses it.
-->

## 0.110.8 — The empty Checklist names what it is filtered by, and the tab strip decides its own width

"Nothing on your list matches that." never said what "that" was. On the full panel the query box
and the scope button sit beside the message, so the gap was survivable; in mini neither is on
screen, and the query still narrows the list from wherever it was typed. The message now names the
query, the scope, or both, from the same two values the filter itself reads — "Nothing on your list
matches 'limpets'.", "Nothing on your list is in A ship's build.", "Nothing in A ship's build
matches 'limpets'." — and the empty state grows a Clear filter button, since mini has no search box
to clear the query from.

A window that opened narrower than the tab strip's words drew the words anyway, and only
collapsed to marks once the Commander touched the edge — the collapse decision ran only from a
resize, so a window that opened at a narrow size and sat still never made it. The decision is now
made once against a settled measurement before the window is first shown, so it opens in the state
it would otherwise only reach after a drag.

## 0.110.7 — The checklist follows the ship, and Sourcing opens on the headset

Filtering the checklist to the engineer in this system now answers from the system the ship is in.
The page listened to the list, the proposals, the filter and the goals, and to nothing that says
where the Commander is — so jumping into an engineer's system left the empty message on screen with
that engineer's work behind it, and left the partial-grades checkbox and the rank line describing
the system just left. The page redraws when the system changes, and on nothing else: docking,
dropping out of supercruise and every other event inside one system redraw nothing.

Sourcing — where to buy what a build still needs — is now the Checklist's second root on the
headset. It was withheld there because the carrier figure is typed and the headset had no
keyboard of its own; a press on the carrier box opens the drawn keyboard the panel already has,
and the figure is written once when the board closes. The desktop window is unchanged.

## 0.110.6 — Settings rows that open a window

Every Settings row that opens a second window — memories, the debrief, notes, the logbook, the
audio recorder, coverage, ship cores, macros, switches, and the arrow that clears a stored key —
now refuses a headset press and says “Not currently supported in VR” on the panel.
A ray press used to reach the handler, which would have tried to open a dialog over a window that
is never shown. The desktop window is unchanged.

## 0.110.1 — What can I do?

The ask box's empty text is now a question you can actually type. It read "Ask D47 something";
it reads "What can I do?", and on a Commander's first run still offers "where am I" and
"what's your status" beside it.

## 0.75 — placeholder

**The release history for this version was not recovered.** This heading exists because the
checkout is missing its changelog and the build embeds one. See the comment above.

## 0.1.0 — placeholder

**The first release's entry was not recovered either.** Present so the file carries the
newest-first ordering the real one has.
