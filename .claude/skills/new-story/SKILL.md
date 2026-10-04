---
name: new-story
description: Develop one d47 stock story with the maintainer — a pitch of premise, secret and twist to accept or reject, then both layers — on a draft page of its own, revise it until he approves, then publish it to the stories release. Use when the user invokes /new-story, /new-story <premise or title>, or says "new story", "write a stock story", "let's work on the next story", "continue the story".
---

# New story

You develop one stock story with the maintainer, from premise to a story published to the stories release.
He reads both layers, the card and the hidden layer, before anything ships. Nothing is hidden from
him; the sealing keeps the hidden layer out of the tree's plain text, not out of his sight.

`/new-story <anything>` has named the work, so start on it in that same turn: a pitch (below),
built from whatever the argument fixes. A bare `/new-story` looks for draft pages first (below): if
there are any, ask whether to continue one or start another; if there are none, pitch one.

## Turn the voice on first

The first step of the working turn is `/claude-voice New story`. `/claude-voice off` stops it and the
work carries on unchanged.

## The rules

Every story keeps all twelve. Check the draft against each before showing it.

1. The card names its level, `new`, `midrange` or `endgame` (#739), and the backstory starts there:
   broke in a stock Sidewinder for `new`, a working ship and some savings for `midrange`, a fleet
   and a fleet carrier for `endgame`. Every level starts with the `covas` core and no Guardian
   cores. The ship and the money are fixed by level. Who the Commander is, why they fly and who they
   have are chosen like the genre: the pitch avoids circumstances that a published card of the same
   level already uses.
2. Act one ends with a data-link scan of a Guardian beacon. The scan brings the story's Guardian
   core aboard (#716), and the `breakIntoTwo` beat says so without naming the core. Every story
   names its own core on the card, chosen to match the story. From 1 month up, its first chapter
   ends with a `beacon` beat for the scan. In a story of 3 days, 1 week or 2 weeks the scan happens
   before the story opens and is narrated (#758): the hidden entry has a `scan` line, and the first
   chapter has no `beacon` beat.
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
   beat or clue uses them.
6. No clue or ending depends on an earlier choice.
7. Hidden text never tells a core that another Guardian mind is alive. Other cores are spoken of
   only as Guardians of the past.
8. Plain prose, and never "wants" where "needs" is meant.
9. The story fills its length, and the beat sheet follows that length's stages in `StoryPacing`.
10. A place the Commander has to go to is a real one, named as the game names it: every system,
    station, settlement, body, beacon site or wreck a beat or a clue sends them to. Check each name
    on spansh.co.uk, the source d47's `AdventureResolver` looks it up in; a name it does not know
    cannot fire. Where any place of a kind will do, name the kind instead of a place. A place
    mentioned only as flavour, which no beat or clue sends them to, may be invented.
11. The story has a secret and a twist, and the twist is not predictable. The secret is what is
    really going on. The twist is the reveal that shows it is not what the clues so far suggested.
    The twist is fair: the earlier clues support it on a second reading. To test it, before showing
    the draft, write down the three explanations a player would most likely guess from the card and
    the first few clues; the twist must be none of them. Stock reveals fail this test unless the
    story takes them a step further: the patron was the villain, the Commander is a clone or caused
    it all, the core or the ship is behind it, it was all a test, the dead one is alive.

    The twist is one of two kinds. A hidden deception: someone or something is not what it seemed.
    A reversal of the goal: what the Commander set out to do turns out to be the wrong thing, or to
    be theirs to give rather than to get. The pitch names which kind, and favours the reversal when
    the closest published stories (see The pitch) are deceptions.
12. Every clue ends on a new question, a new threat or a reversal, never only an answer. Finale
    lines and `end` are exempt.

Within those rules: the setting is soft science fiction in Elite's galaxy of the 34th century, and
Elite's lore and d47's own may be bent or overturned. Jobs, institutions, objects and slang belong
to that setting: a registry clerk works at a starport, a reporter files to a newsfeed, and nothing
is posted, phoned or driven. In a Buddy Love story the partner exists only in the fiction.

Write the story in this session. Do not hand a layer to a subagent to write.

## Titles already used

New titles must not repeat these, or any title in the published `index.json`:

```bash
gh release download stories-1 --pattern index.json --output -
```

If the release has no `index.json` yet, only the list below applies.

- The Marker, All Expenses Paid, The Counting Rhyme, Stock Class, Postdated, Late Start, Dead Drop,
  Long Distance, Form 47-B, The Countdown, Field Notes, Deputy, The Last Movement, Time Served,
  Page Nineteen, Unpublished, The Pilgrim, Accidental, Correspondence Game, The Strike Fund.
- Factory Settings, The Lifeboat, Episode 47, The Quiet Watchers, The Trial, The Wreck With My
  Name, The Salome Tape, The Claim, The Toast, The Glyph, The Dreamer, The Misjump, The Blackout,
  The Job, The Claimant, The Catalogue, The Grey Ghost, Terminal, The Long Sleep, The Passenger.
- Understudy, The Stranger's Kindness, Silent Wing.

## The pitch

The first thing he sees is one pitch, in chat, before any layer or draft page is written.

The maintainer may fix any of the length, level, genre, tone, core or premise, in the argument or in
reply. Keep what he fixed and choose the rest. Where you choose, favour what the catalog lacks:
count the cards in the published `index.json` by genre, by level and length together, by core, and by
the opening word of the tone, and favour the one used least. Also count the cards with a romantic lead
(a `castPictures` name ending `.for-man` or `.for-woman`), and favour a story led by its romance where
that count is low. Pick a gap unless the story fits something else better. Say in one line which gaps
the choice fills.

The genre is one of the ten Save the Cat genres in `StoryCard.Genres`.

The pitch gives:

- a table of the length, level, genre, tone and core, with the core's tagline;
- the premise: the situation at the pick, and why it fills its length;
- the stakes: who or what is lost if the Commander fails, in one sentence;
- the hook: the question a player needs answered enough to come back for the next clue;
- the secret and the twist in one sentence, then a short paragraph on each, and the twist's kind
  (rule 11);
- the three explanations a player would most likely guess (rule 11), and why the twist is none of
  them, with the clues that support it on a second reading;
- the three closest published stories, named from the `index.json` blurbs and tones, with one line
  each on what this story does differently. A pitch that differs from one of them only in setting or
  cast is pitched again;
- a title not already used.

Check the pitch against all twelve rules before showing it. The pitch fixes the premise, secret and
twist; once he accepts it, write the rest without asking again, and bring back only a choice the
pitch did not settle.

## The length

The length is one of the seven in `StoryPacing.All` (`src/D47.Core/Stories/StoryPacing.cs`),
3 days, 1 week, 2 weeks, 1 month, 3 months, 6 months or 1 year. The maintainer may name it; otherwise
the pitch chooses it. Nothing shorter than 3 days is developed; that is an adventure.

`StoryPacing` gives each length's key, clue days, finale chapters, stages and `BeatKeys`. Read the
counts and beat keys from there; this skill keeps no copy.

The premise has to fill its length. Say what keeps the story going that long: one reveal can fill
3 days, and a year needs a long-running situation with several reversals.

## The format

Read the format from `StoryCard` and `StorySecret` in `src/D47.Core/Stories/StoryCatalog.cs` and
from `tools/seal-stories.py`. They win over this list, which also names fields that open issues
add.

- **The card**, `card.json`: `id`, `number`, `title`, `genre` (one of the ten Save the
  Cat genres), `tone`, `level` (#739), `blurb`, `inYourWords` (the Commander's
  backstory in the first person, starting at the level), `length` (the key of a `StoryPacing`), `beacon` (why they go to scan it; for a story shorter
  than 1 month, the scan the story opens after) and `core` (#716: the Guardian core the story is written for,
  never `covas` or `heretic`). Until #739 and #716 land, show `level` and `core` on the page and
  leave them out of the card.
- **The tone** is a feel word or two, then the kind of story, for example "dark, haunted mystery" or
  "desperate survival thriller". Voice words (wry, warm, dry, quiet, cosy) may follow but never open
  it. It does not chain "then turns" clauses.
- **The blurb** is four parts, in order: the situation that hooks, how it escalates, what is lost if
  the Commander fails, and the question left open. The loss is in the fiction (an NPC, a love, a
  colony, a war, the truth), within rules 3 and 4. It uses no praise words such as brilliant,
  masterful, stunning, unforgettable or original. A primary cast member with one name is
  still named in the `blurb` or `inYourWords`, which `tools/seal-stories.py` checks.
- **The hidden entry**, `hidden.json`: `id`, `secret`, `beats` (one line for each key in the length's
  `StoryPacing.BeatKeys`), `clues` and `finale` lines in the counts the length's `StoryPacing`
  gives (`ClueDays.Count` clues, `FinaleChapters` finale lines), a `scan` line for a story of 3 days,
  1 week or 2 weeks (#758) and none for a longer one, `end`, 1 to 4 `options`, each
  with `id`, `label`, `after` and `add` (persona ids, possibly none), and `cast`.
- **Lines** (clues, finale and `scan`) each have `text` and `speaker`: `ship`, `narrator` or a cast `id`.
- **The cast**: each member has `id`, `name`, `who`, `provider` (`kokoro`, or `chatterbox` only
  when the story needs it, #41) and `voice`. Voice `own` is the Commander's recording (#713).
  `primary: true` (#717) marks a recurring character named in the card's `blurb` or
  `inYourWords`, never one with voice `own` (#737). Every story has at least one primary member,
  and every cast member gets a picture, `<story-id>.<cast-id>.png`, a square
  PNG. The page gives an image prompt for each named NPC, matching the style of the pictures
  already shipped. Prompts set the person in the 34th century: flight suits, station corridors,
  cockpits and ship interiors, never present-day offices, clothes or devices. A member with voice
  `own`, or a speaker with no face such as a broadcast or a recorder, gets no prompt. Generate the pictures from these prompts (Cast pictures, below). The
  Commander can change any member's voice and picture later (#737), so the pinned voice is a
  default, not a constraint on the text.
- **A romantic lead** has two versions, keyed `forMan` and `forWoman` by the Commander who sees them (#746):
  one gender-neutral name for both ("Alex") or a name for each ("Ellis" and "Ellie"), and a
  default voice and a picture prompt for each. The card never names the lead and calls it by
  role ("the engineer"); hidden text names it with `{name:<cast-id>}` and gives it no pronoun. In the
  sealed entry the member carries `versions` (`forMan`, `forWoman`, each `name`, `voice` and an
  optional `provider`) and no `name` or `voice` of its own; its pictures are
  `<story-id>.<cast-id>.for-man.png` and `.for-woman.png`.
- **An effect on a cast voice** (a weak comms link, static) cannot be expressed until #726 is
  decided. Describe it in `who` and record it on the page as a gap against #726.

## The draft page

A story under development lives in its own HTML file, `<scratchpad>/<story-id>.html`, published as
its own artifact. The file is never in the repository. Its `<title>` is the story's title, and its
publish `description` starts `d47 stock story draft`, which is how a later session finds it: list
the artifacts with the Artifact tool's `list` action and read the one to continue with `read`,
writing what comes back to the scratchpad file before editing. Republish the file after every
change, so the page is never behind the draft. Load the `artifact-design` skill before the first
publish of a session.

The draft page shows the card with its length, the whole hidden layer, any gap found (below), and its
state: draft, approved or published.

Directly above the `secret` paragraph, the page gives one sentence stating the secret and the
twist, so the maintainer can judge both without reading the paragraph. The sentence is a page aid
only and does not go in the sealed entry. Rewrite it whenever the secret changes.

## Cast pictures

`tools/story-image.py` sends a prompt to the OpenAI Images API with the OpenAI key d47 already
holds (`openai.apiKey`, decrypted in memory from the installed app's `secrets.json`, then
`dev-install`'s). Never print, copy or write out the key. Each call is billed to that key.

```bash
python tools/story-image.py --file <scratchpad>/<story-id>.<cast-id>.txt --name <story-id>.<cast-id> --out <scratchpad>/images
```

- One picture per member, never more: the script's default of one, written as
  `<story-id>.<cast-id>-1.png`, square, at the default `high` quality and the default model
  `gpt-image-2.5-flare`. `--quality low` is for trying a prompt; `--model` and `--list-models`
  pick another model. A romantic lead gets one for each version, named `.for-man` and
  `.for-woman`.
- Generate once the cast is settled, not every round. Regenerate only the members whose prompt
  changed or whose picture the maintainer turned down, one picture each time.
- Show each picture on the draft page under its member's prompt, embedded as a data URI, and look
  at it before showing it: a picture with text, a logo, a present-day setting or the wrong person
  is regenerated, not shown.
- At step 6 each member's picture is copied into the story folder as `<story-id>.<cast-id>.png`.

## The steps

1. **Pitch the story** as the pitch section says, and stop for his accept or reject. Write nothing
   else until he accepts it; on a reject, pitch again with his notes.
2. **Write both layers** to `card.json` and `hidden.json` in the story folder,
   `<scratchpad>/<story-id>/`, outside the repository. Never write hidden text into the tree:
   `NoSealedTextAppearsInTheTreeTests` fails on any hidden sentence of 24 characters or more in a
   repository file.
3. **Check for gaps.** Where the story needs something the format or rule 5's beat kinds cannot
   express, record it on the draft page as a gap and name the issue it would change. Do not
   invent a kind. A finale that needs long-haul flying is a gap against #728 until that is
   decided.
4. **Publish** both layers on the story's draft page. Once the cast is settled,
   generate the cast pictures and add them to it.
5. **Revise** on his notes, republishing the draft page after each round, until he approves the
   story. Nothing is published to the release before then.
6. **Publish on his word.** Copy each member's picture into the story folder, then run

   ```bash
   python tools/publish-story.py <scratchpad>/<story-id> --dry-run
   python tools/publish-story.py <scratchpad>/<story-id>
   ```

   The dry run checks the card, the hidden entry and the pictures and lists the files; a fault is
   printed by id and field and nothing is uploaded. The second command is a GitHub write: run it
   only after he says to publish. Then republish the draft page marked published and delete the
   scratch files: the story folder, the draft page's HTML file and the generated images.

Publishing changes nothing in the checkout. A story that seems to need a code change is a gap:
record it on the draft page and name the issue (step 3).

## Finishing

A turn that hands the story back for review, or publishes it, ends with the `/claude-voice` sentence
as its last tool call, then the written report: the draft page link, what changed this round, and
any gap found.
