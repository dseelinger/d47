# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are in
[bugs.md](../../bugs.md). Everything below behaves as built; the request is that it be built
differently.

An entry leaves this file when it ships, and the line it gets in [CHANGELOG.md](../../CHANGELOG.md)
under the release that carried it is its permanent record.

Several items carry an **open question** that changes the work materially, marked as such.
Those want an answer before the code does.

The batch below was raised hand-testing 0.15.0 on 2026-08-16. None of it is implemented.

Where an item contradicts a comment in the source, that is called out. Those comments are
the reasoning being overturned, and leaving one standing beside code that no longer obeys it
turns the file into a liar.

---

## 1. Microphone indicator labels are too wordy

`src/D47.App/Panel/PanelView.axaml.cs:247` — the state-to-label switch.
Strings are pinned by `tests/D47.App.Tests/MicrophoneIsVisibleTests.cs:47`.

| State | Now | Wanted |
|---|---|---|
| `Idle` | Microphone open, nothing kept | **PTT Ready** |
| `Armed` | Listening for you | **Listening...** |
| `Open` | Listening | **PTT ON** |

Wording only. `Idle` is correct behaviour — the device handle is held open, audio runs into a
half-second ring and is overwritten, nothing is kept. The complaint is that the label leads
with the alarming half of the sentence.

**Open question.** "PTT Ready" and "PTT ON" are push-to-talk words, but `Armed` is by
definition the *non*-PTT mode — d47 deciding for itself. So "Listening..." lands there
naturally, and `Open` is left reading "PTT ON" even for a Commander in continuous mode who
never touched a key. Either the labels go mode-aware, or `Open` needs a mode-neutral word.

**Knock-on.** The XML doc above `ApplyMicrophone` narrates the current wording at length
("says outright that nothing is being kept, which is the claim push-to-talk has always
quietly made and never shown"). It has to be rewritten with the strings.

---

## 2. The turn status line is a wall of text

`src/D47.App/MainWindow.axaml.cs:690` — `DescribeTurn`. Also feeds
`src/D47.Core/Audio/SpeechSpend.cs:98`, and the same figures are quoted to the model at
`Capabilities/Builtin/ConversationCapability.cs:139`.

One line currently carries outcome, route, effort, tokens in/cached/out, turn cost, session
cost, unexplained cold prefixes, voice characters and voice cost.

Wanted: a short line plus a link. The link opens a dialog with all of the above laid out
readably, plus **four** running totals:

- rolling 7 days
- rolling 30 days
- current week, Sunday–Saturday
- current calendar month

**The display half is easy; the totals are the work.** None of that history exists.
`TurnCost` (`Core/Conversation/Pricing.cs:67`) is `(Usage, Dollars, Priced)` with no
timestamp, and `SpendTracker` (`:80`) is a `List<TurnCost>` in memory that dies with the
session. Nothing writes spend to disk anywhere. This needs a ledger under `data/`, appended
per turn and read back at startup.

Three things to respect:

- **Core reads no clock.** The ledger takes an injected time source, on the pattern the
  journal reader already uses. "Append a timestamped row" is exactly the code that reaches
  for `DateTime.UtcNow` by reflex.
- **Store an absolute instant per row.** The rolling windows are timezone-free, but
  "Sun–Sat" and "this month" are local-time concepts — compute those boundaries in the
  Commander's zone at query time. Storing local wall-clock makes rows wrong across a DST
  change.
- **Voice spend needs the same treatment.** `SpeechSpend` accumulates separately. Ledger the
  model only and the dialog's week and month figures will look authoritative while covering
  half the cost.

---

## 3. Settings search filters but does not highlight the matches

`src/D47.App/Settings/SettingsView.axaml.cs:647` — `Filter()`, with the matched fields
already known in `Matches()` at `:679`.

Type `speech` and the surviving cards are correct, but nothing marks *why* each survived.

**This does not contradict the existing design, and the fix must not read as though it
does.** Both `Panel/IFilterablePage.cs` and `Filter()`'s own comment argue against
highlighting *instead of* filtering — "92 rows across 14 sections is a haystack, and
highlighting in place in a haystack is a scroll hunt with extra colour". That reasoning
stands. This adds highlight *on top*: the filter cuts the haystack down, then the highlight
says why each survivor is there. Reword both comments to describe filter-then-highlight.

**Worth more than cosmetics.** `Matches()` tests label, help **and key** — and the key is
never rendered. A row can survive the filter today with no on-screen evidence of why. That
needs a decision about how to show a key match when the key is not displayed.

**Reuse before writing.** The transcript pages already highlight — see `Panel/PanelView.axaml.cs`
and `Panel/TranscriptTabs.axaml`.

---

## 4. The search box needs a clear affordance, and Escape

`src/D47.App/Panel/PanelView.axaml:312` — `SearchInput`, inside `SearchRow`.

- An **× glyph** — explicitly not the letter `x` — on the right-hand side of the box.
- **Escape** clears it too.

**Put the glyph inside the box** via `TextBox.InnerRightContent`. Another sibling in the
`SearchRow` StackPanel sits outside the border and reads as a fourth button in a row that
already has Copy / ‹ / ›, not as part of the field. Use U+00D7 or U+2715; precedent for
glyph-as-content is in the same row, where the steppers use U+2039 and U+203A. Hide it when
the box is empty, or it is a control that does nothing most of the time.

Both routes must run the same clear path — reaching `OnSearchChanged` / `Filter(null)` so the
filtered page is restored, not merely blanking `Text`.

`OnSearchKeyDown` (`PanelView.axaml.cs:404`) handles Enter and Shift+Enter only, so **there is
currently no way at all to clear a search** except selecting the text and deleting it. Set
`e.Handled` on Escape, and check first whether anything above the panel already claims it
(window close, overlay dismiss) — otherwise clearing a search also trips that.

Land this with item 10a, so the search box and the key box gain one idiom rather than two.

---

## 5. The ask-bar hint should retire once the Commander has asked

`src/D47.App/Panel/PanelView.axaml:181` — `AskBox.PlaceholderText`, permanently
`Ask D47 something — try "where am I" or "what's your status"`. It is an onboarding hint
wearing a placeholder's clothes, still teaching someone who has been flying for a month.

Wanted: present only until the Commander has typed into the Ask bar **or asked by voice** —
the trigger is "has asked at all", not "has used this control".

**It has to be recorded, not derived.** The house pattern is derivation —
`Settings/FirstRunWindow.cs:22` says "nothing here is recorded as having been shown;
`FirstRun` decides from live state each time" — and the obvious live signal is a non-empty
conversation history. That does not work: `Core/Persona/PersonaHost.cs:58` states d47 "has
never persisted conversation history", so a fresh launch always looks empty and the hint
returns every time. So a persisted flag it is, and it needs a sentence saying why it differs
from `FirstRun`. Settings are append-only, so adding a property is cheap and permanent.

**Open question.** What replaces it? "Only present until…" reads as *goes away*, but an ask
bar with no placeholder at all is bare. My read is that the hint is the *example* half — drop
`— try "where am I" or "what's your status"` and keep a plain "Ask D47 something" — but that
is a guess at intent.

---

## 6. The Technical tab should carry the speech loop, live

`TranscriptPage.Technical` is defined at `Panel/PanelViewModel.cs:42` as "the same
[conversation], with the diagnostics left in". Only **five** things ever append to it, all in
`MainWindow.axaml.cs` (`:151`, `:164`, `:179`, `:611`, `:860`), and all turn-level: designer
notice, status, turn failure.

Meanwhile the speech loop does produce diagnostics — `Voice/VoicePipeline.cs:118` and `:252`,
plus warnings and errors in `Core/Audio/SpeechPipeline.cs`, `Core/Audio/CueLibrary.cs`,
`Core/Audio/FolderAudioSource.cs` and `Core/Listening/ListenGate.cs`. They all go through
`ILogger`, so they land in the Log file tab and never on Technical. **The information exists;
it is on the wrong surface, and the page whose premise is "diagnostics left in" shows almost
none of them.**

Wanted: speech-loop indicators as they happen, and any errors encountered, especially
speech-related.

**Open question — how to route it.** Bridging `ILogger` into the Technical transcript is one
seam that picks up every existing call site and every future one, but log lines are phrased
for a log file and it risks becoming the Log tab twice; it needs a category/level filter.
Explicit stage events give better wording but are hand-maintained, and an unmaintained list is
how this page got thin in the first place. My lean is both, split by kind: bridge the
*errors*, because those are what must not be forgotten at some new call site, and hand-write
the *stage indicators*, because those are what a human reads.

**Open question — what "as they happen" means.** A live status that updates in place while a
stage is in flight is a different control from appended lines that stay in the history. The
mic indicator is the first kind; the transcript is the second.

**Invariant.** If these lines carry timestamps or ordering, that comes from the App side or an
injected time source — not from Core reading a clock.

---

## 7. Introductions should be remembered across sessions

`Core/Persona/PersonaHost.cs:64` — `_introduced`, an in-memory `HashSet`. Cleared by
`ForgetIntroductions()` at `:98`. Surfaced by the row at
`Core/Capabilities/Builtin/PersonaCapability.cs:146`.

**A deliberate deferral being lifted, not an oversight.** `PersonaHost.cs:58` says so:
"Session-scoped like the transcripts themselves: d47 has never persisted conversation
history, and starting to do so is not something this phase was asked for." That comment is
the spec being changed, and it needs rewriting rather than being left beside persisted state.

**The help text becomes false.** The row's help ends "Forgetting puts every core back to its
introduction, **which otherwise costs a restart**." Once this ships a restart resets nothing
and Forget is the only way back. The opening clause carries the same assumption —
"introduces itself the first time you pick it **after d47 starts**". Both move with the
behaviour or the button's own documentation contradicts it.

**Storage.** This is remembered state, not a setting, so it does not belong in `settings.json`
under the append-only rule. `SettingsView` already persists card collapse state through a
view-state store — the established precedent for "remembered UI state that is not a setting".
Reuse it rather than inventing a second store. But `PersonaHost` is Core, which depends on
nothing, so load/save arrives as an injected port; `CoreDependencyTests` will say so
otherwise.

Shares a persistence question with item 5 and with Phase 23's once-per-24-hours rule. Better
answered once than three times.

---

## 8. The secret editor's button row

`src/D47.App/Settings/SecretEditor.cs` — the row is built at `:96` as
`[ box | Show | Store | Check | Clear | badge ]`.

### 8a. "Show" becomes an eye glyph inside the box

Today a `ToggleButton` flipping "Show"/"Hide" (`:61`), sitting outside the field. Wanted: an
eye-outline-over-pupil inside the box, switching to a crossed-out eye while the key is shown.

`TextBox.InnerRightContent`, same mechanism as item 4 — land them together. Glyphs here are
**drawn in repo** as `Path` data, not taken from a font (see the send glyph at
`Panel/PanelView.axaml:173`, "drawn in-repo like the help mark"), so this is two geometries.
An icon-only control loses its label: give it a tooltip and an automation name, as `AskButton`
does. The reveal shows only what is in the box on the way in — a *stored* key is never shown
back, which is what makes the store write-only and what keeps the eye from being a privacy
hole. Keep that.

### 8b. "Store" becomes "Save" / "Overwrite"

"Save" when nothing is stored, "Overwrite" when something is. Cheap: `IsStored` (`:122`) is
`_settings.HasSecret(...)` and `Refresh()` (`:214`) already opens with `var stored = IsStored;`.
Good on its own merits — "Overwrite" warns that a stored key is about to be replaced.

### 8c. "Check" becomes "Verify Key"

Rename at `:98` is trivial. The **gating is not**.

Wanted: active only when a key has been entered and is in the textbox. But the row's verify
closure is `token => verify(provider.Id, token)`
(`Core/Capabilities/Builtin/ConversationCapability.cs:256`, same shape at
`SpeechCapability.cs:706`) — a provider id and a token, **no key string**. It tests the key
already *stored*, not the text in the box.

So that gating disables the button in the case it is most useful today (key stored, box empty,
"does my saved key still work?") and enables it over unsaved text the check will not look at —
reporting on the stored key while appearing to test the pasted one.

**Open question.** Either gate on `IsStored` instead — one line, keeps today's meaning — or
make verify take a *candidate* key and test the box, which is probably the flow actually
wanted (paste, verify, then Save) but is a signature change through Core and both app-side
implementations. My lean is the second, falling back to the stored key when the box is empty,
so the button means "verify whichever key is about to be in force" and is never pointlessly
dead.

Preserve the existing subtlety: a throw becomes `SecretCheck.Unreachable`, not a rejection,
"because a check that throws is a check that could not be made, which says nothing about the
key. Reporting it as a rejection would send a Commander to reissue a key that works." Note
this is the only button in the row that touches the network.

### 8d. "Clear" as an undo glyph — concern raised

Requested: the undo glyph, outside the box.

**`Clear()` (`:161`) is not an undo.** It blanks the box *and* calls
`_settings.Apply(_row.Key, null, SettingsCaller.Panel)` — it **deletes the stored key**. An
undo arrow reads as "put back what I just typed": harmless, reversible, no confirmation
expected. This destroys a credential the Commander may have to reissue from the provider.
"Clear" beside a text box is already ambiguous about which it clears; a curved arrow removes
the last hint that anything is being destroyed.

Three ways out, none chosen: keep it worded and destructive-looking ("Delete stored key"); use
the glyph but make it genuinely undo-like, clearing only the box, with a separate explicit
control for the stored key; or take the glyph as asked with a confirmation step.

These four apply to **every** secret row, not just Anthropic — verify is wired for every
`LlmProviderCatalog` provider with `NeedsKey` and for the speech providers.

---

## 9. VR panel defaults

The grab failure is a defect and lives in [bugs.md](../../bugs.md). These are the defaults.

**Read `docs/` and the Phase 9 notes on OpenVR first.** Six OpenVR/Avalonia traps fail
silently — no exception, no warning — and this is the code they are about.

### 9a. Default to the mini panel

Mode is chosen at `App/Headset/VrPanelSurface.cs:89`. Mini is `(640, 280)` at `:34`, and
`VrSurfaceSettings.Mini()` (`Core/Configuration/VrSurfaceSettings.cs:97`) already supplies a
placement — distance 0.9, drop -0.30, width 0.34.

### 9b. Default to world-locked

`VrSurfaceSettings.Lock` defaults to `"head"` (`:52`).

**Flipping the string is not enough, and it will look like the change did nothing.**
`Core/Vr/VrSurface.cs:114` is `RidesTheHead => Lock != WorldLocked || Placed is null` — a
world-locked surface with no placement still rides the head. A default placement has to be
computed on first show, from the head pose at that moment. This also depends on the grab bug:
today the only thing that ever sets `Placed` is a successful carry.

### 9c. Top of the panel at about knee level, in front of the Commander

The goal is not blocking vision. Current defaults are a 1.1 m-wide quad, 1.1 m away, 0.25 m
below eye level, **head-locked** — a large panel that follows your gaze at chest height, which
is the complaint exactly.

**Open question — the requirement names an edge the settings cannot set.** `Drop` is "metres
below eye level" (`:58`) and positions the anchor; the quad's height is not set at all, it
"follows from the texture's aspect" (`:63`) off `Width`. So the drop that puts the **top** at
knee level depends on width and aspect. Compute it from the wanted top edge, or add a
top-edge-relative placement — do not hand-tune a number that breaks the moment width changes.

**Open question — what knee level is measured from.** `Drop` is relative to *eye* level; knee
height is naturally floor-relative and varies with the Commander's height and with seated
versus standing. Consider deriving it from the room-setup floor rather than assuming a figure.

---

## 10. "Set focus to game" should bring Elite to the front

Wanted: saying *"set focus to game"*, *"Elite"* or similar puts window focus on Elite
Dangerous.

**It closes a real gap rather than adding a convenience.** Key injection refuses unless Elite
holds the foreground — `App/Input/EliteWindow.cs:56` calls that "the one check that stands
between a voice command and typing into a browser", and `ScancodeInjector` aborts mid-sequence
if focus is lost. So today, a Commander who has alt-tabbed away is told that flight commands
are unavailable and the only remedy is to reach for the mouse. This is the one action that
cannot be delegated to the thing that is already refusing to act.

`IEliteWindow` already knows how to find the window and whether it is in front; it has
`IsRunning` and `IsForeground` and does not currently expose a way to *raise* it. That is the
seam.

**Open question — which modality owns it.** The invariant is that safety-critical settings are
protected by the *caller*, not the modality: panel, hotkey and keyword router reach them and
the LLM does not. Focus-stealing is not a settings row, but it is close enough to the injection
boundary to want the same thought. Options, in ascending trust:

- **Keyword router only.** A fixed phrase the Commander speaks, never a tool. Matches how the
  other protected actions work and cannot be reached by anything in the journal or in comms.
- **A tool the model may call.** More natural — "take me back to the game" in any phrasing —
  but a model-callable focus-steal is reachable from untrusted input, and journal text, in-game
  comms, web search and INARA are all untrusted. A hostile in-game message that can yank focus
  to Elite mid-typing is a nuisance at best.

My lean is the keyword router, with the phrase list including at least "set focus to game",
"focus the game", and "Elite" on its own.

**`SetForegroundWindow` does not always work, and fails silently.** Windows refuses foreground
changes from a process that does not hold it, outside a short list of exemptions — a
foreground-stealing call from a background app typically flashes the taskbar button instead of
raising the window, and returns `false`. `App/SingleInstance.cs:108` already calls it, and is
the place to look for whether this repo has hit that. Whatever ships must **report the refusal
out loud** rather than saying nothing, because a silent no-op here reads exactly like the
speech path having failed.

**Confirm the modality question before building**, because a tool and a router phrase are
different code, not the same code with a flag.
