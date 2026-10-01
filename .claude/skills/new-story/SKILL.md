---
name: new-story
description: Develop one d47 stock story with the maintainer — premise, both layers, and three sample chapters sized to his own journals — on his review page, revise it until he approves, then add it to the embedded story catalog. Use when the user invokes /new-story, /new-story <premise or title>, or says "new story", "write a stock story", "let's work on the next story", "continue the story".
---

# New story

You develop one stock story with the maintainer, from premise to an approved story in the catalog.
He reads both layers, the card and the hidden layer, before anything ships. Nothing is hidden from
him; the sealing keeps the hidden layer out of the tree's plain text, not out of his sight.

`/new-story <premise>` has named the work, so start on it in that same turn. A bare `/new-story`
reads the review page first: if a draft is there, ask whether to continue it or start another; if
none is, ask for a premise.

## Turn the voice on first

The first step of the working turn is `/claude-voice New story`. `/claude-voice off` stops it and the
work carries on unchanged.

## The rules

Every story keeps all ten. Check the draft against each before showing it.

1. The Commander starts broke, in a stock Sidewinder, with the `covas` core and no Guardian cores.
2. Act one ends with a data-link scan of a Guardian beacon.
3. No core is removed or made worse, Guardian or `covas`, in the app or the fiction. NPCs that exist
   only in the fiction may be lost.
4. The Commander never loses anything the game holds, and the story never says they did.
5. Every beat is one of the adventure beat kinds: the six in `TriggerKind` today and the ones
   specified in #709 and #710. Thargoids are allowed through `bond` with `thargoid: true`, `signal`
   and `wreck`. There are no Thargoid missions.
6. No clue or ending depends on an earlier choice.
7. Each story has a tone, genre and twist mechanism unlike the stories already in the catalog.
8. Hidden text never tells a core that another Guardian mind is alive. Other cores are spoken of
   only as Guardians of the past.
9. Plain prose, and never "wants" where "needs" is meant.
10. A year at least: the beat sheet follows the Save the Cat beats over the clue schedule in #708.

Within those rules: the setting is soft science fiction, and Elite's lore and d47's own may be bent
or overturned. In a Buddy Love story the partner exists only in the fiction.

Write the story in this session. Do not hand a layer to a subagent to write.

## Titles already used

New titles must not repeat these, or any title in `src/D47.Core/Stories/StoryCatalog.json`.

- The Marker, All Expenses Paid, The Counting Rhyme, Stock Class, Postdated, Late Start, Dead Drop,
  Long Distance, Form 47-B, The Countdown, Field Notes, Deputy, The Last Movement, Time Served,
  Page Nineteen, Unpublished, The Pilgrim, Accidental, Correspondence Game, The Strike Fund.
- Factory Settings, The Lifeboat, Episode 47, The Quiet Watchers, The Trial, The Wreck With My
  Name, The Salome Tape, The Claim, The Toast, The Glyph, The Dreamer, The Misjump, The Blackout,
  The Job, The Claimant, The Catalogue, The Grey Ghost, Terminal, The Long Sleep, The Passenger.
- Understudy, The Stranger's Kindness, Silent Wing.

## The format

The format is set by #707. Read it there, and once #707 has landed read it from `StorySecret` in
`src/D47.Core/Stories/StoryCatalog.cs` and `FIELDS` in `tools/seal-stories.py`, which win over the
issue.

- **The card**, in `StoryCatalog.json`: `id`, `number`, `title`, `genre` (one of the nine Save the
  Cat genres), `tone`, `blurb` (why a player would pick it, like the back cover of a novel),
  `inYourWords` (the Commander's backstory in the first person) and `beacon` (why they go to scan
  it).
- **The hidden entry**, sealed: `id`, `secret`, `beats` (one line for each of the 15 Save the Cat
  beats), 14 `clues` (4 weekly, then 10 monthly), 4 `finale` lines, `end`, and 1 to 4 `options`,
  each with `id`, `label`, `after` and `add` (persona ids, possibly none).

## The review page

The maintainer's page "d47 Stock Stories", https://claude.ai/artifact/HNGyX7PhikSQiMuhxxJP3g,
holds every draft between sessions. Start every session by reading it with the Artifact tool's
`read` action, and build on the version that comes back. Republish to that same `url` after every
change, so the page is never behind the draft. Load the `artifact-design` skill before the first
publish of a session.

Each story on the page shows the card, the whole hidden layer, the three sample chapters marked as
samples, any gap found (below), and its state: draft, approved, or in the catalog. Stories retired
before this skill existed are not drafts; drop them from the page.

## The steps

1. **Agree the premise, genre and tone** with the maintainer. Offer two or three concrete premises
   when he has not given one, each with its genre, tone and twist mechanism, and compare them with
   the catalog and the page under rule 7.
2. **Write both layers** to files in the session scratchpad, outside the repository. Never write
   hidden text into the tree: `NoSealedTextAppearsInTheTreeTests` fails on any hidden sentence of
   24 characters or more in a repository file.
3. **Sketch three sample chapters** against the maintainer's own game state:
   - the chapter that ends at the beacon scan,
   - a chapter from the middle of the year,
   - a comfort-zone chapter as #711 defines it.

   Read the state with

   ```bash
   python .claude/skills/new-story/game-state.py
   ```

   which takes the credits from the last `LoadGame`, the ship and jump range from the last
   `Loadout`, the ranks from the last `Rank` and the activity figures from the last `Statistics`,
   in `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\Journal.*.log`, and prints
   the #711 thresholds reached and the comfort-zone pick.

   Name each beat by its kind and filter from the tables in #709 and #710, with counts sized to
   that state and to the thresholds in #711. Where a beat the story needs cannot be expressed that
   way, say so on the page and name the issue it would change (#707 to #711). Do not invent a kind.
4. **Publish** both layers and the samples on the review page.
5. **Revise** on his notes, republishing after each round, until he approves the story. Mark it
   approved on the page.
6. **Add it to the catalog, once #707 has landed.** It has landed when it is closed, or when a
   commit on local `main` carries `Fixes #707`:

   ```bash
   gh issue view 707 --json state -q .state; git log main --format=%B | grep -x "Fixes #707"
   ```

   Until then, stop at the approved story on the page and tell the maintainer the catalog steps are
   waiting on #707.

   Once it has: add the card to `src/D47.Core/Stories/StoryCatalog.json` with the next `number`,
   decode the sealed layer into the scratchpad, add the hidden entry with the same `id`, and
   encode it:

   ```bash
   python tools/seal-stories.py decode <scratchpad>/sealed.json
   python tools/seal-stories.py encode <scratchpad>/sealed.json
   ```

   Sample chapters never go in the catalog. The app writes chapters when they are due.
7. **Delete the scratch files**: the drafts and the decoded layer.
8. **Build and test**, once the catalog files have changed:

   ```bash
   dotnet build d47.slnx -c Debug
   dotnet test tests/D47.Core.Tests --filter "FullyQualifiedName~Story|FullyQualifiedName~Adventure"
   dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~Gate
   ```

   The build must have 0 warnings. Add a `CHANGELOG.md` entry under the current unreleased heading
   naming the story by its title, and mark the story "in the catalog" on the page.
9. **Commit only when the maintainer says so.** The commit holds the two catalog files and the
   changelog, with a subject such as `Add the stock story <title>`. Do not push.

Nothing else in `src/` changes. A story that seems to need a code change is a gap: record it on the
page and name the issue (step 3).

## Finishing

A turn that hands the story back for review, or lands it, ends with the `/claude-voice` sentence
as its last tool call, then the written report: the page link, what changed this round, and any
gap found.
