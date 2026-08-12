# TheApp - Capability Checklist

- [x] **Phase 1 — Foundation**
  - [x] TheApp installs on a clean machine - Installability treated as a 0.1.0 feature rather than a packaging afterthought, so no source-mode path ever diverges from the installed one. Per-user install, no elevation, unsigned with a published SHA-256, and one self-contained file with no runtime prerequisite. Not statically linked in the C sense - native libraries self-extract on first run - because architecture.md §9 shows that phrase has no clean referent in .NET, and NativeAOT stays deferred until the native dependency set is stable.
  - [x] TheApp keeps your key and your state safe - Settings survive a restart, unknown keys are rejected, and everything writable lives in one folder beside the executable. Two stores because one loader cannot both fail loudly and shrug; writes go to a `.writing` sibling then an atomic move, and secrets are DPAPI-encrypted at rest.
  - [x] TheApp says what went wrong - Levels, one target per subsystem, structured fields, and two sinks - one human-readable, one machine-readable so an agent can parse a session. No telemetry leaves the machine: no analytics, no metrics endpoint, no upload. Provider traffic is a separate claim with a separate row, because INARA, a cloud LLM, a paid TTS and web search all send journal-derived content off the machine when enabled.
  - [x] Turn a subsystem up without restarting - Runtime per-subsystem verbosity control.
  - [x] TheApp calls a capability - A request produces a real tool call that runs and returns a result, covering the capability registry and the descriptor declaring identity, tools, help, settings rows and display model. Descriptors are registered once and never mutated, and every tool profile's schema block is byte-identical across turns, so prompt caching survives a mode change.
  - [x] Every capability has a documentation page - CI fails if a registered capability has no documentation page, making "write the docs later" impossible rather than merely discouraged. Pages are written from real artifacts and must quote at least one code block or line of real output.
  - [x] GitHub Pages documentation - Each capability should have a documentation page. General help for TheApp outside of the particular capabilities is also available.

- [x] **Phase 2 — Journal spine**
  - [x] Journal behavior is testable without the game - A recorded session replays deterministically at 1x and 100x with no game, headset or hardware. Fixtures are byte-preserved via `.gitattributes` and scrubbed of the Commander name and real system visits, since the repository is public.
  - [x] TheApp knows where you are - TheApp answers what system you are in from the journal as part of an ordinary turn. Picks the newest journal by ordinal filename sort, opens shared for read/write/delete since Elite holds the file open, and is pull-based with no internal thread, which is what makes it testable.
  - [x] Survive a journal schema change - Elite adds and changes journal events several times a year. Unknown events are logged and skipped, a parse failure never kills the tail loop, and the machine-readable sink makes the diff findable, so a game patch costs a morning rather than an outage.
  - [x] Handle more than one Commander on a machine - A second Commander's journals must not merge into the first one's fleet, materials or location. The Commander is identified from the journal header and all derived state is kept per Commander, because a naive newest-file reader silently blends two people into one.

- [x] **Phase 3 — First conversation (typed)**
  - [x] TheApp answers a typed question - Type a question into TheApp's window and get a streamed reply from the model.
  - [x] Ship's AI Unsure - An explicit "unsure" result instead of a confidence threshold, because models produce confident-sounding scores that do not mean anything. A model-free keyword router ships alongside so the whole path is exercisable without a model.
  - [x] Capabilities as state, not guard - For example, an LLM or TTS experiencing downtime or token depletion flips a capability off and the next turn reads what is currently available, so there is no failure handler to author or keep in register. It falls out that every input path must be answerable with no capabilities at all.
  - [x] Model Level and Thinking - LLM attempts to gauge per-turn thinking effort from low through max, but no "off" unless the LLM is set to "none" or through transient degradation.
  - [x] LLM Turn Price - Per-turn token usage including cache reads, priced from a table, plus a running total. A profile switch is the only sanctioned cause of a cold prefix, so an unexplained cache miss is a regression the running total can surface rather than hide.

- [x] **Phase 4 — Settings surface**
  - [x] TheApp is configured without manually editing a file - Every setting, including the voice, is changeable in the UI with no hand-editing. Rows are generated from capability descriptors, defaults appear as placeholders rather than values, keys are write-only, and safety-critical rows are protected from the model.
  - [x] Apply every setting without a restart - Changing a setting takes effect immediately (no need to manually "save").
  - [x] Offer one searchable picker everywhere a value is chosen - A command-palette style picker with keyboard navigation, reused for voices, models and devices. Fail-soft by contract: with an empty list you can still keep the current value or type one.
  - [x] Show the controls the active provider actually has - Settings adapt to the selected provider instead of showing a hardwired set, and changing an endpoint resets the model list to that endpoint's namespace rather than leaving a stale selection.
  - [x] Settings only expose specifics of a provider (LLM, TTS, STT), such as selected voice(s), Model, etc. for the currently selected provider.
  - [x] Protect safety-critical settings from the model - Rows gating keyboard actions and macros are marked protected: never changeable through a tool the model can call, because the model consumes untrusted text and a guard it can flip is privilege escalation. Protected is a property of the caller, not of the modality. The panel, a hotkey and the model-free keyword router all reach these rows; the LLM path does not.
  - [x] Say what each provider receives - A settings row and a documentation page stating exactly what leaves the machine for each enabled provider: system and station names to INARA, turn text and game context to a cloud LLM, reply text to a paid TTS, query text to web search. What is currently leaving is answerable at any time, and local-only operation is a reachable configuration rather than a theoretical one.
  - [x] Link each settings row to its documentation - A setup-guide link per settings row pointing at that setting's page. In-app help stays the short form; the page is the long form.
  - [x] Collapse settings cards - Collapsible sections whose collapse state is remembered as a view preference, not a setting, and applied before first paint so a collapsed card never flashes open.
  - [x] Settings Nav Menu - Is present and highlights the section based on the topmost visible setting.
  - [x] Hotkey Binding - (Only in main window) Press the key to bind it.
  - [x] Themes - Dark, light, default Elite-flavored, Guardian, and based on the current Elite Color Scheme palette, with color living in one place so no view hardcodes a literal.

- [ ] **Phase 5 — Speaking**
  - [ ] One audio stream - Everything audible goes through one queue with priority and supersede, including audio cues, so ducking, interruption and captioning need no second mechanism. One arbiter for every voice, because separate paths per voice are how a line gets spoken in the wrong one. The arbiter exposes a render-side reference tap from the start, since echo cancellation needs the far-end signal and retrofitting that tap means opening the one component every voice path depends on.
  - [ ] TheApp speaks its answer aloud - The reply is audible in the chosen voice. Synthesis is sentence-chunked so speaking starts at the first sentence boundary - the largest perceived-latency win available.
  - [ ] Say so when the model is misconfigured - An audible warning when the provider setup is wrong, rather than silence that is indistinguishable from a model with nothing to say.
  - [ ] Say something when a turn is taking too long - Retry with backoff (N tries, X wait, sequential or logarithmic), and if exceeded say so aloud in the current voice rather than leaving silence.
  - [ ] #20 Give every loop state its own audio cue - One default per state. Cue names come from the shipped resources rather than a hand-written table, since a wrong name goes wrong as silence nobody notices.
  - [ ] #18 Play a thinking bed while the model works - Audible evidence that TheApp heard you and is working, instead of dead air while a turn runs.
  - [ ] Shut up - An always-available instant silence that supersedes everything on the queue and stops mid-sentence, reachable by voice and by hotkey and never gated behind a turn completing. A companion that talks is only tolerable if it can be silenced faster than it can finish a sentence.

- [ ] **Phase 6 — Listening**
  - [ ] Choose the microphone in settings - Pick the input device instead of inheriting the system default. A blank selection produces a silent default and a turn reporting no speech detected, with nothing indicating why.
  - [ ] PTT - Hold the key, speak, release, and your words appear. The key-down path awaits nothing before recording starts, and push-to-talk is one gate policy over an audio stream so continuous listening and wake word become later policies rather than a rewrite. May be set as a "toggle" rather than true PTT via settings.
  - [ ] STT Model Choice - Commander can choose which Whisper model to use and whether or not to allow it to run on the GPU. In VR the GPU is already the scarce resource, so a large model running there surfaces as dropped frames and reprojection rather than as anything that looks like a speech problem, which is the hardest kind of setting to diagnose. GPU is opt-in with that cost stated on the row, and the default is CPU whenever a headset is present - a short push-to-talk clip on the small English models absorbs that fine.
  - [ ] Bias transcription with proper nouns from the journal - Feed system, station and ship names from the journal into the transcriber so they come back spelled correctly. Journal-derived and network-free; proper nouns are where speech recognition fails hardest and most silently.
  - [ ] Report a key that is bound twice - A double-bound push-to-talk key currently has no symptom other than not working; detect the collision and say so. Reads the same parsed binds as keyboard-action reachability rather than keeping a second view of the Commander's controls.
  - [ ] Settings by voice - Unless otherwise noted, every setting should be settable by voice. Protected rows are reachable by voice only through the model-free keyword router, so "by voice" never silently means "by the LLM."
  - [ ] Answer what can you do, from the registry - Spoken help projected from the capability registry - groups, then capabilities, then detail and example phrasings - ranked by real usage. The model is never asked what TheApp can do, because asking produces confidently invented capabilities.

- [ ] **Phase 7 — Knowing the game**
  - [ ] Know what is happening around you - Live situational awareness attached to each turn: where you are, what you are flying, and what just happened.
  - [ ] Know your location and your carrier (if owned) - Current system, station and body, plus where your fleet carrier is.
  - [ ] Ship's loadout - Modules and stats from the loadout event.
  - [ ] Ship metrics - Base jump range and similar, computed from what the loadout event actually reports rather than from a table of ship specifications.
  - [ ] Know what ships you own and where they are - A fleet registry built from stored-ships journal events.
  - [ ] Know what materials you are carrying - Materials inventory built from journal events, answerable without an external lookup.
  - [ ] Know what is in your backpack and ship locker - On-foot inventory parsed from the local files Elite writes.
  - [ ] Session summary - Earnings, bounties, exploration data sold, materials gained and jumps made since the session began, built from the journal already being tailed rather than from anything new.

- [ ] **Phase 8 — Proactive speech**
  - [ ] Call out danger without waiting for a turn - Interdiction, shields down, hull damage, dangerous heat and a full cargo hold announce themselves from journal and Status.json events. These fire on the event and never at the model's discretion, because an alert that depends on a turn completing is not an alert.
  - [ ] Fuel and range safety - Low-fuel warning against the current tank, plus the case that actually strands a Commander: the next star on the route is unscoopable and the one after it is out of range. The journal and the route file supply both, and the warning is unconditional rather than something the model may decide is not worth mentioning.
  - [ ] Route Progress - Jumps remaining (every N systems (setting)), next system, neutron and white-dwarf hazards ahead, and scoopable-star ambiguity resolved rather than guessed. Reads the route file Elite writes locally, so no route-planning service is needed.
  - [ ] Remark on an unusually long jump - Flavor during a longer-than-normal hyperspace jump, fired once hyperspace has actually been entered rather than on the jump being initiated. Threshold is configurable but defaults to 20 seconds.
  - [ ] Recognize where you have arrived - Place-aware callouts for engineer bases, notable stations.
  - [ ] Home System - Home callout when you arrive in your home system (setting).
  - [ ] Call out material-gathering milestones - The first unit, then 25/50/75%, a running count above 75%, and full. The tracker is primed from the session backlog at startup; otherwise starting TheApp after Elite means the milestones never fire.

- [ ] **Phase 9 — VR**
  - [ ] Order agnostic Overlay - The order of starting SteamVR, Elite Dangerous, TheApp, doesn't matter.
  - [ ] TheApp appears in the headset - Captions render over Elite in VR through OpenVR (Elite never calls OpenXR) and clear on their own. Captions are their own unmovable, output-only, ephemeral layer with a rolling three-line window timed from the end of speech. Captions follow Netflix CC standard.
  - [ ] Configure the captions - Follows Netflix CC standards.
  - [ ] TheApp's panel works in VR - One widget tree renders to both the desktop window and VR, so the windowed UI can never be more functional than the headset one. Mini is a mode of the same panel - a reduced content set - not a separate surface or a scaled-down copy.
  - [ ] VR Panel locking - Head-locked or world-locked placement, per surface. Choosing world-locked implies re-anchor is bound and reachable off-panel.
  - [ ] Re-anchor the panels - Elite's in-game recenter moves the cockpit without telling SteamVR, so world-locked surfaces drift out of position with no event to hook. A Commander-triggered re-anchor snaps every world-locked surface back to current head pose and preserves their relative layout rather than stacking them. Reachable by voice and by hotkey, never only from the panel, because a drifted panel is precisely the case where you cannot aim at it. Head-locked surfaces and the caption layer are unaffected.
  - [ ] Overlay Positioning & Look - Opacity, curvature, distance, size, and scale, configurable per surface.
  - [ ] Panels can switch between curved and flat - The panel is bent {curve setting} around the Commander.
  - [ ] Scale the big panel - Zoom the panel up and down in the headset, distinct from mini mode, which reduces content rather than shrinking the rendering.
  - [ ] Keep working when the main window is minimized - In 2D or in VR, if TheApp is minimized, all 2D, VR, and voice functionality still works.

- [ ] **Phase 10 — Acting on the game**
  - [ ] Know which actions the Commander can actually reach - Parse the binds file and know which in-game actions have a keyboard binding at all, because the audience is HOTAS-heavy and a stick-only action asked for by voice fails as silence. Unbound actions are excluded from the advertised profile and asking for one gets a spoken reason instead of nothing. Read-only: TheApp never writes the Commander's bindings.
  - [ ] Autonomous actions are opt-in per action - Every other capability acts on something the Commander said. An action that fires a game input on a journal event with nobody asking is a different category and needs its own consent, so each one is off by default and enabled individually. The category gets its rule before the second member of it exists.
  - [ ] TheApp honks (discovery scanner) when you arrive in a system - The discovery scanner fires on entering a system using the Commander's own key binding, injected via scancodes with the Elite window focused. Never installs a low-level keyboard hook, and `release_all()` is unconditional because a stranded key here is a throttle that will not stop. The first member of the autonomous-action category and therefore off by default and enabled on its own row.
  - [ ] Offer only the actions that work right now - Mode-gated action advertisement, so the set differs on foot, in the SRV, in supercruise and in normal space. The cockpit is not one mode: hardpoints, cargo scoop and landing gear do nothing in supercruise, and Status.json flags supply the split for free. The model is never told about a key that currently does nothing, whether the reason is the mode or the absence of a binding.
  - [ ] Decide which tools ship on a turn as the count grows - At some capability count the tool schemas stop fitting comfortably in context and something must choose between them. The choice is between pre-declared profiles, not between individual tools, so budget pressure degrades to a smaller profile instead of destroying caching on the exact turn caching starts to matter. Whatever is chosen, the anti-invention guardrails are static prompt material and must never be strippable by a budget or effort setting.
  - [ ] Control flight and navigation by voice - Landing gear, lights, cargo scoop, hardpoints and the frame shift drive.
  - [ ] Control ship systems by voice - Power distribution, fuel scoop, silent running and heat sinks.
  - [ ] Control panels, UI, and fire groups by voice - Cycle the panels and fire groups without taking a hand off the stick.
  - [ ] Control the SRV by voice - Toggles only (turret, forward/back, dismiss/recall ship, board, exit SRV, etc.).
  - [ ] Put something on the clipboard - A system name, route or value placed on the clipboard so it can be pasted into the game or a browser.
  - [ ] Galaxy Map - Set a course by sending keystrokes to the in-game galaxy map. Best-effort rather than guaranteed, since it depends on map focus, layout and the game's language setting, so the clipboard route is the primary path and auto-plot is the convenience layered on top of it. A failed plot says so rather than leaving the Commander believing a course is set.
  - [ ] Send/Receive messages to another commander or commanders - Dictate into Elite's chat: local, wing, system.
  - [ ] Macros - Named multi-step voice-triggered sequences, validated against closed vocabularies and the action allowlist. Invocation is by voice; authoring is not, because composing a new action sequence is the one input whose vocabulary cannot be closed in advance. This is the exception to "every setting can be set by voice."

- [ ] **Phase 11 — Persona and voices**
  - [ ] Personas - Pre-built companion characters the Commander can choose between. Guardian Flavored.
  - [ ] Ship AI Naming - Defaults to Persona's name, but may be set by the commander.
  - [ ] Say when the persona has changed - New persona acknowledges when it has been picked. If it changes before its acknowledgement has completed speaking, it stops and the next one starts. Speech, as always, may be interrupted by the commander.
  - [ ] Personality on/off - Plain answers with no persona, flavor, or ambient remarks. The anti-invention guardrails must survive the persona being switched off, so they cannot live inside the persona's prompt block.
  - [ ] Commander's About Me - A Commander-provided prompt stored between sessions and personas so that the LLM remembers basic facts about the commander.
  - [ ] Ship's AI Avatar - Per-state avatar imagery on the panel. Animated formats supported.
  - [ ] ElevenLabs - First offered, paid voice provider alongside the free Edge Neural voices, chosen per role, so a Commander with a key hears it and nothing else changes. Other providers may be added later in development. Differences between providers, such as speed, is maintained on a per-provider basis.
  - [ ] #33 Pair a default voice to each persona - At first startup, lazy-load available voices (Edge neural defaults to "en-" voices). Using LLM (if available), a sensible voice is chosen for each persona based on the persona's prompt (or custom prompt) in the background, so picking a character does not also mean auditioning potentially hundreds of voices.
  - [ ] Ambient Voice - In-character ambient lines motivated by live game state, varied (by LLM if available, otherwise by initially the 10 generated stock phrases per game state covered).
  - [ ] Speak incoming messages in another voice - Re-voice in-game communications so they do not arrive in the ship AI's own voice.
  - [ ] Voices "stick" - Once a voice has been chosen for a sender it stays with them. NPC identities are scoped to the system, since the cast turns over on a jump. Player Commanders are scoped to the session and survive hyperspace, because a wingmate whose voice changes on every jump reads as a bug rather than as variety.
  - [ ] Carrier Captain - If a player owns a Fleet Carrier, they can choose dedicated voices for the Captain and Tower Control, with varied LLM arrival and departure responses.
  - [ ] Ship Crew - Multi-character roleplay with invisible crew (aside from the Ship's AI) on the conversation path, with per-ship rosters drawn from the real fleet and hired NPC pilots imported from the journal with their roles. Crew is addressable and responsive via LLM.

- [ ] **Phase 12 — Soundscape**
  - [ ] #96 Ambient audio mixer - Per-category levels and mute, plus how each category ducks against speech.
  - [ ] Custom Sound Cues - Drop in your own audio files and have TheApp use them, kept distinct from the set TheApp ships with.
  - [ ] Ambient music - Music separate from cues and sound effects, with its own level and its own ducking behavior against speech. Ambience is situational based on journal or "general."
  - [ ] Pick up dropped-in audio without a restart - Cues, sound effects, music and ambient content discovered from convention folders and reloaded live.

- [ ] **Phase 13 — Hands-free listening**
  - [ ] Echo Cancellation - Acoustic echo cancellation, for continuous listening and natural interruption. Consumes the render reference tap from the single audio arbiter rather than a loopback capture, which is both lower latency and correctly aligned. WebRTC AEC3 is BSD-3.
  - [ ] Voice Activity Detection (VAD) - (An option) Voice activity detection as a continuous-listening gate policy over the existing stream.
  - [ ] Wake Word - Wake-word gating, implemented as another gate policy over the same audio stream.
  - [ ] Show that the microphone is open - Once wake word or VAD means continuous capture, listening state is visible on both the panel and the VR surface. Continuous capture with no visible state is the thing a user is right to distrust, and the indicator is a property of the gate policy rather than of any one capability.

- [ ] **Phase 14 — Knowledge and external data**
  - [ ] Galaxy Search - Systems, stations and bodies, distances computed from coordinates. Query parameters are validated locally because the search service ignores filter keys it does not recognize.
  - [ ] Find Nearest - Nearest module, nearest ship for sale, stored ships and modules, plus body and signal lookups.
  - [ ] Route Planning - Neutron and long-range plotting, trade loops, Road to Riches, and mining routes.
  - [ ] Elite Dangerous Ships - Ship and module specifications from a dataset lazy-queried at runtime and generated when new ships are detected.
  - [ ] Engineers - Who unlocks what (and who in the chain of unlocks), current unlock status, where each engineer is, and what they grade.
  - [ ] #102 Help with engineering - Blueprints, material sourcing, per-ship engineering plans, and on-foot suit and weapon engineering. At least two epics' worth, deliberately left vague until it is next.
  - [ ] Know the current community goals - Active community goals and their tiers. CGs the commander is not currently participating in can only be surfaced if an INARA API Key is available and correct.
  - [ ] Web Search - Live web search as a tool, when determined necessary by the LLM or when specifically asked to by the commander.

- [ ] **Phase 15 — Activity assistants**
  - [ ] Exobiology sampling - Genus, the required sample spacing for that genus, whether the Commander has moved far enough for the next sample, and what has already been scanned on this body. The spacing rule is the whole feature: it is the number nobody can eyeball and the reason a sample gets wasted.
  - [ ] Colonisation and construction tracking - Commodity requirements and delivered-so-far for an active construction site, sourced from the journal. This is where a large part of the current playerbase spends its hours, and the running arithmetic is exactly what a Commander should not be keeping on paper.
  - [ ] Prospector and core callouts - Material percentages from prospector limpet results and core asteroid detection, spoken in the ring where mining actually happens. Route planning gets the Commander to the ring; this is the part that happens after they arrive.

- [ ] **Phase 16 — Checklists**
  - [ ] TheApp keeps "The Ultimate" checklist - CRUD + complete/uncomplete and read checklist items by voice, with the panel reflecting changes live.
  - [ ] Per ship build planning/tracking - Checklists that belong to a specific ship and follow it, instead of the universal checklist.
  - [ ] LLM Ship AI may propose that a checklist item is done - Where the journal can tell, TheApp asks whether a checklist item is complete and marks it only after the Commander agrees.

- [ ] **Phase 17 — Session tooling and release polish**
  - [ ] Follow the live log, or stop following it - A floating jump-to-latest control with follow and scroll-lock, so reading history does not mean fighting new lines as they arrive.
  - [ ] Live log selectable and copyable - Read back what was said and copy it out. Free-form drag-selection across a continuously appending log is the hard part; rendering it as one read-only block is the way around it. If it is free due to the nature of the controls being used, then don't invent a workflow that's unneeded.
  - [ ] Copy log - One affordance for copying the entire session's log as currently filtered.
  - [x] Check for Updates on start - On start, when a new release is available, the user is given an opportunity to exit, install it, and restart.
