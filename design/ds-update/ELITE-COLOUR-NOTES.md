# How Elite Dangerous actually uses colour — observed from 23 screenshots (Odyssey 4.4.1.1)

## 1. There are three neutral tiers besides orange

The d47 spec derives every colour from Accent. Elite does not.

| Tier | Where it shows up | Job |
|---|---|---|
| **White / near-white** | Screen titles (CODEX, KNOWN ENGINEERS, BASIC MINING). Names of things (DIDI VATERMANN, CMDR JOHN DEPARAGON, list-row names). Log rows (SESSION SHUTDOWN). **Chat message text and channel tag** (`[LOCAL] … No fire zone exited.`). Engineer log lines. | **Identity and speech.** What a thing is called, and what someone said. |
| **Light grey** | Labels in stat grids (CREDITS, SYSTEMS VISITED). "Specialisations". Engineer bio prose. Training dek. | Labels and secondary prose. |
| **Neutral dark grey ground** | Codex stat blocks. YOUR TOTAL DISCOVERIES. The disabled CANNOT DISEMBARK button. | Read-only data containers and disabled controls. Deliberately *not* orange-tinted. |

## 2. What orange does

- **Values and attributes:** credits, counts, timestamps, specialisation values, breadcrumbs, sub-labels.
- **Interactive surfaces at rest:**
  - A dim brown **filled** tile with orange text: list rows, BACK / EXIT / HELP, START TUTORIAL.
  - Filled, not outlined. Tiles are separated by 1–2px gaps.
- **Selected / focused:** a solid orange fill.
  - **Cockpit holo panels** (NAVIGATION tab, the GIRYAK row, the SQUADRONS tile) put **dark** text on the orange.
  - **Station / menu screens** (CONTINUE, the selected engineer, SESSION LOG) put **white** text on the orange.
- **Structure:**
  - A thin orange rule under every section heading, running full width.
  - A thin orange frame on Codex content panels.
  - A thin orange scrollbar.
- **Body prose, when it is the content** (Galnet, Pilot's Handbook): orange, sentence case.

## 3. The other hues are semantic, and mostly about relationship and ownership

| Hue | Seen on | Meaning |
|---|---|---|
| **Cyan / blue** | The CURRENT SYSTEM pin and name. The system pin in Navigation. The marker on your own carrier. CONFIRMED ✓ counts. Explorer and Exobiologist ranks. MY ARX. The Galnet selected-article bar. The Codex star selection fill. The shield ring. | **Yours / here / verified**, plus the exploration discipline |
| **Red** | The CQC rank. HOSTILE. The red diamond on locked engineers. The notoriety icon. Heat. | Hostile, locked, danger |
| **Green** | All of Powerplay: agent, merits, rank, and the pledged-power rule line. The nav radar arrow. | Faction colour. Context-specific, not global. |
| **Yellow-lime** | A highlighted Navigation row (Sacred Fire, your carrier). The radar range marker. | A secondary highlight / target |
| **White** | TRADE rank. RUMOURED counts. Structures on radar. | Neutral / unknown |

Codex discovery states read as a three-step ladder: **white** rumoured → **orange** reported → **blue** confirmed.

## 4. Type and layout

- Chrome is uppercase. Content prose (Galnet, handbook, bio) is sentence case.
- Title block: a white title with an orange breadcrumb under it, and an orange time/date at top right behind a thin bracket.
- There is a persistent footer action bar (BACK / EXIT, bottom left) above an orange rule.
- Glow:
  - Strong on the in-cockpit HUD and holo panels.
  - A soft halo on menu titles (TRAINING SIMULATIONS).
  - Crisp everywhere else.
- **Chat input is an outlined orange rectangle,** with the channel as a prefix: `TO [LOCAL]:`.

## 4b. Second batch (carrier services, shipyard, market, outfitting, crew, maintenance)

- **Grey means unavailable or nothing to do,** and it is a whole state family:
  - At rest: a dark grey tile with a white title and grey "Unavailable" (Redemption Office, Secure Trading).
  - Focused: a **light-grey** fill with dark text (Universal Cartographics, NO TRITIUM AVAILABLE, the complete COCKPIT repair tile).
  - Grey is the "can't act" twin of orange's "can act".
- **Yellow means stored, equipped or inventory:**
  - The local ship storage bar and 11/40.
  - The lock badges on stored ships.
  - ALL MODULES STORED and its count.
  - The equipped hardpoints: an olive tile with a yellow ✓.
  - The shipyard header band is tinted olive.
- **Cyan means ready.** READY TO DEPLOY is cyan text on a normal orange row.
- **Currencies each have a fixed hue:** CR orange, ARX blue, Merc coin magenta. MERC GEAR is a magenta-red row. SHIP NOT COMPATIBLE is red.
- **Speech:**
  - The NPC's name is small, orange, uppercase (LAILA BOYD). The spoken line under it is **white, sentence case**.
  - A long crew quote is orange sentence case, inside “ ” quote marks.
  - Helper prose is grey, sentence case, centred when it stands alone.
- **Inside a selected (orange-filled) row:** the name is white, labels drop to dark brown (CORSAIR, LOCATION, RANK), and values are white.
- **Two-tone titles.** Orange context word over a white subject: EQUIPPED / HARDPOINTS, STORED / ALL MODULES.
- **Modals:**
  - A 1px orange frame on near-black.
  - A white title with an orange dek, and the balance at top right.
  - An exit glyph tile at bottom left.
  - The action is a full-width dim tile with its cost on a second line (REPAIR ALL STRUCTURAL ELEMENTS / 50,524 CR).
- **Button weight: Elite has none.** In FILTERS, CANCEL / APPLY / CLEAR FILTERS / RESET FILTERS are four equal dim tiles. Emphasis comes only from focus.
- **Checkbox:** a filled orange square inside an orange square outline.
- **Gauges:**
  - A thin orange fill on a dim track, with the value right-aligned above it.
  - Power bars are segmented, with dividers.
  - Storage gauges are yellow.
- **Context tint.** The carrier header panel sits on a blue-tinted ground. That's a place colour, not a state.

## 4c. Chat (cockpit comms panel)

- Channel and sender sit on one line: `[LOCAL] [Sacred Fire BNH-T2F]:`, with the time right-aligned in orange. The message text below is white.
- Player lines (`[Erin Conway]:`) carry the name in orange. Your own line is `TO [LOCAL]:` with your portrait beside it.
- **When the input box has focus, its outline turns cyan.** At rest it's orange, and the typed text is orange.
- There's a character counter to the left of the input (232).

## 4d. Decisions (user, 22 Sep)

- **Four themes:**
  - **Elite default:** stock orange.
  - **Dark.**
  - **Light:** no scanlines, no bloom.
  - **Match my Elite colours:** the player's GraphicsConfiguration matrix, applied to the Elite base palette, so every hue transforms as it does in the game.
- **Button colours:**
  - Default (every button, including SEND): a dim orange tile.
  - Destructive: red.
  - No other emphasis.

## 5. Where the d47 spec is wrong against this

1. It has no white tier, so every word on the d47 screen is orange. That's what reads as "monochrome".
2. It has no neutral grey ground for read-only data.
3. Its status colours are anchored to hues keyed on severity. Elite's hues are keyed on **relationship**: yours (cyan), hostile (red), faction (green).
4. The spec's `hot` is orange+white. Elite uses near-pure white for identity, not a tinted orange.
5. Buttons: Elite's resting state is a dim filled tile, and "primary" is conveyed by focus (solid fill), not by a special shape. The spec's clipped-corner primary is d47's own invention. That's fine to keep, but it isn't an Elite idiom.
