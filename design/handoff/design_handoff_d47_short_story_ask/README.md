# Handoff: the Short Story ask form (2026-10-21)

Build issue: none filed yet.
Canvas: https://claude.ai/artifact/6YvF7CHt22p8cNP3ndNeh6 (private to the maintainer). Option A was chosen; option B (the ask as a sentence) was not.
This handoff departs from option A on the canvas: the brief is a tile that opens the voice entry, not a text field, and it adds CLEAR, the reach hint and the shut Notice. Where they differ, this handoff is right.

## What changed
Stories › Short Story, and the Next chapter page that uses the same form (`AdventuresPage.BuildAsk`). The
behaviour is unchanged: the same choices, the same defaults, the same brief entry, the same write, stop and
shut rules. The layout changes:

1. **Labelled rows.** Each choice is a row: a grey label (REACH, LENGTH, SHIP, BRIEF) in a 120px column,
   the control in the second column. The options lose their `Reach: ` and `Length: ` prefixes.
2. **Content is capped at 900px**, as the design system requires. The tiles no longer span the window.
3. **A hint under each choice** says what the current option does (01, 02).
4. **The brief is a full-width tile** that shows the brief, or the entry's own hint while it is empty, with a
   CLEAR tile beside it once it is set. Pressing it opens the existing brief entry, unchanged.
5. **GO sits in an action row** under a `line2` rule, aligned with the controls. STOP appears beside it while a
   story is written, and the progress block sits under the row (03).
6. **The page title is removed.** "Ask for a short story" repeated the sub-tab; "Write the next chapter" repeated
   the breadcrumb. The intro line stays and is shorter.
7. **When asking is shut**, a `warn` Notice at the top of the form names why, with OPEN SETTINGS in it,
   and GO is disabled (04, 05). It replaces the grey status line and the separate Open settings button.

The header (title bar, tab row, title line, sub-tabs or breadcrumb, avatar) is drawn for context only. It is
unchanged and out of scope.

**Start here:** read this README, then work through `CHECKLIST.md`, comparing the build against `screenshots/`.

## About the design files
`D47 Short Story Ask.dc.html` is a **design reference made in HTML**: a clickable prototype of the look and
behaviour, not production code. Recreate it in the Avalonia app with the existing kit styles and controls
(`Segment`, `LabeledCheckBox`, `Notice`, `WriteProgressBar`). Open it in a browser with `support.js` beside it
(it loads React from unpkg). It loads the app's own fonts from `assets/fonts`, so open it from inside the
repository.

In the prototype, the segments and the checkbox work, pressing the brief tile fills in a sample brief (the app
opens the entry instead), CLEAR empties it, GO moves to the writing state and STOP moves back. The Tweaks props
are `theme` (`elite`, `dark`, `light`, `elite-palette`), `state` (`ready`, `writing`, `shut-model`,
`shut-galaxy`), `follows` (the Next chapter page) and `hasChoice` (whether the SHIP row shows). The same
settings can be given in the URL hash, which is how the screenshots were captured:
`#theme=dark&state=writing&reach=1&length=2&ship=1&brief=1&follows=1&noships=1`.

## Where the strings come from
From the code, unchanged: the option labels (without their prefixes), "This ship only", the brief entry's
hint used as the empty brief text, both shut reasons from `AskShutBecause`, "Open settings", "Go", "Stop", and
everything in the progress block (`AdventuresPage.Doing`, the clock, `StoryProgress.Hint`).

Proposals, change freely:
- Intro, ask: "D47 writes a story for you to fly, then waits for your yes. GO on its own is a complete ask."
- Intro, next chapter: "D47 writes the story that follows {name}, from how it ended, then waits for your yes."
- Reach hint: "Each leg of the story is up to about {N} ly." N is `AdventureGenerator.Radius(reach,
  jumpRange, carrier.Owned)` for the Commander now: the writer is told it is the longest one hop may be, and
  places are searched for within it. It does not depend on SHIP. The screenshots use a 45 ly jump range and no
  carrier: 135, 540, 1,500 (5,000 for ANYWHERE with a carrier).
- Length hints, in the words `docs/capabilities/adventures.md` uses: "Three objectives: setup, turn, resolution.",
  "Five objectives: setup, catalyst, midpoint, all is lost, finale.", "Eight objectives or more."
- Ship hints: off "Any ship you own may be part of it."; on "Only the ship you are flying now."
- Brief hint: "Optional. Press it to say it, or type it."
- Notice head: "CAN'T WRITE A STORY YET".

The sample brief and the story name "The Long Haul" are placeholders.

## Fidelity
**High fidelity.** Colours are Palette roles only, from the Directive 47 design system. Type is the system's
three faces. Zero radius, no shadows. Glow only where the header already has it.

## Screenshots
`NN-state-theme.png`, captured headlessly at 1440×900, and 08 at 1024×640.

| # | State | Themes |
| --- | --- | --- |
| 01 | Ready, every choice at its default, no brief | all four |
| 02 | Choices changed: a session's flying, long, this ship only, a brief set | all four |
| 03 | Writing: GO disabled, STOP, the progress block | all four |
| 04 | Shut: no language model | all four |
| 05 | Shut: galaxy search off | Elite |
| 06 | No ship to choose between: the SHIP row is left out | Elite |
| 07 | Next chapter: breadcrumb and its intro | Elite |
| 08 | Headset big panel at its default 1024×640, no title bar | Elite |

Theme suffixes: `elite`, `dark`, `light`, `elite-palette` (Match my Elite colours).

## The spec

### Page
- The form sits in a column at most 900px wide, 32px below the sub-tabs or breadcrumb. It scrolls vertically only.
- Top to bottom, 28px apart: intro, Notice (only when shut), the choice rows, the action row, the progress
  block (only while writing). The choice rows are 20px apart.
- Intro: Sintony 16, line height 1.5, `grey`.

### Choice rows
- Two columns: label 120px, control `1fr`, 24px apart.
- Label: Saira 13/500, uppercase, tracking 0.06em, `grey`, centred on the 44px control (top-aligned to it when a hint follows).
- Hint: 8px under the control, Sintony 13, line height 1.4, `grey`. Numbers in it are JetBrains Mono 12.
- **REACH** and **LENGTH**: a `Segment` of three equal tiles, 2px apart, 44px tall. Labels Saira 13/600,
  uppercase, tracking 0.06em. At rest `tile` with `a` text, hover `tile2`, selected solid `a` with `knock` text.
  The automation names stay "Reach" and "Length".
- The hint under REACH and LENGTH redraws when the selection changes.
- **SHIP**: the existing checkbox tile, "This ship only", 44px. The hint sits 16px to its right on the same line,
  and wraps under it when there is no room. Left out, label and all, when there is nothing to choose between (as
  now, #202).
- **BRIEF**: a 44px tile filling the control column. Text Sintony 15, 14px from the left, cut with an ellipsis.
  Empty: the entry's hint, "A theme, a mood, a place it must include. Empty is fine.", in `grey`. Set: the brief
  in `white`, without quote marks. Hover `tile2`. Pressing it opens the brief entry as the Brief button does now.
- Once a brief is set, a CLEAR tile sits 2px to the right of it, 44px, Saira 13/600. Pressing it empties the
  brief without opening the entry.

### Action row
- A 1px `line2` rule across the form's width, then 20px, then the row, on the same two columns with the
  first one empty.
- GO: a default tile 200px wide, 44px. Disabled while writing or while shut: `slab` ground, `grey2` text.
- STOP: the same size, 2px to the right of GO, shown only while writing.

### Progress block (while writing)
- In the control column, 28px under the action row. The content is the existing `WriteProgressBar`; only its
  place changes.
- First line: the stage words in Sintony 15 `white` on the left, the clock in JetBrains Mono 12 `a` on the right.
- 8px below: the gauge, 8px tall, `tile` track, `a` fill.
- 8px below: the hint, Sintony 13 `grey`, hidden when empty.
- A write's aside (`status.Say(aside)`) shows under the hint in the same style.

### Shut Notice
- The existing `Notice` at `NoticeLevel.Warning`: missing a model or galaxy search is a caution the Commander
  can act on in Settings, not a failure, so it is amber, not red.
- Between the intro and the first row. 3px `warn` bar on the left, `warn-ground` fill, padding `14 16`, 10px
  between its parts.
- Head: Saira 13/600, uppercase, tracking 0.06em, `warn`.
- Body: the `AskShutBecause` sentence in Sintony 15, line height 1.5, `white`.
- OPEN SETTINGS: a default tile; it opens Settings as the button does now.
- The choices stay usable while shut; only GO is disabled.

### Headset
- The big panel renders the same page (`AShortStoryShowsHowFarItsWriteHasGotTests` presses GO on it). At its
  default 1024×640 the two columns hold: no row stacks and nothing is cut (08). The form scrolls when it is taller
  than the panel.

### Settled writes and failures
Unchanged. A failed write still uses the status line's failure Notice, placed where the progress block was.

## Documentation
`docs/capabilities/adventures.md` describes this form (around "choosers, each with a default"). Its third
chooser is called **Using** there; the form calls the row SHIP. Bring the page in line when this is built.
