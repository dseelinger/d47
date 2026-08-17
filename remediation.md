# Remediation 7

Reported 2026-08-17. Each item is checked off as it ships.

- [ ] **Backoff: logarithmic disables "Wait between attempts".** When Backoff is set to
  logarithmic, "Wait between attempts" should be disabled — it has no effect there.
- [ ] **"What the voice provider receives" is a hint, not body text.** It should appear only
  on hover over the label or the description.
- [ ] **Search boxes use the UI font size.** They currently render smaller than the rest of
  the UI.
- [ ] **Nav highlight does not track the settings scroll.** Scrolling the settings, the
  highlighted heading in the left nav does not line up with what is at the top of the
  settings area on the right. Possibly a zoom issue.
- [ ] **Ship AI callouts belong on the Conversation and Technical tabs.**
- [ ] **ElevenLabs switches Warden to German mid-callout.** Heard on
  `[Callout materials.milestone.adaptiveencryptors: Adaptive Encryptors Capture at 75 percent.
  88 of 100.]` — mostly correct, but part of it comes out German, not every time. Pin the
  language to English if the API allows it. Punctuation or digits may be the trigger; spell
  numbers out before sending to ElevenLabs. "88 of 100" is the suspect.
- [ ] **Do not announce entering a new channel when dropping out of hyperspace into a new
  system.**
- [ ] **Auto-honk does nothing.** It should hold the fire button for 5.3 seconds on entering a
  new system.
- [ ] **Window state is not restored.** If the app closes maximized it should start
  maximized, and on the same monitor where possible.
- [ ] **Only the first caption arrives, and it doubles the spoken line.** After the first, the
  panel appears headlocked but blank while the voice is speaking.
- [ ] **The VR big panel should carry the Settings tab**, unless there is a good reason not to.
- [ ] **All tabs should update in the VR big panel.**
- [ ] **Controls should be clickable in the VR panels.**
- [ ] **"Ray Gateway offers engineering" is a useless callout.** Every starport appears to,
  and if some do not, the absence is what would be worth saying — not the presence.
