# Remediation 12

Reported from 2026-08-18 against v0.35.0, one item at a time. Each is checked off as it ships,
and **checked only once it has been seen to work** — a change that compiles is not a fixed item.

Remediation 11 shipped whole in [v0.35.0](CHANGELOG.md); its permanent record is that section of
the changelog, which is why this file is the current batch and not a growing archive.

- [x] **1. The module list should be grouped by type.** Hardpoints, Utility Mounts, Core Internal
  (including Armour), Optional Internal.

- [x] **2. Everything that is not outfitting should be off the list.** Named in the report:
  `PlanetaryApproachSuite`, `WeaponColour`, `EngineColour`, `StringLights`, `VesselVoice`,
  `ShipCockpit`, `CargoHatch`, `PaintJob`, `Decals`, `ShipName` — "or anything else that is not a
  part of Outfitting", which is the rule rather than the list.

- [x] **3. An empty slot is still a slot.** Nothing with a module in it was listed, so an empty
  hardpoint did not exist as far as the page was concerned.

- [x] **4. No `int` in a description.** The symbol was printed with its underscores taken out, so
  a power plant read as *int powerplant size6 class5*.

- [ ] **5. "Plan this slot" should offer the valid choices for that slot.** A list to pick from
  rather than a name to spell, and searchable, because an Optional Internal has hundreds.

- [x] **6. `TinyHardpoint` is a Utility Mount.**

- [ ] **7. An engineer's name should open that engineer.**

- [ ] **8. "Already yours" should read "Unlocked".**

- [ ] **9. "You can go and get these now" should read "Ready for Unlock".**

- [ ] **10. "Behind somebody else" should read "Requires Engineer Intro First".**
