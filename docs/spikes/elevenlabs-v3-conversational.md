# ElevenLabs v3 Conversational, measured against the pinned Flash 2.5

**Run 2026-09-04, from C#, against the live service on the Commander's own account.** The three
questions [#291](https://github.com/dseelinger/d47/issues/291) says the documentation does not
settle, plus the one it says is worth an ear.

Measured with `spike/ElevenLabsModelProbe`, which builds each request the way
`ElevenLabsTtsProvider.SynthesizeAsync` builds one — same URL, same `output_format=pcm_24000`, same
body shape, text through the same `SpokenNumbers.Expand` — and varies only `model_id`,
`language_code` and `voice_settings.speed`.

**In one line: `language_code` is accepted by both models on every line, the speed control is
accepted by v3 and does nothing at all, and the round trip is not 280 ms against 75 ms but
2.06 s against 0.31 s on a line d47 says every jump.**

**And the finding nobody was looking for: audio tags do not survive being split into sentences
unless every sentence carries one, restating a tag inside one generation defeats it, and at d47's
sentence lengths a tag is a probability rather than a guarantee** (§10).

---

## 1. The model exists, and the service's own words differ from the pricing page's

`GET /v1/models`, nine models listed, three of them relevant:

| `model_id` | What ElevenLabs says | Languages | Max characters | Speaker boost |
|---|---|---|---|---|
| `eleven_flash_v2_5` | "Our ultra low latency model in 32 languages. Ideal for conversational use cases." | 32 | 40,000 | no |
| `eleven_v3_conversational` | "The most expressive model, optimized for natural dialogue in conversational agents. … **Requires more prompt engineering than our previous models.**" | 74 | 5,000 | yes |
| `eleven_v3` | "The most expressive model. … Requires more prompt engineering than our previous models." | 74 | 5,000 | no |

Both v3 entries carry `en` in `languages`, and both advertise the same 74. The 5,000-character cap
is an eighth of Flash's and is far above anything d47 sends, so it is a fact rather than a limit.

**"Requires more prompt engineering than our previous models" is the service's own warning and it is
not on the pricing page.** It is the sentence a persona author would need to have read.

## 2. `language_code` is accepted by both, on every line

Every combination returned **200**: both models × four lines × `en`, `de` and no pin at all. There is
no repeat of Multilingual 2's outright rejection of the parameter — nothing here refuses it.

| Line | What it is for |
|---|---|
| `short` | "Contact on the scanner." — the shape of most of what d47 says |
| `system` | "Route plotted to Hyades Sector DB-X d1-112, 4 jumps, 88 tonnes of tritium aboard." — numerals and a system name, the line `SpokenNumbers` rewrites |
| `german` | "Commander Bergmann says: Achtung, Kopfgeldjager im Ring. Steht das noch, Commander?" — the exact input that disqualified Multilingual 2 |
| `tags` | "[whispers] Contact on the scanner. [sighs] It is a Federal Corvette, and it has seen us." — the reason to look at v3 at all |

**Accepted is not held, and no status code can tell the two apart.** So the probe writes 24 WAVs,
and the `de` rendition is the control that makes the listen an A/B rather than a judgement about a
single recording that already sounds English because the words are: a model honouring the pin says
an English sentence in a German accent and the pair is unmistakable, a model ignoring it hands back
the same reading twice.

> **Open, pending an ear.** `eleven_v3_conversational-short-en.wav` against
> `-short-de.wav` settles whether the parameter does anything; `-german-en.wav` settles whether the
> pin survives the sentence that broke the last model.

## 3. Speed: accepted by v3, and ignored

The finding, and the one that was not on the issue's list of risks. Each value is the median of
three syntheses of the `system` line, in seconds of returned audio:

| `voice_settings.speed` | Flash 2.5 | v3 Conversational |
|---|---|---|
| 0.5 | **400** — "expected to be greater or equal to 0.7 and less or equal to 1.2" | 8.64 s (8.48–8.72) |
| 0.6 | **400** | 8.96 s (8.64–9.28) |
| 0.7 | 9.43 s (9.15–9.89) | 8.64 s (8.16–8.64) |
| 0.8 | 8.08 s (7.94–8.31) | 8.64 s (8.48–8.88) |
| 0.9 | 7.48 s (7.11–7.57) | 8.48 s (8.40–8.72) |
| 1.0 | 6.64 s (6.22–6.69) | 8.48 s (8.08–9.36) |
| 1.1 | 6.22 s (6.22–6.55) | 8.40 s (7.92–8.56) |
| 1.2 | 5.25 s (5.15–5.25) | 8.96 s (8.56–9.04) |
| 1.3 | **400** | 8.64 s (8.32–9.12) |
| 1.5 | **400** | 8.64 s (8.40–8.80) |
| 2.0 | **400** | 8.80 s (8.00–8.80) |

**Flash moves monotonically and by a factor of 1.80 across its published range.** Every value outside
0.7–1.2 is refused with a message naming the range, which is the behaviour `SpeedFor`'s clamp is
written around and it is confirmed exactly.

**v3 accepts 0.5 and 2.0 — a four-fold span — and returns the same eight-and-a-half seconds
throughout.** The spread within one setting (8.08–9.36 s at 1.0) is wider than the spread across all
eleven settings, which is what "this number is not read" looks like. This is the same failure
Cartesia's speed control showed in
[cartesia-voices-and-speed.md](cartesia-voices-and-speed.md): accepted and ignored.

So it is not that v3 has *a different range* the rate row could narrow to, as the issue anticipated.
It has **no rate control**, and a row offered against it would be inert — a control a Commander moves
that changes nothing, with no error to explain why.

## 4. Round trip: 2.06 s against 0.31 s, not 280 ms against 75 ms

Five calls per condition, median with the full spread, on the same account and connection minutes
apart:

| Line | Characters | Flash 2.5 | v3 Conversational | Ratio |
|---|---|---|---|---|
| `short` | 23 | **165 ms** (151–193) | **600 ms** (526–910) | 3.6× |
| `system` | 81 | **313 ms** (296–350) | **2,060 ms** (1,950–2,233) | 6.6× |
| `german` | 83 | **285 ms** (264–571) | **1,724 ms** (1,602–1,762) | 6.0× |

**The published 75 ms and 280 ms are time to first byte for a caller that streams. d47 does not
stream** — `SynthesizeAsync` reads the whole body before the arbiter is handed a clip — so the delay
a Commander experiences is the whole round trip, and it grows with the length of the line rather
than sitting at a constant. The gap the issue weighed as 205 ms is, on the sentence d47 says on
every jump, **1.7 seconds**.

Two further facts from the same runs: v3 returns **more audio for the same words** (9.36 s against
6.36 s on `system`, 47% longer), and the ratio worsens with length, so a persona's longer replies
pay the most.

## 5. What v3 can do that Flash cannot — eight comparisons, open pending an ear

`--only compare` writes eight files, one per claimed difference. Each holds **v3 then Flash**, and
each read is introduced by that model saying which difference is being listened for and which model
it is — *"Whispering. This is the new V 3."* Both halves in one file rather than two, because the
question is a difference and a difference is heard in the seam.

The label is a separate request from the read. A tag at the head of a v3 generation colours
everything after it, so a label sharing the line would be part of the performance being judged.

| | Difference | Line | v3 | Flash |
|---|---|---|---|---|
| 1 | Whispering | `[whispers] Cutting the drives. There is something in the next ring…` | 6.32 s | 4.88 s |
| 2 | A weary sigh | `[sighs] That is the third interdiction this hour, Commander.` | 3.84 s | 3.20 s |
| 3 | Excitement | `[excited] Double painite hotspot, dead ahead…` | 5.20 s | 4.46 s |
| 4 | Shouting under pressure | `[shouting] Heat sink, now! Hull at 14 percent!` | 3.36 s | 3.20 s |
| 5 | Sarcasm | `[sarcastic] Beautiful landing, Commander. The pad will buff out.` | 3.44 s | 3.72 s |
| 6 | Laughter | `[laughs] The entire bounty is 812 credits.` | 4.08 s | 3.53 s |
| 7 | Urgency with no tag to tell it to | `We just lost the starboard thruster. Get us to the station.` | 3.20 s | 3.39 s |
| 8 | Emphasis on a capitalised word, no tag | `That is a Federal CORVETTE, not a Viper.` | 4.96 s | 4.13 s |

**Flash is sent the brackets unchanged rather than stripped**, so what it does with them is part of
the comparison rather than something taken on trust — a model that cannot read a tag should say the
word out loud, and the lengths do not distinguish "performed" from "spoken aloud".

The last two carry no tag at all, on purpose. A model that is expressive only when told to is a
different proposition from one that reads the sentence, and only the second changes anything for
d47's callouts, which nothing writes tags into today.

> **Open, pending an ear.** Cartesia (Phase 60) worked and did not impress, and the same test
> applies: the question is not whether the difference exists but whether it is worth 1.7 seconds.

## 6. The same six lines with no tags at all — the run that describes d47 today

`--only plain` repeats section 5 with the brackets stripped, and nothing else changed. **This is the
comparison that matters for the picker as scoped**, because nothing in d47 writes an audio tag: a
Commander who switched the row today would get the untagged column and no other part of it. Six
files rather than eight, since two of section 5's lines carried no tag to strip.

| | Line, tags removed | v3 | Flash | Tagged, from §5 |
|---|---|---|---|---|
| 1 | `Cutting the drives. There is something in the next ring…` | 5.68 s | 4.13 s | 6.32 / 4.88 |
| 2 | `That is the third interdiction this hour, Commander.` | 2.72 s | 2.65 s | 3.84 / 3.20 |
| 3 | `Double painite hotspot, dead ahead…` | 4.16 s | 3.76 s | 5.20 / 4.46 |
| 4 | `Heat sink, now! Hull at 14 percent!` | 3.92 s | 2.74 s | 3.36 / 3.20 |
| 5 | `Beautiful landing, Commander. The pad will buff out.` | 3.52 s | 2.93 s | 3.44 / 3.72 |
| 6 | `The entire bounty is 812 credits.` | 3.28 s | 3.30 s | 4.08 / 3.30 |

**Both models return less audio without the tags** — Flash by 0.6 s a line on average, v3 by 0.5 s —
which says the bracketed words cost time in both. What that time buys is the listen.

**The reads are labelled by model name from this run on**: *"No tags at all. This is the new v 3"*
against *"…the old Flash 2 point 5"*. The first set said "the old V 2", and
`eleven_multilingual_v2` is a real model in the same listing — the one disqualified in August for
reading a milestone half in German. A file one careless listen from being filed as evidence about
the wrong model is not a file worth keeping.

## 7. Read back: Flash says the tags out loud, v3 never does

`--only words` reads every clip back through the transcriber d47 already ships (tiny.en, from the
install) and prints its level. It exists because two failures look identical from a duration, and
neither is one an ear settles: **words missing**, and **words present but inaudible**.

**Flash 2.5 speaks the bracketed word as text. Every single time.** Transcribed, not inferred:

| Sent | Flash said | v3 said |
|---|---|---|
| `[whispers] Cutting the drives…` | "**Whispers,** cutting the drives…" | "Cutting the drives…" |
| `[sighs] That is the third interdiction…` | "**Sighs.** That is the third…" | "That is the third…" |
| `[excited] Double painite hotspot…` | "**Excited,** double painite hotspot…" | "Double painite hotspot…" |
| `[sarcastic] Beautiful landing…` | "**Sarcastic** beautiful landing…" | "Beautiful landing…" |
| `[laughs] The entire bounty…` | "**Laughs,** the entire bounty…" | "The entire bounty…" |
| `[shouting] Heat sink, now!` | "**Shouting** heat sink now…" | "Heat sink, now!" |

**This is what makes the strip mandatory rather than tidy.** A tag that reaches any provider but v3
is a word the Commander hears in the middle of a sentence, and four of the five providers are in
that position. Kokoro is worse than the rest: its `Phonemiser` lists `[` and `]` as trimmable
brackets, so it pronounces the contents as a word by construction.

**v3 never spoke a tag aloud — including one that does not exist.** `[thargoid]` was silently
ignored, and `[grumbles quietly]`, plausible but undocumented, produced *an actual grumble* — the
transcript reads "Mmm. Cutting the drives." So on v3 an invented tag is performed or dropped, not
leaked as text. That weakens the case for a strict whitelist and does not touch the case for the
strip.

### No content was ever lost, and here is what made it look like it was

Every clip transcribes to the full sentence, every WAV's header agrees with its file size (46 of
46), and a half-second energy envelope shows sound running the whole length of each file. What the
tagged take does differently is quieter and emptier:

| `Cutting the drives. It has not seen us.` | Peak | RMS | The gap between the two sentences |
|---|---|---|---|
| no tag | −2.1 dBFS | −20.6 dBFS | −53.8 dBFS — room tone |
| `[whispers]` | −7.2 dBFS | −24.3 dBFS | **−81.0 dBFS — true digital silence, half a second** |

So a whispered line is **5 dB down on peak** and puts a **half-second of absolute silence** inside
itself where the plain reading has quiet room tone. Nothing is missing; it is quieter, and it stops
dead in the middle. Worth knowing before a `[whispers]` is ever sent over a callout the Commander is
meant to act on.

## 8. The vocabulary a model may be told to write, and why measurement cannot fix it

**The list cannot be ElevenLabs' published one.** Their own guidance is that *"the voice you choose
and its training samples will affect tag effectiveness"*, so a documented tag can be silent on a
given voice — and **a silent tag is worse than a missing one**, because the spoken-line log then
records a delivery that never happened. A Commander reading `[befuddled]` against a line that was
not befuddled is being lied to by the one record that settles complaints.

`--only vocabulary` renders every candidate on the Commander's own voice, against a control of the
same neutral line rendered five times to find the spread with nothing asked for:

```
control x5: 2.88-3.28s   peak -3.2 to -0.0 dBFS   rms -20.3 to -19.1 dBFS
```

Sixteen candidates, two takes each: **every one landed outside that spread**, and only `[whispers]`
moved loudness far enough to be unmistakable (−11.0 and −17.6 dBFS peak against a −3.2 floor).

**That result proves less than it looks, and the reason is the important part.** `[thargoid]` — a
tag that does not exist — also lengthened the same line, by 0.7 s, while transcribing to exactly the
words that were sent. **A bracket costs time whether or not it is honoured**, so duration and level
cannot tell *performed* from *paused*, and no acoustic measurement available here can. The question
"does `[curious]` sound curious" is irreducibly an ear's.

So the spike stops at bounding the listen rather than pretending to settle it. `vocabulary/
audition.wav` is 1.8 minutes: one line, eighteen readings, each introduced by the tag it was asked
for, and **the first two are the reference points** — no tag, then `[thargoid]`, which is what "the
model did something, but not what was asked" sounds like. A candidate that is not clearly better
than the `[thargoid]` entry has not earned a place on a list a model is told it may write.

## 9. Six tags did nothing — and the audition that found them had a flaw

Heard on 2026-09-04, on Roger. **Ten of sixteen landed:** `[whispers]` `[sighs]` `[exhales]`
`[excited]` `[mischievously]` `[snorts]` `[laughs]` `[laughs harder]` `[sings]` `[shouting]`.
**Six did not:** `[sarcastic]` `[curious]` `[starts laughing]` `[wheezing]` `[crying]`
`[strong Scottish accent]` — alongside `[thargoid]`, as expected.

**The audition asked every tag to colour the same deliberately neutral line, and that is the wrong
instrument for half of these.** Sarcasm needs a proposition to contradict; curiosity needs something
unresolved. *"Contact on the scanner. It has not seen us yet."* offers neither, so a tag with
nothing to act on has no way to show that it landed. The neutrality that made the control fair made
the test meaningless for the interpretive tags.

**The second suspect is length, and it is ElevenLabs' own.** Their v3 prompting guide: *"very short
prompts are more likely to cause inconsistent outputs"*, and it encourages prompts **greater than
250 characters**. The audition line was 47.

> **This reaches far past these six.** `SentenceSplitter` exists to start speech at the first
> sentence boundary rather than at end of turn — the largest perceived-latency win d47 has
> (architecture.md §6) — so **every** synthesis d47 issues is one sentence, 23 to 83 characters in
> the measured samples. d47 operates exactly where v3 says its tags are least reliable, and it does
> so by design. Anything that fixes this trades against that latency win.

`--only context` asks each of the six twice: once on a short line written to give the tag something
to act on, once on the same situation told past 250 characters.

| Tag | Short | Long |
|---|---|---|
| `[sarcastic]` | 52 chars, 3.44 s | 321 chars, 20.00 s |
| `[curious]` | 81 chars, 5.12 s | 322 chars, 20.48 s |
| `[starts laughing]` | 62 chars, 5.12 s | 306 chars, 22.88 s |
| `[wheezing]` | 51 chars, 4.00 s | 319 chars, 16.72 s |
| `[crying]` | 53 chars, 5.04 s | 302 chars, 19.52 s |
| `[strong Scottish accent]` | 47 chars, 2.88 s | 317 chars, 18.56 s |

Three outcomes, and each means something different:

- **Both work** — the original line was simply the wrong instrument, and the tag goes on the list.
- **Only the long one works** — a length problem, which is d47's problem and not the tag's. It
  cannot be fixed without giving up the sentence-boundary latency win.
- **Neither works** — the voice is refusing, which is what ElevenLabs means by *"the voice needs to
  be similar enough to the desired delivery"*. `[strong Scottish accent]` is the set's control here:
  an accent needs no help from the sentence, so if it fails both ways the answer is Roger, not the
  writing.

## 10. Length is the gate, the tag fades, and that collides with the sentence splitter

**Heard on 2026-09-04: every long variant worked, including the accent.** So none of the six
failures was the voice refusing — Roger can do all of them. The control did its job and cleared the
voice; **the gate is length**. (§10 qualifies this: length raises the odds rather than guaranteeing
anything.)

**And the tag decays.** The accent held and then reverted to Roger's own voice partway through, at
*"We have the angle on it…"* — **character 187 of a 317-character passage**. So a tag colours
roughly the first 190 characters after it and then stops.

Put beside §9's finding, the two bracket d47 from both sides:

| | |
|---|---|
| Below ~250 characters | the tag may not land at all |
| Beyond ~190 characters from the tag | the tag has already faded |
| What d47 sends | **23–83 characters, one sentence per request** |

`SentenceSplitter` is not incidental here. It exists so speech starts at the first sentence boundary
rather than at end of turn, which is the largest perceived-latency win d47 has (architecture.md §6).
It guarantees the shape v3 handles worst.

### The four ways of saying the same 300 characters

`--only grouping`, with the accent as the instrument because it is the one tag that either is or is
not:

| Variant | Requests | First sound | All of it |
|---|---|---|---|
| **whole** — one generation, tagged once | 1 | 5,199 ms | 5,199 ms |
| **split-tagged** — four generations, each tagged | 4 | **1,513 ms** | 6,975 ms |
| **split-once** — four generations, only the first tagged | 4 | 1,447 ms | 5,999 ms |
| **whole-repeated** — one generation, tag restated at the halfway point | 1 | 5,588 ms | 5,588 ms |

### Heard on 2026-09-04, and it settles the architecture

| Variant | Verdict |
|---|---|
| **whole** | **held throughout** |
| **split-tagged** | **held — and grew more intense sentence by sentence** |
| **split-once** | only the first sentence was affected |
| **whole-repeated** | **no accent at all, or very little** |

**`split-tagged` works, so d47 keeps its sentence boundaries.** Repeating the tag on each short
sentence carries the delivery across a whole reply at 1,513 ms to first sound rather than 5,199 ms.
`GroupsSentencesUpTo` does not need reviving; the seam stays cut, and this page is the record of
why it was reconsidered and did not turn out to be needed.

Three rules fall out, and each is a thing the code has to know:

1. **A tag reaches its own generation and no further.** `split-once` proves it: the model writing
   one tag at the head of a four-sentence reply colours one sentence. Whatever is meant to carry has
   to appear in every sentence it applies to.
2. **Never put the same tag twice in one generation.** `whole-repeated` came back with *less* accent
   than tagging once — restating it does not refresh a fade, it defeats the tag.
3. **Intensity scales inversely with length.** The same tag on a 48-character sentence hits harder
   than on a 342-character passage, which is what "grew more intense" is: four short generations,
   each landing the tag proportionally harder than the long one did. So mechanically repeating a tag
   down a reply escalates it. **This is the argument for the model choosing which sentences carry a
   tag, rather than d47 propagating one down the reply** — the code cannot know whether a rising
   delivery is what was wanted.

### One more thing this run corrected

The `whole` variant held the accent all the way through **342 characters**, where §9's 317-character
passage faded at 187. Same tag, same voice, different sample. So **the fade is real but not
reliable** — and by the same token, so is the landing. `[strong Scottish accent]` failed on a
47-character line in §8's audition and worked on a 48-character sentence here.

That is ElevenLabs' own sentence turned into a measurement: *"very short prompts are more likely to
cause **inconsistent** outputs."* Inconsistent, not absent. **At d47's lengths a tag is a
probability, not a guarantee**, which means the ten-of-sixteen result in §9 is the shape of the
distribution rather than a hard list — and it means the spoken-line log must record that a tag was
**asked for**, never that it was performed. The log is what settles complaints; it cannot claim
something the service does not promise.

## 11. Are the tags billed? Not settled, and it does not need to be

`--only billing` reads the account's `character_count` before and after one tagged synthesis and
one bare one. **Both reads came back 267,096 — the meter did not move for either call**, so
`/v1/user/subscription` does not update per request on this account and the measurement is
inconclusive. It is recorded rather than dropped so nobody re-runs it expecting an answer.

It does not need to be settled. **d47's ledger counts what d47 puts on the wire**, so injected tags
are counted whether or not ElevenLabs meters them; the ledger is then either exact or a slight
over-estimate, and an over-estimate of spend is the safe direction. The tags in the sample above are
worth 19 characters against 39 of speech — roughly a third again on a short line, which is the
figure to keep in mind rather than a per-tag price.

## 12. Expressiveness — the earlier pair

`eleven_v3_conversational-tags-en.wav` against `eleven_flash_v2_5-tags-en.wav`, from section 2's
grid — the same question as section 5 on one line, kept because it is the file the language grid
already renders under all three pins.

---

## What this leaves the issue

The disqualifier the issue named — a model that infers the language per line — **did not appear as a
rejection**, and whether it appears as a *behaviour* is the listening test above.

Two things did appear that the issue's "if the spike passes, more or less" does not cover:

1. **There is no rate control on v3.** The plan's contingency was "if v3 differs, the rate row
   narrows per model". It does not differ; it is absent. A picker would have to disable the rate row
   for one of its two entries and say why.
2. **The latency figure the picker's entries would print is wrong.** "v3 Conversational — more
   expressive, ~280 ms" is a number from a streaming API description; on d47's own path the honest
   entry is nearer two seconds on a normal line.

Both are the maintainer's call, not the spike's.
