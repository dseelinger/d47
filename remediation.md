# Remediation 7

Reported 2026-08-17. Each item is checked off as it ships.

- [~] **Backoff: logarithmic disables "Wait between attempts".** ~~When Backoff is set to
  logarithmic, "Wait between attempts" should be disabled — it has no effect there.~~
  **Dropped 2026-08-17.** It does have an effect: `RetryPolicy.WaitBefore` multiplies the base
  wait by `log2(retry + 1)`, so the first retry waits exactly the base under either shape and
  the later ones are that base decelerating. Disabling the row would have frozen a live value.
- [x] **"What the voice provider receives" is a hint, not body text.** It should appear only
  on hover over the label or the description.
- [x] **Search boxes use the UI font size.** They currently render smaller than the rest of
  the UI.
- [x] **Nav highlight does not track the settings scroll.** Scrolling the settings, the
  highlighted heading in the left nav does not line up with what is at the top of the
  settings area on the right. Possibly a zoom issue.
- [x] **Ship AI callouts belong on the Conversation and Technical tabs.**
- [x] **ElevenLabs switches Warden to German mid-callout.** Heard on
  `[Callout materials.milestone.adaptiveencryptors: Adaptive Encryptors Capture at 75 percent.
  88 of 100.]` — mostly correct, but part of it comes out German, not every time. Pin the
  language to English if the API allows it. Punctuation or digits may be the trigger; spell
  numbers out before sending to ElevenLabs. "88 of 100" is the suspect.
- [x] **Do not announce entering a new channel when dropping out of hyperspace into a new
  system.**
- [x] **Auto-honk does nothing.** It should hold the fire button for 5.3 seconds on entering a
  new system.
- [x] **Window state is not restored.** If the app closes maximized it should start
  maximized, and on the same monitor where possible. **Note:** the monitor half is new and
  tested; the restore itself already worked headlessly and had no test. If it still opens
  un-maximised on the real build, the remaining suspect is the event ordering guarded in
  `WindowPlacementMemory.SampleWhenSettled`, which no headless test can tell apart.
- [x] **Only the first caption arrives, and it doubles the spoken line.** After the first, the
  panel appears headlocked but blank while the voice is speaking.
- [x] **The VR big panel should carry the Settings tab**, unless there is a good reason not to.
- [x] **All tabs should update in the VR big panel.**
- [x] **Controls should be clickable in the VR panels.** **Not yet confirmed in a headset.**
  Tabs and buttons are covered by headless tests that press the real surface; a combo box or a
  text field on the settings page takes the gesture and does nothing useful with it.
- [x] **"Ray Gateway offers engineering" is a useless callout.** Every starport appears to,
  and if some do not, the absence is what would be worth saying — not the presence.
- [x] **Materials announcements stop after a trader.** They work for a while, but after
  filling up at Jameson's Crash Site and then offloading at a Materials trader, no more
  announcements arrive until the app is restarted.
- [x] **Record which voice was used in the log file**, whenever something is spoken.
- [x] **Named NPCs each use a different voice, per name, for as long as you are in the system.**
  The assignment was already there and tested; the pool it drew from was not. `Cast.Pool` read
  ElevenLabs' *accent* label as a locale and kept only what started with `en`, so a 473-voice
  account became a pool of one and every NPC shared it.
- [x] **A woman's name gets a woman's voice; everything else a man's.** Elite records no sex
  anywhere in 914 journals, so this is a shipped list of 692 given names rather than anything
  derived. `tools/scan-npc-names.py` reports what is still unmatched, most heard first.
