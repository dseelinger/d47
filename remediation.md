# Remediation 9

Reported 2026-08-17 against v0.23.1. Each item is checked off as it ships.

Remediation 8 shipped whole in [v0.23.1](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

- [x] **The VR panels are flickering all the time.** A regression in 0.23.1, and a narrower form
  of it was already fixed once in 0.22.1. The aiming highlight added in 0.23.1 marks the surface
  dirty on every tick whether or not the light moved — and it is asked on every tick even when no
  ray is anywhere near the panel — so the whole panel is re-rendered, converted and handed back to
  SteamVR thirty times a second for pixels that did not change. That is the same condition 0.22.1
  removed from the carry path, except that this one never stops. `Illuminate` already knew whether
  anything had changed and now says so.
- [x] **Voices need more "radio", a tad more static, and certain voices sound more loud/present.**
  Three angles on one complaint: a treated line still sounded like it was in the room.
  - The passband was the telephony band, 300–3,400 Hz. It is the SSB voice channel now,
    400–2,700 Hz. The top edge does most of the work — presence lives between about 2 and 5 kHz —
    and drive went 1.9 → 2.6.
  - The static went up about 4 dB, of which 3 dB only pays back what the narrower band took away:
    less bandwidth is less noise power. The bare carrier lands near −31 dBFS against −33 before.
  - **"Loud" needed a rule changed.** The treatment restored each clip to the loudness it arrived
    with, which preserves whatever level spread the speech provider produced — so a voice that is
    simply hotter than its neighbours arrived hotter, and matching each line to itself cannot fix
    a difference that is between lines. There is a receiver AGC now: 26 dB of spread going in
    comes out inside 1 dB, on a target of 0.10 RMS that is the level real Edge Neural output was
    measured at, so the average voice does not move and only the spread collapses.

  Settled with the Commander before building: the extra static is for senders outside the ship,
  which is the existing over-the-air split, rather than for the Commander being on foot or in an
  SRV — d47 tracks both, so that remains available if it is wanted later.

**Both are tuning that has to be judged by ear.** The numbers above are reasoned and measured, not
heard: the band, the drive and the static are taste, and the AGC target is arithmetic on one
recorded measurement. If the link now sounds too narrow, or the static too present, the constants
are named and documented at the top of `src/D47.Core/Audio/RadioVoice.cs` and each one says what
it was and why it moved.
