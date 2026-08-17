# Remediation 8

Reported 2026-08-17 against v0.23.0. Each item is checked off as it ships.

Remediation 7 shipped whole in [v0.23.0](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

- [x] **The Copy button is not vertically centred.** On the main page, beside the search box.
- [x] **"The ship is holding position behind us" is a claim d47 cannot make.** Heard as
  `ambient.srv`: *"The ship is holding position behind us. It will be there when you want it."*
  It may well not be behind us — the Commander may have turned around. "Nearby" is the better
  line, if the line is canned at all.
- [x] **"Show the VR panel" did not show it.** Answered *"The overlays are dark, Commander —
  nothing showing in the headset right now"*, which should instead have been the panel appearing.
- [x] **Scrollbars in VR should be usable with a controller.** Hovering one with the motion
  controller should highlight it, and it should be draggable and clickable. The aim must not have
  to be precise: hand jitter means "close enough" has to be good enough.
- [x] **The "Newest" button in VR does not appear to work.**
- [x] **Captions: which standard, and how long do they stay?** ~~Sometimes three lines arrive,
  and the caption clears the moment the voice stops rather than lingering.~~
  **Answered, and half changed.** The numbers are the broadcast/Netflix ones: 42 characters a
  line, two lines per caption event, 20 characters a second reading speed, and a dwell floored at
  5/6 s and ceilinged at 7 s. The **three** lines are d47's own choice and not Netflix's — a
  rolling three-line window, which is the roll-up form live captioning uses, distinct from the
  two-line maximum for one utterance. The dwell was the real complaint: it was timed against the
  last sentence alone, so a short one cleared the whole window in 5/6 s. It is now timed against
  everything still on screen, still inside the same 5/6–7 s.
- [x] **"Adaptive Encryptors Capture is full. 109 of 100."** Impossible. The count must not run
  past the capacity.
- [x] **NPC speech does not need captioning.** It is already on the Comms panel.
- [~] **An NPC message was spoken inside the cockpit, with no radio colour.** Heard as
  `message.npc: All I was doing was mining!` — *Spoken by Limp in ZzBnwUd5N5vZp018EN64
  (announcement)* — with none of the radio sound or static that a sender who is not aboard should
  arrive with.
  **Not reproduced.** The announce path passes the radio colour for every role that is not the
  ship's AI or the crew, and there is now a test that says so end to end — an NPC line comes back
  with different samples than the provider produced, and a D47 line comes back identical. What
  0.23.0 could not tell you is which way a given line went, so the spoken-voice log line now ends
  with `in the room` or `over the air`. If it says `over the air` and still sounds close, the
  filter is working and the argument is about how strong it should be.
