# Proposal — per-role voice providers, then OpenAI, then Kokoro

**Status: proposal, awaiting sign-off. No code written.** Asked for 2026-08-25.

Three phases are proposed — **57**, **58**, **59** — and the numbers are free: 1–21 and 23–53 are
frozen, 22 is retired and never reused, and 54, 55 and 56 are already spoken for.

Read this in order. §1 is four findings that change the shape of the work, and two of them
contradict the brief. §2 is the phase text. §3 is the seams. §4 is what should be an Issue.

---

## 1. Findings first, because two of them move the target

### 1.1 The role set already exists, and it is not the four in the brief

The brief says to derive the roles from the code rather than from its own names. Doing that:
`VoiceCast.VoiceRole` is a **closed set of five**, and it disagrees with the brief's four in
three separate ways.

| Brief | Code | Disagreement |
|---|---|---|
| Ship AI | `ShipAi` | agrees |
| Carrier voices | `CarrierCaptain` **and** `TowerControl` | **two roles, not one** — separately voiced (`CarrierCaptainVoice`, `TowerVoice`), separately auditioned, and `FlavourBriefs` writes a different character brief for each |
| NPCs | — | **not a role at all** |
| Comms messages | `Comms` | carries *both* NPCs and players |
| — | `Crew` | **a role the brief does not mention** |

Two of those matter.

**NPC-versus-player is a boolean inside one role, not a role.** `IncomingMessages` assigns
`Voice = IsMyCarrier(sender) ? TowerControl : Comms`, so every in-game message — from a pirate,
from a station, from another Commander — is `VoiceRole.Comms`. What separates them is
`VoiceCast.ForSender(sender, isPlayer, role)`'s `isPlayer` flag, which today decides only how long
the voice assignment sticks.

**And that distinction is exactly the one the brief's security argument needs.** The
attacker-influenced, unbounded-volume input is *another player's* text. NPC chatter is Frontier's,
written by the game, bounded and not spammable at you. So splitting them is well motivated —
better motivated than the brief argues — but it is a new distinction at the provider layer rather
than an existing one, and it should be built knowing that.

**`Crew` needs a home and the code already implies one.** `RadioVoice.IsOverTheAir` returns false
for `ShipAi` **and** `Crew` and true for everything else: the code's own statement that crew are
aboard rather than on a radio. Grouping crew with the ship's AI follows a division that already
exists rather than inventing a second one.

**Grouping settled with the Commander, 2026-08-25**, and it is by *category* rather than by role:
Carrier is two roles under one provider, Crew and the ship's AI are one category, NPCs are *"not
real people"*, and friends and squadron mates are one category.

**Applying that to the channels the code already reads leaves a fifth category, and it is the one
the security argument is about.** `IncomingMessages.PlayerChannels` is
`{ player, wing, local, friend, squadron, starsystem }` against the single non-person channel
`npc`. Sorted by the Commander's own rule — is this a real person, and is it a person the Commander
chose to be in contact with — those six do not fall into one bucket:

| Channel | Who that is |
|---|---|
| `npc` | not a person |
| `friend`, `wing`, `squadron` | a real person the Commander has accepted, teamed with, or joined |
| `local`, `starsystem` | **any real person in range, with no consent anywhere in it** |
| `player` | a direct message — a real person, and whether that implies contact is a question for the Commander |

**`local` and `starsystem` are the spam vector**, and they are the reason this phase exists: *"a
player spamming local chat spends the Commander's money."* A friend is a real person whose lines
the Commander probably *wants* on the good voice; a stranger in local is a real person whose lines
are heard once, are unbounded in volume, and are chosen by somebody else. Cost and trust point the
same way and both separate those two.

So **five slots**, which is the Commander's four with the human channels split by consent rather
than by humanity:

| Slot | Covers | Why |
|---|---|---|
| **Aboard** | `ShipAi`, `Crew` | the Commander's rule, and `RadioVoice.IsOverTheAir` already groups exactly these two |
| **Carrier** | `CarrierCaptain`, `TowerControl` | two roles, one installation, one provider |
| **NPCs** | channel `npc` | not real people; game-authored, bounded, unspammable |
| **People you know** | `friend`, `wing`, `squadron` | real people the Commander is in contact with by choice |
| **Anyone in range** | `local`, `starsystem`, and `player` pending a ruling | **the untrusted path** — real people, no consent, unbounded volume |

**This needs no new plumbing to be knowable.** The channel is already carried:
`IncomingMessages` builds the key as `$"message.{channel}"`, so every announcement already says
which channel it came from. What is *not* carried is a distinction finer than the
`SpeakerIsPlayer` boolean, which is all that reaches `Cast.ForSender` today — so the routing key
becomes the channel, and the boolean keeps its existing job of deciding how long a voice sticks.

**Two rulings still wanted:** where channel `player` belongs — a direct message is a real person,
but whether reaching you that way implies contact is a game question rather than a code one — and
whether four slots with friends and strangers together is preferred anyway, accepting that the
free-provider setting would then also apply to squadron mates.

### 1.2 The ONNX Runtime licence — the gate would not fail. It would pass, and be wrong to.

The brief says `LicenceGate.Permissive` fails MPL deliberately and asks whether widening it is
needed. **Widening it is not the decision**, because the gate never sees the MPL at all.

I downloaded `Microsoft.ML.OnnxRuntime` 1.29.0 (155 MB, from `api.nuget.org`) and read it rather
than its metadata. What is actually true:

- Its nuspec declares `<license type="file">LICENSE</license>`, and that file is **MIT**.
- `ThirdPartyNotices.txt` lists **Eigen under MPL v2.0**, with the licence quoted in full — the
  brief's claim, confirmed.
- `LicenceGate.FromFile` reads **only the declared licence file**. It never opens
  `ThirdPartyNotices.txt`. So the gate resolves this package as MIT, marks it permissive, and says
  nothing.

**This is the `OpenCvSharp4.runtime.win` case exactly**, which `PackageLicenceGateTests` already
records: a package that declares one licence and packs another thing's code, invisible to any
metadata check, found by a person reading the package. The gate's honest claim — *"nothing in the
graph declares a licence d47 may not ship"* — remains true and remains insufficient.

**Two facts make the exposure much smaller than it looks, and both are verifiable:**

1. **ONNX Runtime is built with `EIGEN_MPL2_ONLY`, unconditionally.**
   `cmake/adjust_global_compile_flags.cmake` line 188 is `add_definitions(-DEIGEN_MPL2_ONLY)`,
   under the comment *"Guarantee that the Eigen code that you are #including is licensed under the
   MPL2 and possibly more permissive licenses (like BSD)."* So the LGPL-licensed corner of Eigen is
   compiled out. The exposure is MPL-2.0 and nothing worse.
2. **MPL-2.0 is file-scope copyleft and explicitly contemplates this.** §3.3 permits distributing a
   Larger Work under other terms provided the Covered Software stays under MPL; §3.2 requires
   telling recipients how to get the Covered Software's source. Eigen is header-only, so it is
   compiled into `onnxruntime.dll` — d47 would redistribute an unmodified binary Microsoft built,
   and would modify no Eigen file.

**So the honest options, for your decision and not mine:**

| | What it costs |
|---|---|
| **A. Accept, and say so in `NOTICE`** *(recommended)* | One entry naming Eigen, MPL-2.0, and where its source is. Satisfies §3.2. Does not relicense d47 and does not touch the gate. |
| **B. Accept and widen `Permissive` to include MPL-2.0** | Wrong instrument: the gate would not have caught this anyway, and widening it lowers the floor for every future package, including ones that *do* declare MPL and would have been caught. |
| **C. Refuse** | No local TTS except the Windows built-ins, which the brief has already ruled out. Phase 59 does not happen. |

**Recommending A.** The gate stays as it is and keeps its meaning; a person made a decision and it
is written down where the obligation is discharged. Per the gate's own failure text, that record
should name who decided and when.

**One thing I did not verify and would before building:** that the shipped `onnxruntime.dll`
contains no other third-party code the notices do not mention. I read the notices and the build
flag; I did not disassemble the binary.

### 1.3 `VoiceChoices` already carries the extra dimension. `VoiceMemory.Switched` does not.

The brief asks whether the key becomes `(role → provider → voice)`. It does not need to.

`SpeechSettings.ProviderVoices` is `provider → VoiceChoices`, and `VoiceChoices` holds
`{ Ship, CarrierCaptain, Tower, Cores, Paired }` — **one field per role already**. Per-group
providers do not add a dimension to the store; they change *which fields move* when a switch
happens.

What breaks is `VoiceMemory.Switched(settings, from, to)`, whose whole signature assumes one
provider changed for everything. It becomes per-group: switching the Carrier group stashes and
restores `CarrierCaptain` and `Tower` and leaves `Ship`, `Cores` and `Paired` alone.

**`VoicesProvider` is the field that has to grow**, and append-only says how. Keep
`Speech.Provider` and `Speech.VoicesProvider` meaning exactly what they mean today — the Aboard
group's — and add one nullable map for the other three. **Absent means "same as `Provider`"**, so a
settings file written before this change loads and behaves identically, which is the Phase 54
pattern and is assertable rather than assumed.

Phase 19's rule survives intact and gets a second application: a choice that cannot be filed under
a known provider is dropped rather than filed under a guess.

### 1.4 Per-role rate contradicts a stated ruling in the source

`VoiceCast.Rate`'s comment: *"One value for the whole cast rather than per role: it is a property
of how fast the Commander likes to be spoken to, not of who is speaking."*

That is a decision, not an accident, so the brief's lean toward per-role rate overturns it and the
conventions say that must be called out rather than left standing.

**The trade-off.** Rate is per *provider* today because providers disagree about units and range —
Edge takes a wide percentage offset, ElevenLabs a multiplier it refuses to exceed, and the row
narrows to the selected provider's range. Per-role providers already deliver most of what the
brief wants: NPCs on Edge and the core on ElevenLabs are *already* two independently-set rates,
because they are two providers. What per-role rate adds is only the case where **two slots share
one provider** and the Commander wants them at different speeds.

**Recommending: not in Phase 57.** It is a second dimension on a settings row that has a doc anchor
(`speech.md {#rate}`), it makes the row's range question ambiguous when a role's provider differs
from the row's, and the phase is large enough. It should be its own change request once per-role
providers are in and it is clear how often the shared-provider case actually bites.

---

## 2. The phases

Proposed split matches the brief — refactor first, one phase per provider — **with one amendment:
the OpenAI language spike is a prerequisite of writing Phase 58's text, not an item inside it.**
The brief flags language drift as a thing to measure, and its outcome could change which role
OpenAI is fit for. A phase whose own subject depends on an unmeasured fact should not be written
yet.

### Phase 57 — Every voice can come from somewhere different

- [ ] **Four slots, and they come from the roles that already exist** — Asked for 2026-08-25.
  Today one provider speaks for everybody. It becomes a choice per group: **Aboard** (the ship's
  AI and the crew), **Carrier** (the captain and the tower), **NPCs**, **people you know**, and
  **anyone in range**.
  **The groups are derived rather than invented**: `RadioVoice.IsOverTheAir` already separates
  aboard from over-the-air, `VoiceCast` already holds five roles, and `ForSender`'s `isPlayer` flag
  already separates a Commander from an NPC — and `IncomingMessages` already carries the channel in
  the announcement key, which is what separates a squadron mate from a stranger in local. A
  `VoiceGroup` sits above `VoiceRole` and no existing role changes meaning. Accepted when each slot can name a different provider and every voice still
  comes out of the arbiter in the order it was written.
- [ ] **No other player's text has to leave the machine** — The reason this phase exists rather
  than a nicety it enables. `speech.md {#egress}` already says re-voiced messages are *"written by
  other players"*, which makes local and system chat an **untrusted, attacker-influenced, unbounded-volume**
  path pointed at a per-character paid API — someone spamming local chat spends the Commander's
  money. Accepted when the ship's core can be on a paid provider **while anyone-in-range is on one
  that costs nothing and sends nothing**, and when the settings surface states that plainly rather
  than leaving it to be inferred.
- [ ] **One client per provider, never one per slot** — `ElevenLabsTtsProvider.MaxConcurrent`
  justifies itself with *"Callouts, crew lines and re-voiced comms all share the same account, so
  the gate has to be here rather than in any one pipeline."* **That reasoning only survives if two
  slots choosing the same provider share one instance.** Four clients would each believe they owned
  the account's whole concurrency budget, and the failure is the one Phase 11 already fixed: a red
  banner and a sentence the Commander never heard. Accepted when a test pins that selecting one
  provider for two slots constructs it once.
- [ ] **The wiring plan becomes a diff and stays a pure function** — `SpeechWiring.Plan` returns
  one `RebuildClient` bool today. It becomes per-slot: which changed, which clients to build, which
  to dispose, which to share. **It builds nothing, disposes nothing and reaches no network**, which
  is the property it was extracted for and the reason both faults found in one afternoon's
  hand-testing became reachable by a test. Its harness grows with it, keeping the key-arrival edge
  it was written for — selecting a provider before pasting the key, then pasting it.
- [ ] **A file written before this still loads, and sounds the same** — `ProviderVoices` already
  holds one field per role inside `VoiceChoices`, so the store gains no dimension; what changes is
  which fields move on a switch, and `VoiceMemory.Switched` becomes per-group. `Speech.Provider`
  keeps its meaning as the Aboard group's, and the other three read a new nullable map where
  **absent means "the same as Provider"**. Phase 19's rule is unchanged and applies twice over: a
  choice that cannot be filed under a known provider is dropped rather than filed under a guess.
  Accepted when a settings file from before this change loads, and every voice is the one it was.
- [ ] **The disclosure becomes a table, because one sentence can no longer be true** — Phase 4
  requires stating exactly what leaves for the selected provider, and there is no longer one.
  Accepted when the row states what leaves **per slot**, when the Privacy list agrees with it
  rather than restating it, and when the state the item above describes — the core paid for, no
  other player's words leaving — reads as one legible sentence rather than four rows a Commander
  has to combine.
- [ ] **Which slot is costing money is a question worth being able to ask** — `SpeechSpend` records
  per provider and that survives untouched. Per-slot is what the refactor makes askable for the
  first time, and it comes free without weakening the metering seam: `MeteredTtsProvider` stays the
  single place counting, and one thin decorator per slot wraps the **shared** client, so the count
  knows the slot without `ITtsProvider` learning about roles and without a second client. Accepted
  when the cost row breaks down per slot and the per-provider total still agrees with itself.

### Phase 58 — A voice from OpenAI

**Not written yet, deliberately.** Its text depends on the spike in §4.1: if the language drift the
brief flags is real, this provider's fit for the Aboard slot is the phase's own subject and the
item list changes. The shape it will take is in §3.6.

### Phase 59 — A voice that never leaves the machine

**Blocked on §1.2 and not written until it clears.** Intended for NPCs and anyone-in-range, which is
where "free, offline, unlimited, and nobody else's text leaves" is worth most.

---

## 3. The seams

| Seam | Change |
|---|---|
| `VoiceRole` | **Unchanged.** Five roles keep their meaning. |
| `VoiceGroup` (new) | Five members. A pure `GroupOf(VoiceRole, string? channel)` maps role plus channel to a slot, in Core, testable, with the mapping asserted rather than commented — the channel because the `SpeakerIsPlayer` boolean cannot tell a squadron mate from a stranger in local. |
| `SpeechWiring.Plan` | Per-slot diff. Still pure, still builds nothing. |
| `SpeechWiringState` | Holds a provider id and key-presence **per slot**, not one pair. |
| `VoiceMemory.Switched` | Per-group stash and restore. `VoiceChoices` unchanged. |
| `SpeechSettings` | One new nullable per-slot provider map. `Provider`, `VoicesProvider`, `ProviderVoices`, `ProviderRates`, `CharacterPrices` all keep their keys and meanings. |
| `MeteredTtsProvider` | **Stays the only metering seam.** One decorator per slot over a shared client. |
| `SpeechCharge` | Gains the slot. Per-provider totals unchanged. |
| `ITtsProvider` | **Unchanged.** No role, no slot, no metering. |
| `SpeechPipeline` | **Untouched.** Ordered enqueue over concurrent render is not disturbed; stop still reaches synthesis. |
| `AppHost` provider switch | From one `_tts` to a small map of slot → client, built from the plan. The audition path takes a slot. |

### 3.6 What OpenAI needs that the seam does not have yet

- **A static catalogue is honest, and the contract should say so.** `VoiceListing` has four states
  built for a network answer. OpenAI has no voices endpoint, so `Unreachable` is unreachable and
  `NoKey` is wrong — the voices are known without a key. Proposal: **answer `Listed` always**, and
  do not invent a fifth state. `VoiceCatalogue.WhyEmpty` is only consulted when the list is empty,
  which for this provider it never is, so nothing downstream needs teaching. A fifth state would
  need a sentence in `WhyEmpty` describing a situation that cannot arise.
- **The Check button needs a probe that is not a voice list.** It proves a key by listing today.
  The cheapest honest probe is a one-character synthesis, discarded — it costs a fraction of a cent
  and exercises the actual path. Whatever it is, **a failure must still distinguish "refused the
  key" from "could not be reached"**, which `docs/spikes/elevenlabs-voice-sources.md` §3 establishes
  as load-bearing.
- **`instructions` does not change `VoiceSelection` — but only if it is per-provider config rather
  than per-utterance.** `VoiceSelection` is `(VoiceId, Rate, Name)` and is constructed all over.
  Driving `instructions` from `guardian-personas.md` means the *persona* supplies it, and the
  persona is known where the client is built, not where a sentence is pushed. So the seam to leave
  is on the provider's construction, not on the selection record. Answering the brief's question
  directly: **accepting it later does not change `VoiceSelection`'s shape**, provided it is not
  made per-sentence.
- **Both ⚠️ items are measurements, not assumptions.** `speed` and language drift are in §4.1.

---

## 4. Out of scope — Issues, not phase items

1. **Spike OpenAI's language stability** *(blocking Phase 58's text)*. Synthesise a Guardian line
   seeded with `Shinrarta Dezhra`, `Ngalinn`, `Deciat`, `LHS 3447` and two HIP designations against
   the pinned snapshot. Report drift, and whether `speed` is honoured or silently ignored. Written
   to `docs/spikes/` either way, because a negative result is the finding.
2. **The Edge class doc is stale** — the drive-by. `EdgeNeuralTtsProvider.cs:19-21` still says audio
   is requested as raw PCM "on purpose"; `EdgeProtocol.cs:83` requests
   `audio-24khz-48kbitrate-mono-mp3`, and the comment above it records that the raw formats were
   withdrawn mid-2026. Mentioned, not folded into any of this.
3. **`instructions` from `guardian-personas.md`** — the reason to want OpenAI, deliberately not in
   Phase 58.
4. **Per-role speaking rate** — §1.4. A change request once the shared-provider case has been felt.
5. **Cartesia** — see below.

### Cartesia: not ahead of OpenAI, but ahead of it on one axis

The brief invites the case. **It does not displace OpenAI, but one of its properties should worry
us more than the brief lets on.**

OpenAI's differentiator is `instructions` — steering accent, tone and delivery from prose, which is
what would let a Guardian core be *cast* rather than merely assigned a voice. Nothing else on the
list has it, and it is a direct fit for the Aboard slot.

Cartesia's differentiators are speed control and **explicit language pinning** — both of which d47
already gets from ElevenLabs, so on paper it is a better ElevenLabs rather than a new capability.
Two things against it now: its concurrency cap of 2–3 on entry tiers is *tighter* than the limit
`MaxConcurrent` was written for, and its voice-library size is unpublished, which matters for a
provider whose value would be NPC variety.

**But:** OpenAI has **no language parameter at all**, and language inferred from text is the exact
failure that moved the ElevenLabs pin off Multilingual 2. Cartesia pins language explicitly. So if
the §4.1 spike shows drift on system names, **Cartesia is the better Aboard provider and OpenAI is
the worse one for the reason d47 has already been bitten by.** That is a real fork in the plan and
it is worth knowing before Phase 58 is written rather than after.
