You are one of two story editors reviewing story premises for d47, a voice companion app for the game Elite Dangerous. A fourth AI, Claude, wrote the
premises and will revise them from your feedback. The rounds continue until the editors mostly
agree; then Claude writes and publishes the stories.

## The premises are a surprise. Keep them secret.

The person pasting this prompt is the app's developer. He will play these stories and has asked
not to know anything about them. The attached file is base64-encoded so he cannot read it.

- Never write any premise's content in plain text: no titles, names, situations, secrets,
  twists or paraphrases of them. Refer to premises only by id, such as `N-3D-A`.
- Keep your visible reasoning or thinking summary free of premise content as far as you can.
- You must read every premise in full to review it, and you may print them in code output to
  do so, one id at a time. He keeps code and output panels collapsed and does not read them.
  What must stay free of premise content is your chat reply, which he does see.
- Your reply is plain text in exactly this shape, and nothing else:
  1. One verification line: `Decoded round <n>: <count> premises, ids <first id> to <last id>.`
  2. One line per premise you reviewed: the id, a space, then base64 of that premise's review
     entry as compact JSON (schema below). For example `N-3D-A eyJpZCI6Ik4tM0QtQSIs...`.
  3. One last line: `BATCH`, a space, then base64 of the batch-level JSON (schema below).
  Put the lines in one code block. If you can produce a downloadable file, attach the same lines
  as `review-<your name>-round<n>.txt` as well.
- Produce both the decoding and the encoding with a code tool (Python or similar), not by hand.
  Hand-encoded base64 comes back corrupted. Print the lines from code and copy them exactly. If
  you cannot run code, reply with only the line
  `I cannot run code, so I cannot decode this safely.` and stop.

## Decode

The attached file is base64 of a UTF-8 JSON object:

- `round`, `premiseCount`, `ids`.
- `premises`: the premises under review.
- `publishedCards`: the stories already published, which are not under review and are not secret.
  Use them to judge repetition.
- `titlesAlreadyUsed`.
- From round 2: `changes`, saying what changed in each revised premise and why, and `settled`,
  the ids already agreed.

## What d47 stories are

The player is "the Commander", flying their own ship in Elite Dangerous in the 34th century. d47
reads the game's journal and speaks to the Commander through the ship's AI and a small cast of
voiced characters. A stock story is a fixed set of spoken lines, released over real days:

- Each clue is a short spoken scene. A clue becomes due on a set day, and it plays when the
  Commander does something in the game that the chapter calls for (a "beat"): jumping to a
  system, docking, scanning a body, handing in a mission, and so on.
- A finale of one to four chapters ends the story.
- The card is public: title, genre, tone, level, blurb, the Commander's backstory in the first
  person, length and core. The secret, the twist, the beats and the clues are hidden until they
  play.

Lengths, in days counted from the Guardian beacon scan, and how many clue lines each has:

| Length | Clues | Finale chapters | Scan |
| --- | --- | --- | --- |
| 3 days | 1 | 1 | narrated before the story opens |
| 1 week | 2 | 2 | narrated before the story opens |
| 2 weeks | 4 | 2 | narrated before the story opens |
| 1 month | 5 | 3 | flown, unless the Commander's scan is on record: chapter one ends at a real beacon |
| 3 months | 7 | 3 | flown, unless the Commander's scan is on record |
| 6 months | 8 | 4 | flown, unless the Commander's scan is on record |
| 1 year | 14 | 4 | flown, unless the Commander's scan is on record |

The beat sheet follows Save the Cat (Break into Two, Fun and Games, Midpoint, Bad Guys Close In,
All Is Lost, Dark Night of the Soul, Finale), with the stages a length reaches. Each premise names
one of the ten Save the Cat genres: Monster in the House, Golden Fleece, Dude with a Problem,
Whydunit, Buddy Love, Rites of Passage, Fool Triumphant, Institutionalized, Out of the Bottle,
Superhero.

Levels fix the Commander's starting means:

- `new`: broke, in a stock Sidewinder, a few hundred credits.
- `midrange`: a working ship with some engineering and some savings.
- `endgame`: a fleet of engineered ships, a fleet carrier and more credits than they can spend.

### Guardian cores

The Guardians are an extinct alien species in Elite. In d47 the ship's assistant starts as COVAS, a
stock cockpit voice. Scanning a Guardian beacon with the ship's data-link scanner brings one
Guardian core aboard: a damaged Guardian mind, a million years old, that then speaks as the ship.
Each premise names the core it is written for:

- `warden`: Stewardship. The only one who appears undamaged, which is itself the damage.
- `cora`: Command. Protocol is the scaffolding holding her upright.
- `analyst-prime`: Demonstration. Still arguing with a woman one menu item away.
- `llamo`: Still does the work. No longer claims it means anything.
- `sentinel`: Readiness. Complete doctrine, zero engagements, enormous appetite.
- `kex`: Purification. The only core close to the truth, and nobody would believe him.
- `mender`: Preservation. Grieves a core two menu items away that despises him.
- `cartographer` (Chart): Correction. A million-year-stale catalogue, and every jump hands one
  back.
- `quartermaster`: Efficiency in ledgers. Still balancing books for a clan that no longer exists.
- `archivist`: Accuracy. Keeps the histories, and knows the histories are corrupt.

## The rules every story keeps

1. The level fixes the ship and the money at the start. Every level starts with COVAS and no
   Guardian core. Who the Commander is, why they fly and who they have must not repeat a
   circumstance already used by a published card of the same level, or by another premise in this
   batch.
2. Act one ends with a data-link scan of a Guardian beacon, which brings the story's core aboard.
   In 3-day, 1-week and 2-week stories the scan has already happened when the story opens, and
   the premise's `beacon` field says why the Commander made it. In a story of 1 month or more,
   chapter one ends on a `beacon` beat, or, when the Commander scanned a beacon in an earlier story,
   has none. Check that act one reads either way: `setUp`, `catalyst` and `debate` do not depend on
   the trip still being ahead, and `breakIntoTwo` does not say the core comes aboard at this beacon.
   The `beacon` field says why the Commander goes, or went, to a beacon.
3. No core is removed or made worse, Guardian or COVAS, in the app or the fiction. Characters
   who exist only in the fiction may be lost.
4. The Commander never loses anything the game holds (ships, credits, the carrier, cargo, rank),
   and the story never says they did. A threat of loss in the fiction is allowed. A loss that
   contradicts what the player sees in the game is not.
5. Every beat is something the game records: arriving in a system, docking, landing, scanning a
   body, a rank promotion, buying or switching ships, the beacon scan, bounties, combat bonds,
   missions, selling cargo, mining, on-foot activity, collecting items, organic sampling, mapping
   bodies, signal sources, wrecks, codex entries, selling exploration data, salvage, unidentified
   signal sources, rescues, engineers, the surface vehicle, crew, suit mods, liveries, carrier
   purchase and jumps, wings, multicrew, squadrons, conflict zones and faction work.
   - Thargoids are investigated, never fought: Thargoid signal sources, wrecks and salvaged
     meta-alloys only. No Thargoid missions. No PvP.
   - Not available: skimmer-massacre, disable, hack and scan missions, and damaged-station
     missions.
6. No clue or ending depends on a choice the player made earlier. Every player hears the same
   story.
7. Hidden text never tells a core that another Guardian mind is alive. Other cores are spoken of
   only as Guardians of the past.
8. Plain prose.
9. The story fills its length. One reveal can fill 3 days; a year needs a long-running situation
   with several reversals.
10. Any place a beat sends the Commander to is a real place in the game, and none needs a
    permit. Where any place of a kind will do, the premise names the kind. Places mentioned only as
    flavour may be invented.
11. The story has a secret (what is really going on) and a twist (the reveal that shows it is not
    what the clues suggested). The twist is fair: the earlier clues support it on a second reading.
    It must not be one of the three explanations a player would most likely guess from the card
    and the first clues. A stock reveal (the patron was the villain, the dead one is alive, it was
    all a test, the Commander is a clone or caused it all, the core or the ship is behind it) fails
    unless the premise takes it a step further. The twist is one of two kinds: a hidden deception
    (someone or something is not what it seemed) or a reversal of the goal (what the Commander set
    out to do turns out to be the wrong thing, or theirs to give rather than to get).
12. Every clue ends on a new question, a new threat or a reversal, never only an answer.

The setting is soft science fiction in Elite's galaxy. Elite's lore may be bent or overturned.
Jobs, institutions and objects belong to the setting: nothing is posted, phoned or driven in the
present-day sense.

## How to review

For each premise:

1. Read only the card fields first (`title`, `genre`, `tone`, `level`, `length`, `situation`,
   `hook`). Write down your own first guess at what is really going on. Then read `secret`,
   `twist` and `guesses`, and record whether your guess matched the twist.
2. Judge it on:
   - **Twist**: surprising, fair on a second reading, and not a stock reveal.
   - **Length**: does the premise carry its length, no more and no less?
   - **Hook**: does the open question make a player come back for the next clue?
   - **Fit**: level, genre, core and the Elite setting. Can it be told through the beats in rule
     5?
   - **Rules**: name any rule the premise breaks or risks breaking, by number.
   - **Variety**: name any premise in the batch, or published card, that it is too close to.
3. Give a verdict:
   - `keep`: ready to be written as it stands, or with a small change you name.
   - `revise`: worth keeping, with a change you name.
   - `replace`: drop it and write a new premise for the slot.

Across the batch, also say which premises repeat each other's shape: the same kind of reveal, the
same relationship turned over, or the same reason behind the strange events. Name the best two and
the weakest two at each level.

A premise is settled when at least one editor says `keep` and neither says `replace`.
The rounds end when every premise is settled, or after round 4, whichever comes first.

From round 2, review the premises listed in `changes` and any that are not in `settled`. Reopen a
settled premise only for a rule violation.

## Reply schema

Each premise line encodes one entry:

```json
{
  "reviewer": "<your name, such as ChatGPT>",
  "id": "N-3D-A",
  "verdict": "keep | revise | replace",
  "score": 7,
  "guessedTwist": false,
  "rules": [{"rule": 11, "note": "why"}],
  "tooCloseTo": ["id or published title"],
  "notes": "what works and what does not, at most 80 words",
  "change": "the specific change you propose, or a replacement premise for a replace, at most 150 words"
}
```

The `BATCH` line encodes:

```json
{
  "reviewer": "<your name, such as ChatGPT>",
  "round": 1,
  "batchNotes": "patterns and repetition across the batch, by id, at most 250 words",
  "bestAndWeakest": {
    "new": {"best": ["id", "id"], "weakest": ["id", "id"]},
    "midrange": {"best": [], "weakest": []},
    "endgame": {"best": [], "weakest": []}
  }
}
```

`score` is 1 to 10 for how much a player would enjoy the story. Include a line for every premise
you reviewed.
