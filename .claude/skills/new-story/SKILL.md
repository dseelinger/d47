---
name: new-story
description: Develop one d47 stock story with the maintainer — premise, both layers, and three sample chapters sized to his own journals — on a draft page of its own, revise it until he approves, then add it to the shared review page and the embedded story catalog. Use when the user invokes /new-story, /new-story <premise or title>, or says "new story", "write a stock story", "let's work on the next story", "continue the story".
---

# New story

You develop one stock story with the maintainer, from premise to an approved story in the catalog.
He reads both layers, the card and the hidden layer, before anything ships. Nothing is hidden from
him; the sealing keeps the hidden layer out of the tree's plain text, not out of his sight.

`/new-story <premise>` has named the work, so start on it in that same turn. A bare `/new-story`
looks for draft pages first (below): if there are any, ask whether to continue one or start
another; if there are none, ask for a premise.

## Turn the voice on first

The first step of the working turn is `/claude-voice New story`. `/claude-voice off` stops it and the
work carries on unchanged.

## The rules

Every story keeps all nine. Check the draft against each before showing it.

1. The card names its level, `new`, `midrange` or `endgame` (#739), and the backstory starts there:
   broke in a stock Sidewinder for `new`, a working ship and some savings for `midrange`, a fleet
   and a fleet carrier for `endgame`. Every level starts with the `covas` core and no Guardian
   cores.
2. Act one ends with a data-link scan of a Guardian beacon. The scan brings the story's Guardian
   core aboard (#716), and the `breakIntoTwo` beat says so without naming the core. Every story
   names its own core on the card, chosen to match the story, and its first chapter ends with a
   `beacon` beat for the scan.
3. No core is removed or made worse, Guardian or `covas`, in the app or the fiction. NPCs that exist
   only in the fiction may be lost.
4. The Commander never loses anything the game holds, and the story never says they did.
5. Every beat is one of the adventure beat kinds: the six in `TriggerKind` today and the ones
   specified in #709 (combat and trade), #710 (exploration and on foot), #732 (conflict and
   faction work), #734 (carrier, squadron and team) and #736 (suit mods and livery). Odyssey is
   assumed (#730), so on-foot beats are fine. Thargoids are investigated, never fought: `signal`
   with `Thargoid`, `wreck`, and `salvage` with `metaalloys`. A live Thargoid ship is flavour only
   and never a beat, because none can be found on demand. There are no Thargoid missions, and no
   PvP beats.

   Set aside until their spikes are flown: skimmer massacres (`Mission_Massacre_Skimmer`, #721),
   Disable (`Mission_Disable*`, #722), Hack (`Mission_Hack*`, `Mission_OnFoot_Hack*`, #723), Scan
   (`Mission_Scan`, #724) and damaged-station missions (`Mission_RS_*`, `Mission_DS_*`, #725). No
   beat, clue or sample chapter uses them.
6. No clue or ending depends on an earlier choice.
7. Hidden text never tells a core that another Guardian mind is alive. Other cores are spoken of
   only as Guardians of the past.
8. Plain prose, and never "wants" where "needs" is meant.
9. A year at least: the beat sheet follows the Save the Cat beats over the clue schedule in #708.

Within those rules: the setting is soft science fiction in Elite's galaxy of the 34th century, and
Elite's lore and d47's own may be bent or overturned. Jobs, institutions, objects and slang belong
to that setting: a registry clerk works at a starport, a reporter files to a newsfeed, and nothing
is posted, phoned or driven. In a Buddy Love story the partner exists only in the fiction.

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

Read the format from `StoryCard` and `StorySecret` in `src/D47.Core/Stories/StoryCatalog.cs` and
from `tools/seal-stories.py`. They win over this list, which also names fields that open issues
add.

- **The card**, in `StoryCatalog.json`: `id`, `number`, `title`, `genre` (one of the nine Save the
  Cat genres), `tone`, `level` (#739), `blurb` (why a player would pick it, like the back cover of
  a novel), `inYourWords` (the Commander's backstory in the first person, starting at the level),
  `beacon` (why they go to scan it) and `core` (#716: the Guardian core the story is written for,
  never `covas` or `heretic`). Until #739 and #716 land, show `level` and `core` on the page and
  leave them out of the catalog.
- **The hidden entry**, sealed: `id`, `secret`, `beats` (one line for each of the 15 Save the Cat
  beats), 14 `clues` (4 weekly, then 10 monthly), 4 `finale` lines, `end`, 1 to 4 `options`, each
  with `id`, `label`, `after` and `add` (persona ids, possibly none), and `cast`.
- **Lines** (clues and finale) each have `text` and `speaker`: `ship`, `narrator` or a cast `id`.
- **The cast**: each member has `id`, `name`, `who`, `provider` (`kokoro`, or `chatterbox` only
  when the story needs it, #41) and `voice`. Voice `own` is the Commander's recording (#713).
  `primary: true` (#717) marks a recurring character named in the card's `blurb` or
  `inYourWords`, never one with voice `own` (#737). Every story has at least one primary member,
  and every named cast member gets a picture, `assets/stories/<story-id>.<cast-id>.png`, a square
  PNG. The page gives an image prompt for each named NPC, matching the style of the pictures
  already shipped. Prompts set the person in the 34th century: flight suits, station corridors,
  cockpits and ship interiors, never present-day offices, clothes or devices. A member with voice
  `own`, or a speaker with no face such as a broadcast or a recorder, gets no prompt. The
  Commander can change any member's voice and picture later (#737), so the pinned voice is a
  default, not a constraint on the text.
- **A romantic lead** has two versions, keyed `forMan` and `forWoman` by the Commander who sees them (#746):
  one gender-neutral name for both ("Alex") or a name for each ("Ellis" and "Ellie"), and a
  default voice and a picture prompt for each. The card never names the lead and calls it by
  role ("the engineer"); hidden text names it with `{name:<cast-id>}` and gives it no pronoun. In the
  sealed entry the member carries `versions` (`forMan`, `forWoman`, each `name`, `voice` and an
  optional `provider`) and no `name` or `voice` of its own; its pictures are
  `assets/stories/<story-id>.<cast-id>.for-man.png` and `.for-woman.png`.
- **An effect on a cast voice** (a weak comms link, static) cannot be expressed until #726 is
  decided. Describe it in `who` and record it on the page as a gap against #726.

## The draft page

A story under development lives in its own HTML file, `<scratchpad>/<story-id>.html`, published as
its own artifact. The file is never in the repository. Its `<title>` is the story's title, and its
publish `description` starts `d47 stock story draft`, which is how a later session finds it: list
the artifacts with the Artifact tool's `list` action and read the one to continue with `read`,
writing what comes back to the scratchpad file before editing. Republish the file after every
change, so the page is never behind the draft. Load the `artifact-design` skill before the first
publish of a session, and match the look of the shared review page.

The draft page shows the card, the whole hidden layer, the three sample chapters marked as
samples, any gap found (below), and its state: draft or approved.

## The shared review page

The maintainer's page "d47 Stock Stories", https://claude.ai/artifact/HNGyX7PhikSQiMuhxxJP3g,
holds approved stories only. A draft never goes on it. When the maintainer approves a story, read
the page with `read`, add the story's section from the draft page with its state set to approved,
keep every story already there, and republish to the same `url`. On a publish conflict, merge the
story into the newer version the tool hands back. Then republish the draft page once more,
marked approved, with a link to the shared page.

## The steps

1. **Agree the premise, genre and tone** with the maintainer. Offer two or three concrete premises
   when he has not given one, each with its genre, tone and twist mechanism.
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
   whether the #711 long-haul threshold is reached and the comfort-zone pick.

   Name each beat by its kind and filter from the tables in rule 5's issues, with counts sized to
   that state, and keep the chapter rules the app enforces:
   - from chapter two on, at most two of five beats are `arrive`, `dock`, `land` or `scan` (#711);
   - a chapter fits one to three play sessions; a longer undertaking (engineering, saving for a
     ship then buying it, a run of ranks) continues across ordinary chapters (#711);
   - a beat that spends credits needs the price plus a reserve of the price again or 500,000,000,
     whichever is less, at the last load, so saving and buying fall in separate chapters (#735);
   - the finale stays within a session's flying unless the long-haul threshold in #711 is reached.
     A story that needs a long-haul finale is a gap against #728 until that is decided.

   Where a beat the story needs cannot be expressed that way, say so on the page and name the
   issue it would change. Do not invent a kind.
4. **Publish** both layers and the samples on the story's draft page.
5. **Revise** on his notes, republishing the draft page after each round, until he approves the
   story. Nothing goes on the shared review page or into the catalog before then.
6. **Move it to the shared review page**, as that section describes.
7. **Add it to the catalog.** Add the card to `src/D47.Core/Stories/StoryCatalog.json` with the next `number`,
   decode the sealed layer into the scratchpad, add the hidden entry with the same `id`, and
   encode it:

   ```bash
   python tools/seal-stories.py decode <scratchpad>/sealed.json
   python tools/seal-stories.py encode <scratchpad>/sealed.json
   ```

   Sample chapters never go in the catalog. The app writes chapters when they are due.
8. **Delete the scratch files**: the drafts, the draft page's HTML file and the decoded layer.
9. **Build and test**, once the catalog files have changed:

   ```bash
   dotnet build d47.slnx -c Debug
   dotnet test tests/D47.Core.Tests --filter "FullyQualifiedName~Story|FullyQualifiedName~Adventure"
   dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~Gate
   ```

   The build must have 0 warnings. Add a `CHANGELOG.md` entry under the current unreleased heading
   naming the story by its title, and mark the story "in the catalog" on the shared review page.
10. **Commit only when the maintainer says so.** The commit holds the two catalog files, the
   cast pictures in `assets/stories/` and the changelog, with a subject such as `Add the stock story <title>`. Do not push.

Nothing else in `src/` changes. A story that seems to need a code change is a gap: record it on the
draft page and name the issue (step 3).

## Finishing

A turn that hands the story back for review, or lands it, ends with the `/claude-voice` sentence
as its last tool call, then the written report: the draft page link (and the shared page link once the story
is approved), what changed this round, and any gap found.
