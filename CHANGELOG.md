# Changelog

<!--
  Placeholders, not release history. The entries below record the file's shape — headings newest
  first, `## <version> — <title>` — and nothing about what shipped. `D47.Core.csproj` embeds this
  file as the `D47.Core.Changelog` resource for the About dialog; nothing else parses it.
-->

## 0.169.0 — No more drop-downs

The carrier's captain and tower can now be given their own names. Set them on the Fleet › Carrier
tab, where their voices moved to as well — the settings window's Carrier voices place is gone. A
named captain is addressed as "Captain {name}"; a named tower replaces "Tower Control" with the
name given. Leave either empty and the line reads exactly as it always has.

The Carrier tab's single Tritium row is now five: in the tank, in the carrier's own hold (marked
"counted", or "may be off" while a tritium trade order is open), in your own ship's hold, a total,
and a rough range — the tritium spent to jump follows the carrier-jump formula, based on a spansh
plot of Sol to Colonia, and ignores the carrier growing lighter as it burns fuel, which is why it
is called rough. The page now redraws when your ship's hold changes, not only when the
carrier's own figures do.

The Carrier tab now survives a restart: fuel, cargo, capacity, free space, jump range, balance,
docking access, decommission status and crewed services are folded from history the same way
location and identity always were, dated by the same "Figures as of …" line. Only a jump already
under way is still forgotten, since the tank drain since the last figure is not tracked either.

Every drop-down is gone. A choice of two to four is a row of segments; a longer list, or one that
can grow, is a stepper with an arrow either side. Nothing opens a list that can close under a VR
pointer, so every choice now works on the headset panel directly.

Moving through a choice no longer does anything costly. Picking a ship to plan opens a list you can
narrow by typing; pressing a row or an arrow key only highlights it, and nothing is planned until
you press Plan this hull or Enter, or say the name. The speech model and the local voice build now
stage the one you step to, and the one in use keeps running until you press the button that names
the cost, such as "Download 466 MB and use it".

The conversation bubbles take the HUD dress too: Saira prose instead of the monospace font, a faint
tinted fill and a chamfered corner on the tail side — Accent for the ship, Info for the Commander —
in place of the rounded corners and flat Accent fill they drew before. The Log File and Journal File
readings still draw monospace.

Your own carrier's routine traffic is now always addressed to you as its owner. Lines Frontier wrote
for a visitor, such as "Ensure to observe starport protocol during your visit, pilot.", are always
reworded, whatever the reword percentage is set to. When no model is set up, Personality is off, or
the model fails, times out or returns something unusable, d47 says its own line instead, for
example "Welcome back, Commander Seelinger." The log now records at Information level whenever a
line was spoken as written, and why.

The carrier's tower and captain no longer say the same sentence every visit. Each of the six lines
they speak as written — the ship secured, the captain's welcome, clearing the deck, the tower
telling the captain the Commander is inbound, the captain's answer, and a jump plotted — now draws
from a pool of six, cycling so two visits running never repeat one. When personality is on, the
model is asked to compose in the same situation rather than reword a fixed sentence, the same way
the docked welcome already did. Overheard tower traffic at a station or at your own carrier now
varies its subject and its opening beat too, instead of always being "clearances, pad assignments,
a telling-off".

The Fleet cards do too. A ship's hull picture now sits in a pure black cell with a rule beneath it
in every theme, the current ship carries its badge in the corner rather than a highlighted card, and
the name is drawn upper case in Saira Semi Condensed with the hull, where it is and what is planned
each on their own line beneath it. The card the other pane is drawing now reads by fill as well as
by border — 14% of Accent and a solid edge, against 5% and a rule for the rest. "Settings for this
page" is now a chamfered strip with a chip counting its rows, on the Fleet, Routing and Adventures
tabs alike.

The Settings page takes the dress too. The section tree's selected node carries a 3px Accent bar
and an 18% fill; the top-level areas draw upper case and tracked, the places under them in Saira
Semi Condensed, indented and inked in Accent. Rows are at least 60px tall and alternate on a 5%
Accent fill, and the reset glyph moves off the label and onto a square 40x40 button at the end of
the row. The protected and per-Commander tags carry a 60% Accent border and no fill, and a bound
key now reads in monospace. No row or tree node draws a rounded corner.

Every other hand-built card takes the dress as well — Utilities, Adventures, Learned Phrases,
Community Goal, Market, Route Plan, the Loadout notice and drag ghost, the macro, persona, switch,
coverage, debrief, logbook, lore, memory and audio-recorder windows, the entry-prompt card, and the
headset's own overlay card. Each draws square with a 5% Accent fill and a Rule border, or 14% Accent
and an Accent border when selected or open. The current row on Route progress and the next jump on
the plan result now carry the same 3px Accent bar the Settings tree uses. The macro, persona,
switch and picker windows no longer paint their whole background Surface; they take Background,
like every other dialog. The headset's zoom glyphs and the offscreen page's row buttons draw as
the kit button now, dropping the overrides that used to sit on top of it.

The Conversation page now says who is speaking. Every bubble but the panel's own note carries a
head: a chip naming the speaker — CMDR, D47, a persona's own name, or a role such as Tower or
Carrier for a callout with nobody specific behind it — a small tag naming the callout it came
from, and the time. Callouts spoken to you now join the conversation the same way a reply does;
invented chatter and a message you only overheard stay off it. The bracketed name in front of a
persona's reply is gone now that the chip carries it.

A checklist proposal now shows as a card in the Conversation, tagged proposal, with its summary
and its own Accept and Decline. Settling it — from the card, from the Checklist page, or by
saying "accept the proposal" — takes the buttons away and settles both: the card reads what
happened and its tag changes to proposal · accepted or proposal · declined, and the proposal
leaves the Checklist page's own list the same moment.

Every text box and number field takes the dress too: square corners, no fill, and a border that
turns Accent on focus in place of the rounded grey box Fluent draws by default. Page and card
titles — the Fleet, Engineers, Checklist and Adventures headings, the route plan cards, and the
Settings page and its section titles — now draw upper case and tracked in Saira Semi Condensed;
a line that reads as a sentence, such as a route summary or a prompt's question, keeps its own
case and takes only the typeface. The breadcrumb row now draws in Saira Semi Condensed as well.

The eight per-subsystem log levels move to the Log file page, as that page's own settings strip:
the default level, and a track below it with one row per subsystem — seven stops from None to
Trace, a readout, and a reset. A row with its own level draws in Accent; a row with none follows
the default, shown as a hollow marker that moves when the default does. The Diagnostics card on
the Settings page now carries only what is paused and hand-testing coverage.

SteamVR's own account of a panel's position and visibility now writes at Information only on the
first sighting and on a real change; the five-minute heartbeat that repeats an unchanged
description now writes at Debug, so a still panel no longer pushes real log lines off the page.

The microphone indicator moves to its own box above the ask line, styled to match the HUD kit —
a faint Accent fill, a rule that turns solid Accent while the gate is open with a glow behind it,
and a round dot in place of the microphone glyph. It stays on screen with the microphone off,
reading MIC OFF rather than disappearing, and it survives mini mode and the headset even though the
ask box does not. The ask button loses its own fill and rounded corners for the kit Button style,
and the ask box's placeholder now draws upper case and tracked, in Saira Semi Condensed, at heading
size; what you type keeps its own case.

The adventure editor no longer draws an unfinished draft in Danger. What is missing now reads as
one caution line — an Accent bar, a CAUTION label, and a sentence naming everything still needed,
such as "An adventure needs a key, a name, and at least one beat before it can be saved." A beat
whose place is not yet resolved carries the same Accent ink rather than red.

Elite, Dark and Guardian now carry a bloom and scanlines; Light carries neither. The tab strip's
unselected label, the selected tab's own fill, the selected list row's leading bar, and a primary
button's fill each carry a soft Accent glow, and a faint repeating line sits over the whole
window, on the desktop and in the headset alike.

The Elite theme's ground is now black, and the Conversation's pane is black with a faint Accent
tint, where it was grey. The ship's prose is a lighter amber, and a callout's key is plain
monospace text instead of a boxed tag. The time is monospace too. A short conversation now sits at
the bottom of the pane, beside the ask box, instead of at the top. In Ship, Log File and Journal
File are three segments rather than a stepper, and go back to the stepper on a pane too narrow for
all three. The line saying which path answered is no longer monospace. The scanlines keep their spacing at every
display scaling, so they stay visible at 150% and 200% and stay sharp at 125%.

Bloom is recalibrated. The unselected tab's label no longer glows — it sits on a tinted fill, and a
glow only shows where the area around it is dark. The primary button's fill, the selected tab and
the selected list row's leading bar now glow stronger in the headset than on the desktop window. A
new rule sits under the tab strip with its own glow, and the panel's outer edge now glows too.

Every window but the VR overlay now draws its own titlebar instead of the Windows one: the app
icon and title on the left, minimise, maximise and close on the right, in d47's own type and
colour. Dragging the strip moves the window, double-clicking it maximises or restores, and Aero
Snap, edge and corner resizing, and Alt+Space still all work as before. A window that cannot be
resized shows no maximise button, and the picker window — which has no taskbar entry to bring it
back from — shows no minimise button either.

An upgrade no longer reports settings it does not know. When a newer release first starts, it copies
`settings.json` to `settings.json.<old version>.bak` beside it and deletes any key the new build does
not use, without a startup message. A key typed by hand after that, or one left by a newer build on
a downgrade, is still kept and named.

A planned engineering slot no longer reads "I do not know your rank" for every entry on the Gap
page. The recipe is now resolved by the module actually in the slot — the plan's own module, or
what is fitted, when a blueprint name such as Heavy Duty belongs to more than one module type — so
a Shield Booster and Armour cost their own ingredients rather than whichever the table lists first.
Where a module cannot be told apart, the slot is left uncosted with a note saying so. The total now
uses the highest rank among the engineers who could actually craft it, and where nobody unlocked
reaches the grade yet, it is counted at the most rolls the grade can take, with a line saying why.

## 0.168.0 — Controls take the HUD dress

Buttons, toggle switches and the transcript tabs now draw d47's own look rather than the
Fluent default: a square rule border, an amber fill, and a squared, skewed tab rather than a
rounded one. The panel itself now carries a 1px rule frame with two corners cut on the
diagonal, on the desktop window and in the headset alike. Labels and tabs render in Saira Semi
Condensed and prose in Saira, both embedded rather than drawn from whatever font Windows
happens to have installed; the Elite theme's accent moves to a slightly lighter orange and its
info colour to a brighter cyan.

## 0.167.1 — Give the vertical scrollbar its own column

Every scrollbar in the desktop window, its dialogs and the VR panel drew over the right edge of
the content it scrolled, wider still once the pointer moved onto it. The content now stops at its
own column, and the bar shows at its full width whenever it is there — never wider, never
overlapping.

## 0.167.0 — Ask which engineers want a thing

One tool now answers across every engineer at once: who refers them, what earns the invitation,
what it asks for, and whether each of those is met. Asking "which engineers want sensor
fragments" now names both Chloe Sedesi and Professor Palin, rather than whichever one a single
lookup happened to find. Material lookups ("found at", "found in") no longer mix an engineer's
tribute into where a material is actually sourced — that question now belongs to the engineer
tool alone.

## 0.166.0 — Say what is left for an engineer

Asking "what is left for" an engineer, by name, now answers aloud with the prerequisites that are
not yet met — a referral, an invitation task, a tribute — each with d47's reading of it, such as
"120 of 200 handed over". Routed with no model turn, for every engineer in the directory. An
already-unlocked engineer says so.

## 0.165.2 — Custom checklist lines get an aligned checkbox

A custom line's tick is now a checkbox labelled "completed" instead of a switch, and sits beside
the reorder, Edit and Delete controls rather than above them. All five now share one vertical
centre on the card, whether or not the line is selected.

## 0.165.1 — Panel controls become dropdowns

The Checklist bar's scope button and the Ask for an adventure form's Reach and Length buttons are
now dropdowns. Picking a value no longer opens a chooser that takes the whole panel.

## 0.165.0 — Remove project ordering and checklist import/export

The Checklist tab's control bar no longer offers Order or Import/Export. Projects now sort
here-and-now first, then in the order they first appear on the list, with no way to set an order
of your own between them. A checklist can no longer be moved to another machine by file. An older
checklist file with a stored project order still opens; the field is ignored.

## 0.164.0 — Answer questions from the commander's career statistics

D47 can now answer from the journal's `Statistics` event — bank balance, combat, exploration,
mining, trading, crafting, exobiology and the rest of the sixteen sections. Ask for one section by
name, or for all of them, and each figure comes back under a readable name and in its unit —
credits, light years, or hours and minutes — dated to when the game last reported them.

## 0.163.0 — Delete completed checklist items in bulk

The Checklist tab's control bar now has a "Delete completed items" button, beside Import/Export.
It removes every Done line from the whole checklist — the Commander's own and every plan's — in
one confirmed press, ignoring the current filter and search. It is disabled when nothing is Done.

## 0.162.0 — Remove checklist tombstones

A plan revision that drops a line now deletes it outright, rather than leaving it on the list as
a muted "dropped by a later version of a plan" record. A line a later revision wants again comes
back as a new open line rather than one that remembers what it had earned.

## 0.161.0 — Focus the checklist on one engineer

The checklist filter now offers an entry for each engineer with unlock prerequisites on the
list, under its own "Unlocking an engineer" heading. Choosing one shows only that engineer's
invitation, tribute and referral lines — the same set "Add to checklist" put there — and the
filter falls back to Everything once the last of them is gone.

## 0.160.0 — Draw progress bars on engineer prerequisites

Each unlock prerequisite with a number behind it — a contribution total, a reputation reading, a
statistic, a rank — now draws a progress bar under its text, on the engineer detail page, the
Route page and the checklist item alike. A ceiling test (Uma Laszlo's reputation cap) fills fuller
the further under the ceiling the reading sits; a met line draws full; a line with no number draws
no bar.

## 0.159.0 — Add engineer prerequisites to the checklist

An engineer's Unlock Prerequisites now carry an **Add to checklist** control, on the detail page
and beside each engineer on the Route page, absent once every line is met. One press adds every
unmet line as a checklist item that tracks itself from the journal — a referral, the invitation
and the tribute alike — and never adds a line twice.

## 0.158.0 — Decide Hero Ferrari's meeting test from low conflict zone wins

Hero Ferrari's "Complete 10 surface conflict zones" requirement now reads as a floor on
`Combat.ConflictZone_Low_Wins`, the on-foot ground fight count, rather than standing undecided.

## 0.157.0 — Correct three Odyssey bartender sale counts

Kit Fowler, Yarden Bond and Wellington Beck's engineer prerequisites named the pre-4.0.18.08
sale counts. They now read 5 Opinion Polls, 5 Smear Campaign Plans and a total of 15
entertainment items, on the engineer detail page and the Route page alike.

## 0.156.0 — Drop route promotion, copy way-in systems

Every stop under an engineer's "The way in" now carries a copy glyph beside its system name, the
same control the engineer's own header shows. The "Put the route on my checklist" and "Put this
route on my checklist" buttons are gone from the Engineers tab, and the underlying tool no longer
answers "put that route on my checklist" or "promote this unlock". Adding individual unlock
prerequisites to the checklist replaces it.

## 0.155.0 — Merge Large cards into Hull pictures

Fleet › Ships had two switches for hull artwork and neither did what its name said. There is now
one: **Hull pictures**. On, cards carry their drawing, a ship's page shows the large picture and
plays the turntable, and missing files are downloaded. Off, fleet cards are text only, a ship's
page shows no picture and plays no turntable — even for a hull whose files are already on disk —
and nothing is fetched. Files already downloaded stay on disk; the setting only stops them being
shown. Flipping it redraws an open Ships page without a restart.

## 0.154.0 — Split the power bar by priority group

The Power gauge now splits into one span per priority group — the numbers Elite switches modules
off in, in order, when the plant cannot meet the draw — instead of one flat fill, with each
group's cumulative deployed draw labelled underneath. The bar also marks the 20%, 40% and 50% thresholds where a
damaged or destroyed plant's output falls. A fitted module's group shows read-only in its detail
pane; a planned module that still needs work gets a stepper for its group, 1 to 5. A ship boarded
before this shipped shows one flat bar and a note to board it again.

## 0.153.0 — Show planned megawatts on each slot row

A ship's slot rows now carry a megawatts figure: what the planned module draws with its blueprint
and experimental applied, or what is fitted where nothing is planned. A modelled figure — one d47
worked out rather than read off the game — carries the same `~` prefix and muted hue as the Power
gauge's own modelled reading. A slot too vague to cost, or a module that draws nothing, shows no
figure.

## 0.152.0 — Rename the active ship's badge, drop its redundant line

The active ship's card badge now reads "CURRENT SHIP" instead of "FLYING NOW". The line under its
name no longer repeats that it is being flown; it now shows the system alone, or the system with
its planned slot count, matching every other ship's card.

## 0.151.0 — Outline the slot the detail pane shows

The left-hand list on a ship's page now outlines the slot row whose detail is open in the right
pane, the same accent-coloured outline the fleet cards and the Engineers list already carry.
Pressing a different slot moves the outline to it; the on-foot item page outlines its open row the
same way.

## 0.150.0 — Name the slot atop its detail pane

The slot detail pane now opens with the slot's own name — "Large Hardpoint 1", "Utility Mount 1",
"Compartment 3 (size 5)" — above the existing "Fitted" and "Planned" headings, on ships and on
foot. Previously the pane opened with "Fitted" alone and said nothing about which slot it was.

## 0.149.0 — Remove fulfilled checklist items when set

A new Checklist setting, off by default, removes a derived item from the list once it is done
instead of leaving it ticked, and deletes a ship slot's plan once it is fully met — the tick and
the plan column both disappear, and the slot's page offers "Plan this slot" again. The "is done"
callout still speaks once before the item goes. Lines a commander writes by hand are never
affected, on or off.

## 0.148.0 — Adopt keeps derived checklist items derived

A checklist line written before a commander was known — before the first journal, or while
Elite was not running — now keeps its kind, key, source and hull when it is handed to the
commander who appears. Previously every such line arrived as an authored note, so d47 stopped
checking it against the journal and drew a switch beside it instead of a live verdict.

## 0.147.0 — The rescan sentence counts this commander's ships

"Rescan my journals" now says how many ships it found for the commander currently flying, matching
the "What is fitted, remembered" row above it. Previously it summed every commander the journal
folder had ever seen. Where the folder holds more than one commander, a second clause says how many
others there are and how many ships they have.

## 0.146.0 — A commander reset forgets the ships that came before it

Resetting a commander now clears the ships d47 remembers for that Frontier ID, both while it is
running and after a rescan. Previously a reset left every ship the commander had ever flown in the
"What is fitted, remembered" count and the Fleet tab, including ships from before the reset.

## 0.145.0 — A tab's own settings fit a narrow column

"Settings for this page" on the Fleet, Routing and Adventures tabs fits the width of its column, so
it no longer shows a sideways scrollbar when the column is narrow.

## 0.144.0 — The selected ship scrolls into view

Selecting a ship on the Fleet tab scrolls the ship list so its card is fully visible, when the list
is too long to show every ship at once.

## 0.143.0 — The Transcript copy button keeps its glyph

Clicking Copy on the Transcript tab and waiting past the "Copied" state now brings back the
clipboard glyph, not the words "Copy All".

## 0.142.0 — The privacy notice covers a downloaded copy

The donation privacy notice and the retention policy now state that a donation may be copied to the
holder's machine to be read, that the copy is one file per donation in one folder, and that it is
deleted once the object it came from has left the store. Nothing about what d47 sends, or when it
asks, has changed.

## 0.141.0 — Where to farm engineering materials

`find_material` now leads with two farming tiers ahead of the origins text: the general methods
that yield a material's kind, and the fastest hand-picked site for the top grade of its trade
group, with the trade that turns it into the grade actually asked about. A new
`get_material_farming_route` tool lists those sites in travel order from the Commander's own
position, what to collect at each, and the trades that follow — optionally narrowed to Raw,
Manufactured or Encoded.

## 0.140.0 — Ship name and wake words moved to their own capabilities

The settings page no longer mixes Persona and Listening rows under one "Its name" card: ship name
and Keep Ship AI name now sit in the Persona place, and the wake-word and corrections rows sit in
the Voice Input place, next to the rest of what Listening owns.

## 0.139.0 — Cost details by provider

The cost details window can now be asked the other way round: a picker under Running totals reads
one provider — ElevenLabs, Anthropic, whichever has been charged — down the same four windows,
models rolled up under that provider's figure and named in full, with no cap on how many are
listed.

## 0.138.0 — Engineers on the slot page

A ship slot with a plan now lists who can roll it, under the Planned block: Unlocked engineers
first, then Locked, each ordered nearest first with a copy glyph on the system. An unlocked
engineer ranked below the planned grade still shows, holding what they hold. On foot, a
modification slot lists the same way; a grade slot carries no engineer list. Before
`EngineerProgress` has been read, the list shows once, ungrouped, saying so.

## 0.137.0 — Neutron jumps give their distance

Each waypoint on a plotted route now says how far the leg to it is, alongside the jump count and
the distance left — on the Route tab's plan result, the mini panel, and the spoken answer. A plan
saved before this change shows no figure until it is plotted again.

## 0.136.0 — The resize cursor points the way it drags

In resize mode, the ray's cursor turns from a ring into a double-headed arrow over a resize
handle, pointing the direction that edge or corner drags. Held during a drag even if the ray
wanders off the handle it started on.

## 0.135.0 — Zoom and resize the headset panel from the keyboard

Add four system-wide hotkeys for the headset panel — zoom in (`Ctrl+Alt+=`), zoom out
(`Ctrl+Alt+-`), reset zoom (`Ctrl+Alt+D0`) and toggle resize mode (`Ctrl+Alt+S`) — reachable
while Elite has the foreground, the same as the overlay's own two keys. Entering and leaving
resize mode is silent; asking for it with the motion controllers off or no headset session is
spoken aloud, since a Commander in the headset has no transcript to read it from.

Resize mode also opens and closes from a ray. A glyph beside the panel's help mark enters it on
the full panel, when the motion controllers are on; once the handles are up, a bar along the
bottom offers zoom out, reset, zoom in and done. The mini panel carries the bar too, with no entry
glyph — voice and the hotkey are what open resize mode there.

## 0.134.0 — Reset the headset panel's position by voice

Add "reset the panel", which forgets where the on-screen headset panel was placed and puts it back
where a fresh install puts it: world-locked, at its default distance, resting in front of you on
the next active tick. Works with no headset session attached and no model configured. Each
placement heading in Settings — the panel you are looking at, the full panel, the mini panel —
carries the same reset as a glyph.

## 0.133.0 — The headset panel goes where you look

Add "place the panel here", which moves whichever headset panel is on screen to the middle of the
direction the headset is facing, as far away as it already was, turned to face you. Looking down
places it lower and looking up places it higher. The panel is left world-locked, its place survives
a restart, and no model is needed.

## 0.132.7 — Kokoro's rows hide unless a voice actually uses it

Fix the "Local voice" and "Local voice model build" rows in Settings, which showed beside every
other provider's rows once Kokoro was downloaded, whatever provider each voice slot actually used.
They now follow the same rule as a provider's key row: on screen while some slot — the ship's own
or any other — names Kokoro, and hidden otherwise.

## 0.132.6 — Microphone and speech recognition settings share one card

Merge the Microphone and Speech recognition settings places into one card, "Voice Input", holding
both as named groups. Both carried the same help topic already; there is no longer a reason to
look in two places for one topic.

## 0.132.5 — Ship names and common words draw no chip

Fix system-name chips drawing on a ship or module name that happens to contain one — "Caspian
Explorer" no longer draws a Caspian chip, and "Rail Gun" no longer draws a Gun chip — and on a
one-word table hit that is an ordinary English word such as Arm, Long or Union. A Commander
actually in one of those systems still gets the chip.

## 0.132.4 — What to say comes from the phrase book, not a guess

Add `find_phrase`, so asked what to say for a goal the model reads the model-free router's own
phrase book instead of guessing or denying a working phrase exists. Two phrases that reach the
same thing are named together, and a goal nothing matches gets told so plainly rather than being
told the goal cannot be done.

## 0.132.3 — No invented chatter where nobody lives

Fix Passersby and Hail chatter firing in systems with population zero: they invented other people
nearby, or someone hailing the Commander, a thousand light years from the bubble with nobody around
to have said it. Controller chatter is unaffected — it only ever fires docked, where a station or
the Commander's own carrier already justifies the scene.

## 0.132.2 — The Power gauge counts the cargo hatch and the hangars

Fix the Power gauge undercounting a build whenever `ModulesInfo.json` has not been read for that
ship: the cargo hatch draws power from no slot in the outfitting layout, so it was never visited,
and the Mk II vessel hangars and free fighter hangars had no figures in the table at all. Both now
count.

## 0.132.1 — The Fleet page states the ship's real figures

Fix the Fleet page, `list_ships`, and the `ShipLoadout.MaxJumpRange` doc comment describing the
game's `MaxJumpRange` as a full-tank figure. It is not: it is unladen mass plus one jump's fuel,
the same figure `JumpGauge.Best` already reports. A full tank or a laden hold both jump shorter
than the number shown.

## 0.132.0 — The route panel no longer takes callouts down with it

Fix a crash on the tick thread when a route was plotted while the Routing tab's mini panel was
live: it dropped that tick's journal events, including the arrival that ends a long-jump
countdown, so the long-jump callout fired at the full duration on jumps that landed early.

## 0.131.0 — Crew on the intercom, not just the line

Address a hired pilot by name and they now keep the line the way the carrier's captain does:
follow-up questions reach them with no name needed until you say "that's all" and its kin, name
your ship AI, or name a different pilot. Each pilot keeps their own conversation rather than
sharing the ship AI's, and the ship AI hears each exchange as one it overheard rather than one it
had. A pilot is never offered a tool.

## 0.130.0 — The carrier captain on the line

Ask "carrier report", "carrier status", "how is my carrier", "carrier services" or "carrier fuel"
for the fleet carrier's fuel, cargo against capacity, balance, jump range, docking access, booked
jump and every service with who staffs it — apart from "where is my carrier", which still answers
with the system alone.

Start a question with "Captain" and the captain of your fleet carrier answers it, in the captain's
voice, from the carrier's figures and the galaxy tools. Follow-up questions go to the captain without
the name until you say "that's all", "that'll be all", "thank you captain", "dismissed" or "carry on",
or start a question with your ship AI's name. The ship AI hears those exchanges and can refer to them
afterwards. With galaxy search on, a carrier more than 500 light years away is out of range: the ship
AI tells you so and gives the distance. Beyond 250 light years the captain's line weakens as the
distance grows: more static, a narrower voice, short dropouts, and lost words marked with an
ellipsis in the panel and the caption. The ship AI hears only the words that came through.

## 0.129.1 — One tool surface for every provider

On Claude Opus 5, Opus 4.8, Opus 4.7, Fable 5, Mythos 5 and Haiku 4.5, through Anthropic's own
endpoint, the model is now given every tool it may use and searches them for the ones a request
needs, instead of receiving only the tools for the mode you are in. The list no longer changes when
you board the SRV, step out on foot or turn key presses off, so a mode change no longer re-bills the
cached prompt. A control that does nothing in your current mode is still refused, with the reason,
when the model calls it. Claude Sonnet 5, the OpenAI providers and custom Anthropic endpoints keep
the mode's list. If Anthropic refuses tool search for a model, the request is sent again with the
mode's list, and later turns on that model use the mode's list for the rest of the session.

## 0.129.0 — Settings one area at a time

Every two-state control in the app is now drawn as a switch — the checklist's Goals and Include
Partial Grades toggles, the engineer directory's two filters and a pinned blueprint, the route
planners' loop and pad checkboxes, the market page's three filters, the logbook's date range, Help
Improve's two toggles, the key editor's reveal, an adventure's "This ship only", and the two
keyboard swaps — in place of a ticked square or a button that changed colour to show a state.

The Settings page shows one area at a time: the nav lists every area, but only the selected one's
sections, and the page opens on that area's own title and sentence above its cards. Following a help
card into a setting, or reopening the page on a setting you were last reading, switches to its area
first. A search still draws matches from every area at once, each behind its own area's name, and
clearing it goes back to the area you had open. Below the width the nav collapses at, a dropdown of
area titles takes its place.

A section whose rows are all hidden behind "Show every setting" now keeps its card, with a "Show N
more" button in place of its rows; pressing it draws that section's own hidden rows without opening
any other section's. Following a help card into a setting still unfolds only the section it lands on.

A search now also matches an area's name, a section's name or one of its search terms (Microphone
answers to "mic" and "ptt", Its voice to "tts", Speech recognition to "stt" and "whisper", Privacy and
egress to "telemetry" and "data"), and a named group's title or help — each shows every row under it,
marked the same way a row's own words are. A row that lives on a tab rather than the Settings page —
Fleet › Ships, Routing › Community Goal, Adventures, or the Checklist tab's own row — now shows up
under "On other tabs" with a button that opens the tab and root it belongs to, its own strip open
where it has one.

Motion controllers default on for a new install — a settings file that already exists keeps
whatever it held, so an existing install turns them on with "motion controllers on". The Motion
controllers row no longer carries a warning pill or sentence.

A line spoken within thirty seconds of the last time the Commander was addressed by name — from any
voice, ship AI, callout, carrier captain or tower — now drops "Commander" from what is said aloud.
Captions and the flight recorder still show it; only the audio changes.

A callout, carrier line, canned message or story beat with an in-character rewording is now put to
the model only half the time by default; the rest of the time it is spoken exactly as written, with
no model call. Ambient remarks are unaffected and are still reworded every time.

Eight optional Guardian voice treatments for the ship AI — Cylon, pitch down, an octave-down layer,
chorus, metallic resonance, ring modulation, glitch and reverb — are now switches under Its voice,
all off by default and global to every core. Turning any on reaches the ship AI's turn replies, its
own callouts and a persona's introduction or return; the crew, the carrier and every over-the-air
voice are unchanged.

A Test button under Guardian voice plays a line through whichever of the eight treatments above are
on, and never bills a provider: it plays free where the provider is free, a free sample where the
provider has one, an audition already paid for this session where there is one, or a bundled
stand-in voice.

## 0.128.0 — Settings arranged by area

Fleet › Ships, Routing › Community Goal and Adventures now carry a "Settings for this page" strip of
their own — Rescan my journals and Hull pictures on Ships; the Inara API key and the week's turn on
Community Goal; Notable places for adventures on Adventures. Closed by default, and remembered open or
closed on its own account. Those rows are no longer on the Settings page.

The Settings page, in the window and in the headset, is arranged in six areas: Voice and hearing, The
ship's AI, Speaking up, Acting on the game, Screens, and Privacy and this install. The nav lists each
area with its sections beneath it, and a section gathers the rows for one job from wherever they were
declared, so When a turn fails is under The ship's AI and its reset puts back only its own rows. Which
sections you had closed, and where the page was left, start again from the defaults, except for
Persona, Callouts, Privacy and egress, and Diagnostics.

## 0.127.0 — Open the new plan after plotting

Plotting the Neutron Plotter, Road to Riches or a trade run now opens that plan's result page rather
than leaving the Commander to press "Show most recent" themselves. A second plot while its result page
is already open — from either surface's card, or by voice — updates the same page and its breadcrumb in
place with the new plan, rather than showing a stale one under a stale heading. A plot that finds
nothing to record leaves the surface on the form, as before.

## 0.126.0 — Surveyed biology from Spansh

Arriving in a system now gets a spoken callout naming the bodies Spansh has surveyed biology on, when
their species together reach the biology threshold: "Spansh has surveyed biology here: 3 b at 15
million, 3 c at 12 million." It needs galaxy search, which is off by default, and while it is on each
jump sends the system's address to spansh.co.uk; the Privacy and egress section says so. A body it
names is not called out again when you scan it. On by default, with its own toggle beside the other
callouts.

## 0.125.0 — What a body could hold

Scanning a landable body the FSS has counted biological signals on now gets a spoken callout when its
biology could reach 10 million credits or more: "3 b could hold up to 18.2 million in biology: Stratum,
Bacterium." The figure is the best case from the species the body's conditions admit, one per genus;
once a surface scan names the genera, the line gives a range over those. Said once per body. On by
default, with its own toggle and threshold beside the other callouts.

Asking what is on a body now says the same estimate. An FSS-only body names the possible genera and a
best case; a surface-scanned body gives a low–high range over the genera it actually found. Either way,
the exact value still waits on a sample — the genus is all Elite ever names before then.

Both now drop a species that cannot occur in the Commander's own galactic region, so a body near the
Scutum-Centaurus Arm no longer gets credited with a variant that only pays out elsewhere.

## 0.124.0 — Supercharge

Stepping onto a body for the first time now gets a spoken callout: "First footfall on Smojue
EB-O d6-37 AB 1 b." Directive 47 infers it from the body's scan and the `Disembark` that follows,
since Elite writes no event for a first footfall. On by default, with its own toggle beside the
other callouts.

Arriving at a system nobody has sold data on now gets a spoken callout: "Undiscovered system.
Nobody has sold data on this star yet." It fires on the arrival star's own autoscan, so a
`NavBeaconDetail` scan carrying the same flag in a populated system stays silent. On by default,
with its own toggle beside the other callouts.

"Plot next neutron jump", "plot next riches stop" and "plot next trade stop" plot the next stop on
a stored plan through the galaxy map, by voice, with no model needed. The next stop is the first
one after the furthest reached whose system is not the one the Commander is already in, so a
trade plan's first stop — the station it was plotted from — is never plotted back to. The answer
names where the stop sits on the plan, and a trade stop's answer also names the station.

A stored plan's result page now ticks off the stops the Commander has reached, muted, with the
row after them marked as the next one to copy. The page redraws as the reached stop moves,
without needing to be reopened, on the desktop window and in the headset. This applies to all
three plan kinds; on Road to Riches and trade plans, a tick means the Commander got there, not
that the bodies are mapped or the cargo is traded.

Mini's Routing tab now shows the last neutron route plotted, as a list of waypoints with the next
one marked, instead of the three planner forms. The next waypoint comes from `NavRoute.json` where
it names one, otherwise from the Commander's current system; where neither matches, the list is
still drawn with nothing marked. The full panel is unchanged, and the other three Routing roots
keep drawing their full-size pages in mini.

A route warning no longer fires on a leg that a neutron star or white dwarf supercharge would
clear. The strand check now measures the jump beyond an unscoopable star against the boosted
range for the fitted frame shift drive — six times normal range at a neutron star and three at a
white dwarf for the Mk II overcharge booster, four and one and a half for any other hyperdrive.
Where the boost clears the leg, the urgent warning is replaced by a quiet line naming the
supercharge; where it does not, the warning still fires, quoting the boosted range.

## 0.122.0 — How do I

A window left at full height comes back at full height. Restoring a remembered size used to
apply the same 90% margin as a fresh window's opening size, so a window snapped to fill the
screen reopened noticeably short of the taskbar; the margin now applies only when there is
nothing remembered.

The desktop window no longer shrinks to mini. It was always the full panel now: the setting, the
`Ctrl+M` hotkey, the spoken phrases and the shrink mark are gone. The headset's own mini panel and
the flat overlay are unchanged.

"How do I plot a course", "how can I get my ship engineered", "how would I turn off silent
running" — these now land on the one feature they mean, without the model. Where the words fit
two or three features equally well, d47 asks which; where they fit none, a configured model
answers instead, and with none configured this falls back to the top of the spoken map rather
than saying it has no way to work it out.

A wording d47 has learned now has a page: Settings → Learned phrases lists every one, newest
first, said next to the phrase it runs. Press Forget, or say "forget 'set focus on elite'", and it
stops matching — it goes back to being offered as a near miss rather than run silently.

With no model configured, an utterance nothing else can answer no longer just says it has no way
to work it out. If it shares words with a feature, d47 names that area and offers its list; asking
for the list gives the same drill "what can you do" would. With a model configured, an unmatched
utterance still reaches it as before.

Settings → About's Version row now has a Check for updates button, so a release that comes out
while d47 is running can be found without restarting. It works whether or not the startup check is
on, and says what it found: an update, that this build is already the latest, that GitHub could not
be reached, or that a local build has no release to compare against. Finding one adds an Install
row beside it, doing what Update now on the home tab already does — either button reaches the same
install, so only one runs at a time.

## 0.121.0 — Did you mean, and what can you do

A command missed by a word is no longer handed to the language model to refuse. Say "set focus on
elite" and d47 asks "Did you mean 'set focus to elite'?"; say "yes" and it does it. Where only one
harmless command fits once "on" and "to" are read as the same word, d47 just runs it. Anything that
presses a key in Elite is always asked first, and anything other than a yes or a pick drops the
question.

"What can you do" is now a drill rather than one long answer: d47 says how many areas there are and
names them, then asks which one. Answer with an ordinal, a name, or enough of one, and it goes a
level deeper, down to one feature — its own sentence, a phrase or two that works, and the panel
page it lives on where it has one. Anything else drops the question and answers normally.

Say "yes" to a "did you mean" offer and, once it has run, d47 asks whether to remember your own
wording as another way to say it. Say "yes" again and it does — say it that way next time and it
runs with no offer at all. It never asks twice about the same wording in one session, and never
asks at all for a one-word utterance, one that is already a declared phrase, or one already
remembered for something else.

## 0.119.0 — Ask how to get it

A new question, "how do I get X", answers for anything named — a ship, module, suit, hand weapon,
modification, material, ship-locker item, or commodity — rather than only the three things the
model could already ask about separately. It says how the thing is acquired first, then names the
nearest shipyard, market, mining ring, or material trader where finding one is part of the answer.
A rare good and a tech broker unlock are answered from the table alone, with no search needed.

## 0.117.0 — Engineering, seen two new ways

A ship gated behind an Empire or Federation naval rank now says which rank it needs — the
Imperial Cutter asks for Duke, the Federal Corvette for Rear Admiral — and the Cobra Mk IV says it
is Horizons early-adoption only rather than reading as an ordinary shipyard buy.

The checklist can now be filtered to what a pinned blueprint can finish, wherever you are docked —
not only where the engineer's own workshop is. Pin a blueprint on an engineer's page in the
Engineers tab ("A blueprint is pinned with them"); the filter then keeps the lines that engineer
covers and your rank with them already clears, dropping anything that would still need a trip to
the workshop. It sits under its own heading in the Show chooser, and does not go away when you
leave the system an engineer works in — the point of a pin is that you do not have to be there.

An engineer's unlock prerequisites now read your rank, reputation, career statistics and
contributions where the game states one of those as the test, instead of only what your journal's
`EngineerProgress` says about them directly. Where the reading falls short, the criterion shows it
— "Last reported 4,558", "18 of 25 handed over", or a stale reputation reading's date — on both the
engineer's own page and the Route page, in the SteamVR overlay as well as the window.

## 0.116.0 — Voices that suit who is speaking

A station under Imperial control now speaks in a British-sounding voice, where your speech
provider offers one — read off `StationAllegiance` on the `Docked` event. Most stations name no
allegiance at all and are unaffected; Federation and Alliance stations keep the voice they always
did.

Listening to ElevenLabs voices in the voice list no longer costs money. The play button now plays
ElevenLabs' own free sample of each voice. A second button beside it has the voice say your core's
line, billed as before, and the price above the list now refers to that button. A voice with no
sample keeps the single paid button.

With personality on, the game's own NPC lines are now said in other words rather than word for
word each time. A trader's "I strongly advise you against this." is reworded in a trader's voice,
a pirate's in a pirate's, and station traffic stays a procedural notice; each keeps its attitude,
so a threat is still a threat. Only lines Elite itself wrote are reworded — a message another
player typed is still read exactly as sent — and the comms panel still shows Elite's wording.
With personality off, they are said as written.

Speech settings now has a "Reset every voice to its pairing" row. Pressing it puts every core back
to the voice d47's pairing pass chose for it, and the carrier captain and tower back to speaking in
the ship AI's — undoing any voice you have since hand-picked. It covers every voice provider you
have used, not only the one selected now, and a core with no recorded pairing is paired again
rather than left as it was. A first press only asks; a second press within a few seconds is the
confirmation.

## 0.115.0 — Resize the panel where it sits

Place mode (`Ctrl+Alt+M`) now offers a resize as well as a move. While the border is up, the
overlay strip takes drag handles on its top edge, its bottom edge and all four corners — a corner
drags both dimensions, an edge drags one. A drag stops at a minimum rather than going to nothing,
and the new size is remembered the way the position already is.

The headset panels resize too. Say "resize the panel" and the panel on screen shows a handle along
each edge; point a controller at an edge or a corner, hold the trigger and pull. The panel changes
shape in the room and its content reflows into the new space rather than being stretched. Say
"stop resizing" or press the grip to leave the mode. The mini panel's resolution is now a setting
like the full panel's, so mini describes how much is shown rather than a fixed size.

The panel's zoom can be stepped from inside the headset: "zoom the panel in", "zoom the panel out"
and "reset the panel zoom" change how large the content is drawn without moving the panel's edges.

## 0.114.0 — Timers and alarms behind a startup flag

Clocks, timers and alarms are off unless d47 is started with `--utilities` or with
`D47_UTILITIES=1` set, and the switch lasts for that run only. Without it there is no Utilities
tab on the desktop, in the headset or on the overlay strip, no timer or alarm tools, no "what time
is it" or "cancel the timer" phrases, and `alarms.json` is not read. d47 also does not know the
date in that case, and says so when asked rather than working one out.

## 0.113.0 — Copy any system name the panel shows

A system name could be copied from the Ships whereabouts line, the Community Goal table, or a
route being planned or flown — three different mechanisms, each reaching the clipboard its own
way, and none of the three reaching the Carrier, Sourcing, Market or Engineers pages at all. Every
system name the panel draws now carries the same copy glyph, wired through one seam, so it is
available everywhere a system name is shown — desktop and headset alike, since the shared control
never depends on the desktop window's own clipboard.

The Journal reading now draws the same glyph beside any line whose event names a system — an
`FSDJump`, a `Docked`, a `Location` — and none beside a line that names none.

The In Ship reading now draws a copy chip under any turn that names a system, one per name in the
order it was said, whichever side of the conversation said it and in the headset as well as on the
desktop.

A drag on the In Ship page that starts in one bubble and ends in another now keeps both ends and
every bubble between them, rather than only the bubble it started in. Copy puts the whole
selection on the clipboard, d47's lines and the Commander's own both included, in the order they
are shown.

## 0.112.9 — Controls that draw their state

The Engineers tab's Colonia filter was a button that rewrote its own label — "Hide the Colonia
eight" or "Show Colonia again" — so the only way to read which way it was set was to read the
offer to change it, and the setting was gone at the next launch.

It is now two checkboxes, **Hide the Colonia eight** and **Hide on-foot engineers**, both
unticked by default and both remembered between sessions. Either can take rows off the Directory
and the Route page's ranked list without touching how the ranking itself is worked out or what the
spoken answer says.

The expand and shrink marks on the mini panel's way out were four corner brackets at a size where
the arms were shorter than the pen was wide, so the mark read as a plus sign. Both are now diagonal
arrows — heads outward for expand, inward for shrink — drawn on a finer pen and at the size the
rest of the mark family uses.

The Ships index carried the one switch in the app that did not follow the settings page's own
pattern: a `ToggleSwitch` with its label as `Content`, which stacks the word above the knob and
wraps it onto a second line. It now reads **Large cards**, the label as a `TextBlock` beside a bare
switch with no `On`/`Off` text, on one line in both the desktop panel and the headset.

## 0.112.8 — An engineer's row says who it opens

An engineer whose referral gates somebody else drew no line about it, however much the Commander's
plans needed the other end. Marco Qwent's own page said nothing about the fact that reaching grade
3 with him is what opens Professor Palin, Lori Jameson and Chloe Sedesi.

An engineer not yet at the referral grade a plan-named dependant needs now says so, on the
directory row and the engineer's own page: "grade 3 opens Professor Palin, Lori Jameson and Chloe
Sedesi." An engineer whose referral is already met, or whose dependants no plan wants, says nothing
extra.

An engineer's page and the spoken engineer report no longer carry a "Reputation rises fastest by"
line. It stated the same mechanic for every engineer — crafting and selling raise it — and the base
above it already named the one activity that was specific to them.

An engineer's page named nowhere which of the Commander's plans it was the answer to, though the
directory counted them. It now lists the planned work that names them, before the cost of reaching
them — "Planned work" once unlocked, "What unlocking them buys" while they are not, capped at eight
with an "and N more" tail.

## 0.112.7 — Lore is remarked on once a week, not once a day

A Commander whose home system is where they spend most sessions heard the same lore remark every
day. The quiet period between remarks on the same system is now a week by default, and a row under
Lore in Settings — "How often a system's lore is worth repeating" — holds the number of days.

## 0.112.6 — A squadron acceptance is spoken, naming the squadron

A squadron accepting the Commander's application went unsaid, the same as any other journal event
d47 did not recognise. It is now a narrated line naming the squadron: "Application to GREYBEARD
DELTA approved."

## 0.112.5 — A confirmed plot says the jump count and the distance

A plotted course said only that it worked: "Course plotted to Colonia." The Commander still had to
open the map to find out whether that was four jumps or four hundred.

The confirmed sentence now carries both: "Course plotted to Colonia. 43 jumps, 22,000 light years."
The jump count is the jumps still ahead of the Commander, not the hops in the route file. The
distance is left out when any leg of the route has an unknown length. A route the Commander plots
by hand, or a plot that could not be confirmed, is unchanged.

## 0.112.4 — A ship with no Fuel Scoop is warned when its fuel will not finish the route

Flying without a Fuel Scoop silenced every route fuel warning, because each one was about a star
that could not be scooped. A Commander whose only way to refuel is a station heard nothing when the
route was longer than their fuel would last.

Fuel on a route is now its own warning. It works out how many jumps the fuel in the tank covers,
from the fuel each jump has actually used this session, and warns when the route is longer than
that with nowhere to refuel inside it: "Fuel warning. At 2.1 tonnes a jump there is fuel for 4
jumps, and the route has 9 jumps left." A ship with no scoop hears nothing about stars. A ship with
a scoop hears how far away the nearest scoopable star on the route is.

The line that said an unscoopable next star left too little fuel for the jump beyond it is replaced
by this warning. The warning about an unscoopable star before a jump that is out of range is
unchanged.

## 0.112.3 — A sold ship's build is deleted with its checklist lines

Elite reuses ship ids: sell a ship and the next one bought can take its number. A build pinned to
that number stayed, and its checklist lines were spoken, ranked and drawn as "That ship id now
reports a Panther Clipper Mk II, and this plan was written for a Sidewinder" — a plan for a ship
that no longer exists, with nothing to do about it.

A build is now deleted when its ship is sold, or when its ship id reports a different hull, which
catches sales made while d47 was closed. Every checklist line the build put on the list is deleted
with it, and nothing is said. The plan cannot be recovered.

Notes you wrote yourself on a ship's list are no longer deleted when the ship is sold, and the sale
is no longer announced as "I cleared 2 items from your list".

A rank gate on the checklist no longer teaches the engineering rank mechanic every time an item
becomes blocked. "Grade 5 cannot be crafted at rank 3 with Marco Qwent at all" is still said and
still shown; "Rank rises by working with them, and it compounds" no longer follows it, on the
twentieth unlock or the first. The Checklist page still shows a blocked item with its reason, and
asking still answers — only the unprompted callout is silenced.

## 0.112.2 — The search box shows up on a filterable tab's first visit

Opening Checklist, or another tab whose panes filter, from a cold start left the search box
missing until the tab was left and revisited. The row was decided from the tab's drill strip
before the strip had drawn its panes for the first time, so the question "does this tab filter"
was asked and answered "no" a layout pass too early, with nothing to ask it again once the strip
had finished drawing.

The strip now says when it has drawn, and the panel asks the question again when it does. The
search box is there on the first look.

## 0.110.49 — Turning off a chat or chatter switch now takes effect immediately

Seven rows in Speech, "Other voices" and the personality half of the two chatter gates were read
once, when d47 built its callouts at startup, and never read again. Turning "Include NPC chatter"
off, or any of the six channel switches next to it, changed nothing until the next launch. Turning
personality off silenced neither "In Ship chatter" nor invented "NPC chatter" without a restart.

All of them now read the live settings, so a switch in the panel is obeyed on the next tick.

## 0.110.48 — A version number is read aloud instead of throwing on ElevenLabs

Reading a version like 0.112.0 out loud threw, because the number reader split it on the first
full stop only and then read the rest, "112.0", against a digit table meant for single digits.
The sentence was dropped and a warning logged instead of being spoken.

Every dot-separated group after the first is now read digit by digit, the way a decimal fraction
already is: 0.112.0 is "zero point one one two point zero". Asking Directive 47 for its status
now gets an answer on ElevenLabs.

## 0.110.47 — A stick that is plugged in is no longer reported missing at startup

Windows takes a moment to report every game controller it has, and D47 waits for that list to stop
changing before reading it. The wait was not counted properly: the fifteen reads a bound stick gets
to turn up in were being spent on reads taken during the wait, which report nothing whatever is
plugged in. Whether D47 then warned that push-to-talk was bound to a controller that is not here
came down to which side of that moment the reads happened to fall.

The stick is now read only once the list has settled, so the fifteen reads are fifteen real chances.
A stick that is genuinely absent is still reported, and the push-to-talk key on the keyboard is read
on every tick as before.

## 0.110.46 — A SteamVR registration that keeps being refused says so once

D47 registers its controller bindings with SteamVR as soon as the headset session is up, and tries
again on every tick until SteamVR takes them. When SteamVR refused for a reason that did not clear,
that retry rewrote the six binding files on disk and wrote the same warning to the log ten times a
second for as long as the session lasted.

The retry is unchanged, because the refusal often does clear. The files are now written once, and each
distinct reason is logged once. A different refusal is logged once more, and the registration
finally going through still says that controller input is on.

## 0.110.45 — A part of D47 that keeps failing is paused rather than retried forever

Almost everything D47 does runs off one loop that ticks ten times a second. A part of that loop
which threw on every tick was called again on the next one regardless: one installed build threw
25,000 times in an hour, wrote 250 error lines, and ran the part of its work that came before the
throw ten times a second for the whole hour.

A part that fails ten times running now stops being called, and is tried again once a minute until
it succeeds. It resumes by itself when the fault clears, so a window that has since opened or a
file that has since appeared no longer needs a restart. One part failing on and off is untouched:
the ten have to be consecutive.

A paused part is a feature that is not running, so it is named rather than left to be inferred.
Ask **what's your status** and the report names it, the Diagnostics card carries a row naming it
while it is paused, and the log at shutdown says whether it was still paused when D47 closed.

## 0.110.44 — Jumps left is read from the plotted route

Asked how many jumps were left in a route, D47 answered with how many had been made this session.
It kept its own note of where the ship was heading, taken from the journal's `FSDTarget` event, and
treated arriving as spending that note. On a plotted route Elite targets the *next* hop about seven
seconds into the hyperspace tunnel, before it writes the arrival, so arriving threw away a target
the game had already set. The route line then said nothing at all while the Commander sat in a
system mid-route, and the only jump count in front of the model was the session's completed jumps.

The spoken answer and the Route Progress panel now read the same file, `NavRoute.json`, so the two
cannot disagree. Both name the next hop and the jumps left from where the Commander is standing;
both say the Commander is not on the route rather than counting the whole route as still ahead when
they have jumped off it; and neither mentions a route when none is plotted. The remark on a long
crossing names the system being flown to rather than the hop after it, and a system name read aloud
states the class of the star the Commander is actually sitting next to.

## 0.110.43 — Target the next system in the route by voice

Say **next system**, *target the next system* or *target the next system in route* and D47 targets
the next system on a plotted route, the way `TargetNextRouteSystem` does on its own binding. It
refuses rather than pressing a key with nothing to target: with no route plotted, or standing on
the last hop, it says so instead of acknowledging a key that would have done nothing.

## 0.110.42 — Ask D47 to request docking

D47 could take a ship out of a station and could not bring it in. Say **request docking** — or
*request permission to dock*, *permission to dock*, *ask for docking*, or *take us in* — and it
walks the left panel's contacts tab and asks the station for permission. Elite binds no action for
requesting docking, so this is a menu walk, and a blinder one than taking us out: the status file
says a panel is open and never which tab is showing or which row is selected. D47 therefore
confirms by the journal instead, and says whether a request actually went in rather than assuming
the walk worked. It refuses before pressing anything unless the ship is undocked, in normal space
and has a destination selected, and *take us in* refuses with no docking computer fitted, since
that phrase promises an approach D47 does not fly. It has its own settings row beside the other
compound commands.

## 0.110.41 — D47 follows the Default Device as it moves, not just where it started

Left on "system default", the microphone and the speaker resolved which device that was once, at
startup, and held it for the rest of the session. Switching on a headset mid-session moved
Windows' own default without d47 noticing, so it kept listening to and speaking through whatever
had been default before. Both now register for Windows' default-device notifications and re-open
once a move settles — one re-open for a burst of several, and only on whichever direction is left
on "system default"; a chosen device is unaffected. A line already playing finishes rather than
being cut off mid-word, unless the device playing it has itself disappeared, in which case it
stops rather than continuing on a device no longer there. A microphone move that lands
mid-sentence says it did not catch that rather than dropping the words silently.

## 0.110.40 — D47 now listens on and speaks to the same Windows default

The microphone and output device rows followed different Windows defaults: the microphone opened
the Communications device, the speaker opened whatever NAudio's own default happened to be, and
neither matched the Default Device a Commander sees first in Sound settings. Windows only splits
Communications off from the Default Device once a headset or voice-chat app is involved, which is
why the two roles agreed on some machines and named different devices on others. Both rows now
follow the Default Device, and the settings page names the device and which Windows row it came
from. **On an existing install left on "the system default", the
microphone d47 opens may change after this update** — check the Microphone row if a Commander
notices d47 has gone quiet or stopped hearing them.

## 0.110.39 — A journal history walk that did not finish is no longer reported as an absence

The walk over older journals ends one of three ways: it finishes, it throws, or it is stopped when
d47 quits. Only the first of those has read any history, but the carrier, ship list and loadout
answers treated a walk that threw as one that had finished, and said no carrier appears in any
journal read. They now say the history was not finished reading, the same as while it is still
running. Quitting while a walk was running also left it running, so it could still be writing the
file of heard names and logging after the log had been closed; quitting now stops the walk at the
next journal file and waits for it to stop.

## 0.110.38 — The window no longer waits for the journal history to be read

Four walks back through older journal files — the fleet, the loadouts, the carrier and the place
names — ran inside startup, before the window could be built. On a data folder that has already
recorded how far it has read, none of them takes noticeable time. On one that has not, which is
every first run of a fresh install, the names walk reads every journal in the folder: 13.7 seconds
on one measurement, and 26 seconds on the launch that prompted this. The walks now run once the
window is on screen, and what they recover is folded into the game state on the next tick, without
displacing anything the current session's journal has already said. While a walk is running the
panel shows "Reading journal history" with the seconds so far, and asking about the carrier, the
ship list or the fleet loadouts answers that the history has not been read yet rather than
reporting an absence it cannot yet know about. `get_app_status` names the walk and its state.

## 0.110.37 — Startup no longer waits for the speech model, and a press made while it loads is kept

Loading the speech model ran on whichever thread asked for it: the startup path, and the window's own
thread on every change to a listening setting. On the maintainer's machine `medium.en` took about 1.2
seconds, the largest single step in a 3.5-second start, and picking a different model or microphone in
Settings froze the window for the same time. The load now runs off that thread, so the window appears
without waiting for it and Settings stays responsive while it happens. A press made before the load
finished used to be captured and answered with "I have no speech model loaded to understand it"; it is
now held and transcribed once the model is loaded. The microphone indicator says "Loading model..." while
that is true, rather than reporting that it is ready.

## 0.110.36 — The published build no longer decompresses itself on every launch

The single-file publish compressed every assembly into the exe and decompressed all of them into
memory on each start, and none of them were precompiled, so every method Avalonia and d47 touched
before the window appeared was JIT-compiled cold. Publish now leaves the assemblies uncompressed
and ahead-of-time compiles them. The exe is larger on disk; nothing about the install, the update
payload or the update check changes.

## 0.110.35 — The log says where startup time goes

A launch measured on the maintainer's machine took 46 seconds, 35 of them inside two stretches that
logged nothing while they ran, so what was slow could only be guessed at from what each stretch ended
with. Every step of startup is now timed — the settings and stores, each of the four journal
backfills, the priming tick, the audio output, the transcriber, the echo canceller, the Elite window,
the bindings, the controllers, the capability registry, the speech model and the three settings
applies — and a step that took 250 milliseconds or more writes a line saying how long it took. The
first and last lines of a launch also state how long the process had already been running, so the
time before d47 had anywhere to write is measured rather than inferred.

## 0.110.34 — Closing SteamVR no longer crashes d47

Closing SteamVR while the headset overlays were up crashed d47, with nothing in its own log after
the line saying the session had ended. The ninety-hertz thread that places the aim ray was
inside an OpenVR call when the tick thread shut the session down, and the memory it was reading had
already been freed. Ending a session now waits for that call to finish, and every call after it
returns without reaching OpenVR at all.

## 0.110.33 — Every engineer prerequisite draws a checkbox, on the Route page as well as the detail page

The engineer detail page marked each unlock prerequisite with a character — `✓`, `·`, `?` — read out
by nothing. Those become a drawn box, checked, empty or dashed, coloured accent, muted or informational
so a met one, an unmet one and one nothing d47 can read decides are each visibly distinct, and named
for a screen reader. The Route page's five ranked engineers now list the same prerequisites for the
engineer the chain ends at, under the summary sentence and above the existing route lines.

## 0.110.32 — An engineer no longer appears as a prerequisite for themselves

The engineer detail page's **What it takes** section used to end with "Liz Ryder works for you",
stating an engineer working for you as one of the conditions of that engineer working for you, when
**Where you stand** above it already answers the same question with more detail. That line is gone,
and the section is retitled **Unlock Prerequisites**, which reads correctly whether the engineer is
unlocked or not.

## 0.110.31 — The Engineers summary counts the game's three states, not d47's reading of reach

The line at the top of the Directory, the Route page, and the spoken answer to "who should I unlock
next" used to read "X of 38 unlocked, Y within reach" and a clause about planned work waiting on
somebody not yet unlocked — d47's own judgement of what could be acted on today, which counted an
engineer who has never been mentioned in the journal the same as one already invited. It now counts
the three states Elite itself writes: "9 of 38 unlocked. 16 in progress. 13 not started." The waiting
clause is gone; with it, nothing distinguishes a plan blocked on an engineer from one blocked on
materials, which is a real loss and may come back as its own line later.

## 0.110.30 — The Engineers directory counts modules left to finish, not rolls already done

An engineer's count on the Engineers tab used to add up every planned blueprint and experimental
effect they could in principle roll, including ones already applied and ones on a module they
cannot actually grade — Selene Jean showed 63 planned things while her own work was already done,
eight of them "Heavy Duty" on Shield Boosters, which she cannot touch at all. The count is now
modules still to engineer that the engineer could finish: a slot with something already applied at
the planned grade contributes nothing, a blueprint name shared by several module types is matched
against the module actually fitted, and an engineer who can only apply an experimental effect
without also being able to roll an outstanding blueprint on the same module is not counted for it.
The row reads "1 module" or "43 modules" rather than "1 planned thing wants them".

## 0.110.29 — A number in a system name is read as part of the name, the same way on every voice

"HIP 3269" was read as "thirty-two sixty-nine" by the local voice and "three thousand two hundred and
sixty-nine" by ElevenLabs, while Edge Neural, OpenAI and Cartesia were sent the digits and said
whatever they chose. Numbers are now written out as words once, before any provider sees the line, so
every voice says the same thing: a run of four or more digits is read digit by digit — "three two six
nine" — and a run of three or fewer keeps the casual reading, so "Col 385" is still "three
eighty-five". A number carrying a unit, a decimal point or a grouping comma is a quantity rather than
a name, and is left as it was.

## 0.110.28 — A long system name survives two dropped letters; a short one stops out-ranking it

"Set course for Shinrarta Dezhra" heard as "Shinrata Desra" was offered "Ra" — a real system whose
name happens to sit inside the misheard one — while the Founders' World, three edits away against a
budget of two, never came up. A catalogue name under four letters now has to be the whole word rather
than merely appear inside what was said, and a long compound name is allowed one more edit than the
length-scaled budget would otherwise give it. Separately, a failed tool call's arguments are now in
the log line alongside it, since the call itself is unrecoverable once the turn has ended.

## 0.110.27 — A misheard commodity name is offered its nearest match, not just refused

Asking for "Leavian Brandy" got told there was no such commodity, twice, because commodity lookup
never used the near-match table that already helps with ships, engineers and modules. It now checks
a name that matches nothing against the cargo and rare-cargo ledgers and, if one is close, offers it
by name — "Did you mean Lavian Brandy?" — instead of asking to be repeated. A name near a ship
material rather than a commodity is left alone, since a material is answered by a different tool.

## 0.110.26 — Asking about a rare good names its one station and what is on offer there

"How much Lavian Brandy can I get at a time?" used to be answered from the ship's cargo capacity,
because nothing else was available to answer it with: a rare is sold at one station, and a search for
markets near you finds nothing. D47 now names that station and system from a table it ships — no web
access needed — and reads how much is on offer there from the market's own last report, given with
its age. That figure, not the hold, is what limits a visit, and it moves with the station's economic
state, so a boom can put far more on offer. Where there is no recent report, the station is still
named and the quantity is said to be unknown. Asking where to sell one still sweeps the radius, since
that is a question the market index can answer.

## 0.110.25 — Three more throttle positions, "military thrust" among them

D47 could only set the throttle to zero or full. Elite's own preset already binds 25%, 50% and
75% to the keyboard, so "military thrust" (also "military power", "throttle to seventy-five",
"seventy-five per cent") now presses `SetSpeed75`, and the same phrasing pattern covers 25% and
50%. Unbound, each refuses by name and presses nothing, the way every other action does.

## 0.110.24 — A command that worked is acknowledged rather than narrated

"Gear down" answered "Pressed L for the landing gear" — the sentence written for the log, read out
loud. A command that works now answers in two or three words, varying rather than settling on one:
"Aye.", "Done.", "Acknowledged.", "Aye, gear down." The key that fired is still in the log and in
the result the model reads, because which binding actually went in is the first question on any
"it did not do it" report. Refusals are unchanged — they still name the action, the reason and the
binding — and so is the reply to a command with nothing to do, which corrects you rather than
acknowledging you.

## 0.110.23 — A failed plot is heard as the tool wrote it, not retold by the model

`plot_course` and "set a course and take us out" report a plotting attempt in one of three exact
sentences: plotted, no route appeared, or the outcome could not be told. Asking a model to repeat
that back left room for it to add something that was not true — offering to check the spelling, for
instance, when the tool never said the name was wrong. Those sentences are now spoken as written and
end the turn there, the same as any other tool result marked to be relayed.

## 0.110.22 — Asking about a stored ship reads its modules instead of refusing

"How is the Panther Clipper outfitted" used to answer with cargo, jump range and the like, then
say there was no way to read out its module fit unless you were aboard it — even though every
module of every ship you have ever sat in is kept in `loadouts.json`, the same file the checklist
already reads. Naming a ship now answers from the loadout last seen for it, dated so a ship
refitted since is visibly stale, and a ship you own but have never boarded says so rather than
describing a different one. And asking with no ship named now answers about the one you are
flying from memory after a restart, before Elite has written a fresh `Loadout` for it — the same
gap #337 closed for engineering, one level down.

## 0.110.21 — A command's refusal and the next callout no longer run together

A key-binding refusal spoken in answer to a command could be followed straight away by an
unrelated ambient callout — a High Grade Emissions report, a jump-remaining line — with no
separation between them, so the two read as one continuous sentence about the same subject when
they were coincidence. A short silence now separates two spoken lines from different groups, but
only once the first one has actually finished; a line cut short by Silence, a dropped group or an
alert does not wait for anything.

## 0.110.20 — Asking a second time shuts the dock hands up as well as the first

Chatter was cut off when you opened the microphone, but not always when you opened it again. If the
previous turn ended without an answer, nothing was spoken and no sound cue played, so the loop was
still showing that turn's outcome when you asked again. Opening the microphone then let the loop
return to idle in the middle of shutting chatter off, and that let chatter straight back in for the
whole of the second question. The loop now moves before the audio does, so a follow-up silences the
exchange exactly as the first question did.

## 0.110.19 — Talking to Directive 47 stops the dock hands talking

Ask something while a dock hand is mid-sentence and the answer used to queue up behind the whole
exchange, arriving after you had moved on. Invented chatter is now kept apart from everything else
that gets spoken, and opening the microphone cuts the chatter line mid-word and drops the rest of
the exchange. It goes on the microphone opening rather than on the answer arriving, because
transcription and the model together take several seconds and a four-line exchange finishes inside
them. Chatter then stays shut off until the answer is done and the loop is quiet again, because the
next speaker in an exchange is written and voiced while the first is still talking and would
otherwise arrive a second after you cut them off. Nothing else is dropped with it: a message the
game sent you, a callout you switched on and the sound cue in front of the answer all keep their
place. And only you do this — an unprompted line of Directive 47's own still waits its turn.

## 0.110.18 — A refused tool call is not tried again in the same turn

A plot request refused by the online gate (#408) was retried by the model five times in seven
seconds, each retry speaking "Plotting the course to Kamitra" again and the model's separate
replies running together with no space between them. A tool call is now run once per turn for a
given name and arguments; a repeat is told plainly that it was already tried and answered the same
way, without running the tool or speaking its announcement again. Text spoken across two tool
rounds in one turn is now always separated by a space.

## 0.110.17 — The Quartermaster stops reaching for a ledger word for everything

A weekend of logs showed the Quartermaster's accounting vocabulary leaking into lines that had
nothing to do with cost — a TV show got "decent margins", a session greeting rotated through six
different synonyms for "ready" ("reconcile the ledger", "balance the ledger", "run the ledger" and
more). The lexicon is now scoped in the prompt to cost and worth, dropped elsewhere, and the
session greeting is told plainly not to dress up "ready" in a core's own words unless something
real is worth naming.

## 0.110.16 — Passers-by chatter stops running one script

Overheard exchanges between a dock hand and a courier were one script with the names changed: a
third of them opened on the Commander's ship blocking a pad and closed on "not my problem, I'm
just here for the run". The cast, the topic and the opening beat now rotate deterministically per
exchange, and the worn lines the logs measured are named off limits in the prompt itself.

## 0.110.15 — Invented chatter at your own carrier knows whose deck it is standing on

Parked on your own fleet carrier, overheard chatter already knew whom to cast as the tower
controller and the captain but told the scene nothing about what your owning the place means to
the people in it. Now the model is told how your own crew regard you — deference or an easy
grumble made to you, not surprise — and how a visiting pilot at somebody else's carrier can react:
surprised, careful, or embarrassed to have been overheard. It colours the scene rather than
narrating it: ownership is the subject of at most one exchange a visit, the rest may only show it,
and none of this fires until you are actually on the carrier's deck.

## 0.110.14 — A System Authority vessel near your own carrier gets the owner treatment reliably

A System Authority vessel's canned line, heard while the Commander shared a system with their own
fleet carrier, was sometimes read out as an ordinary stranger's message instead of the reworded,
deferential line that setup calls for. The check for "does the Commander own a carrier here" was
installed as a side effect of an unrelated periodic pass over game state, so a line arriving before
that pass had run was always judged as "no" — including every line spoken in the first seconds
after Directive 47 starts. The check now reads live game state directly from where the callout is
built, so it is never standing in for "nothing has told me yet".

## 0.110.13 — NPC chatter stops inventing a dock in supercruise and normal space

Overheard chatter talked about landing pads and dock queues while the Commander was crossing a
system in supercruise, or flying in normal space nowhere near a station — an invented exchange
about a ship sitting on a pad it was nowhere near. Two fixes: no chatter at all in supercruise or
hyperspace, where nobody is near a ship to overhear; and the model is now told, in words, whether
the ship is docked or in normal space, with a normal-space scene for the passers-by and hail
pairings that has no pads, dock hands or queues to draw on. Docked scenes are unchanged, and a
controller exchange asked for while docked is dropped rather than composed if the Commander lifts
off before it is written.

## 0.110.12 — "Which of my ships has the best jump range" is answered rather than refused

Asked which of their ships with at least 24 tonnes of cargo space had the best jump range,
Directive 47 said it could only see the loadout of the ship being flown. That was never true:
every `Loadout` Elite writes is kept in `data\loadouts.json`, and cargo capacity and maximum jump
range are both in it. What was missing was a tool that could read it. There is one now. It lists
every ship as it was last seen fitted — cargo, jump range, unladen mass, fuel, value and rebuy —
and can narrow to ships above a hold size and rank them by jump range or cargo, so the comparison
is arithmetic on figures the game reported rather than a guess from hull specifications.

Two things the answer always states, because both are true and neither is a reason to refuse: the
jump range is the maximum on a full tank with an empty hold, which is the right figure for ranking
ships against each other and the wrong one for a laden run; and every ship carries the date it was
last seen, because one refitted since you last boarded it is remembered as it was. A ship you own
that no `Loadout` has been read for is named rather than left out, so a ranking is not mistaken
for the whole fleet.

## 0.110.11 — "Which engineer is in this system" finds them, instead of a lore lookup's empty answer

Asked who the engineer was in Leesti, Directive 47 reached for system lore — the wrong tool, which
knows nothing about engineers — and reported its empty result as "no record of an engineer based
in Leesti." Didi Vatermann works there; the engineer table always had them. `find_engineer` now
takes a `system`, named or left out for the Commander's own, and answers who is based there or
says plainly that nobody is. "Engineer in this system," "who's the engineer here" and "which
engineer is here" reach it without a model in the loop.

## 0.110.10 — An experimental effect confirms against its symbol, not its spoken name

"Super Capacitors" never confirmed "Super Capacitor" on a shield booster, because Elite localises
the display name and the checklist was comparing that name rather than the unlocalised symbol
underneath it. Every experimental effect whose localised spelling differs from the recipe table's
was unconfirmable the same way, and the checklist line read the module as carrying the wrong
effect rather than as one it could not check. The comparison now joins on the symbol, and a symbol
the table does not recognise is reported as unconfirmed rather than as a conflict.

## 0.110.9 — Routing and Fleet reach the headset, and a drawn keyboard takes a spelled value

The keyboard a controller ray opens on a text box in the headset heard nothing: every character of
a system name or a carrier figure had to be pointed at. Both drawn keyboards — that one and the
panel's own — now take a spoken value. Say each letter as its word, NATO style, and the keys are
pressed in order: "alpha bravo seven done" types `ab7` and commits, in one breath, because Done is
a key. So are delete, clear and cancel, and digits are said either way.

It is not a mode. Every utterance is tried as spelling first; if any word is not a key, nothing is
pressed at all, the board names the word it could not take, and the whole utterance lands in the
field as a value — so "Shinrarta Dezhra" is said rather than spelled, and "Alpha Centauri" arrives
whole. That gives the headset's board dictation as well, which it also did not have. Spelling is
live only while a keyboard is drawn, so "bravo" in conversation still means what it says.

Ask "how do I spell something", "the phonetic alphabet" or "what is the word for K" and Directive
47 answers from the same table the keyboard parses, without asking a model.

Spelling was the last thing Routing was waiting for. Where you are going was the window's alone:
the plan forms wanted a keyboard, and the headset had none. It has had a drawn one since 0.23.1 and
spoken values into it since 0.25.0, so the reason had been false for a while, and now it is gone
outright. Plan, Progress, Course, Market and Community Goal are all on the headset panel, in the
same place in the tab strip the window puts them, each remembering the reading it was left on the
way every other tab does. A ray press on a form's box opens the drawn keyboard, so a destination is
said whole and a jump range is spelled key by key. Copy and Copy and plot in the galaxy map work
from the headset for the reason they always did: it is the same PC's clipboard and the same key
sequence, and Elite is what is in front of you.

Your ships, their builds and your carrier only drew in the window too. Fleet was withdrawn from the
headset panel during the redesign in 0.37.0 and left out again when Checklist came back, on the
reading that a three-level drill ending in a search field was a bigger surface than one list of
short rows. That no longer holds: every row it drills to is a button or a switch a controller ray
already presses, and every value typed into it goes through the same voice-first prompt the rest of
the panel uses.

Fleet now sits after Transcript on the headset panel, in the place in the tab strip the window puts
it. Ships, Suits, the gap between the two and the carrier all draw there. A ray press opens a ship
and then a slot, the mode switch takes a press, and three grip-backs from a slot row return you to
the root. The hull picture's step buttons do what the mouse wheel and drag do in the window. The
tab redraws from the headset's own tick, for the same reason Engineers does: the ship underneath
you can change without anything else happening first.

Ctrl-dragging one slot onto another stays a desktop convenience. The headset surface has no
pointer-moved path for that gesture to ride on, and no new gesture is being added before 1.0.0, so
a module is copied there by the row's own action.

## 0.110.8 — The empty Checklist names what it is filtered by, and the tab strip decides its own width

"Nothing on your list matches that." never said what "that" was. On the full panel the query box
and the scope button sit beside the message, so the gap was survivable; in mini neither is on
screen, and the query still narrows the list from wherever it was typed. The message now names the
query, the scope, or both, from the same two values the filter itself reads — "Nothing on your list
matches 'limpets'.", "Nothing on your list is in A ship's build.", "Nothing in A ship's build
matches 'limpets'." — and the empty state grows a Clear filter button, since mini has no search box
to clear the query from.

A window that opened narrower than the tab strip's words drew the words anyway, and only
collapsed to marks once the Commander touched the edge — the collapse decision ran only from a
resize, so a window that opened at a narrow size and sat still never made it. The decision is now
made once against a settled measurement before the window is first shown, so it opens in the state
it would otherwise only reach after a drag.

## 0.110.7 — The checklist follows the ship, and Sourcing opens on the headset

Filtering the checklist to the engineer in this system now answers from the system the ship is in.
The page listened to the list, the proposals, the filter and the goals, and to nothing that says
where the Commander is — so jumping into an engineer's system left the empty message on screen with
that engineer's work behind it, and left the partial-grades checkbox and the rank line describing
the system just left. The page redraws when the system changes, and on nothing else: docking,
dropping out of supercruise and every other event inside one system redraw nothing.

Sourcing — where to buy what a build still needs — is now the Checklist's second root on the
headset. It was withheld there because the carrier figure is typed and the headset had no
keyboard of its own; a press on the carrier box opens the drawn keyboard the panel already has,
and the figure is written once when the board closes. The desktop window is unchanged.

## 0.110.6 — Settings rows that open a window

Every Settings row that opens a second window — memories, the debrief, notes, the logbook, the
audio recorder, coverage, ship cores, macros, switches, and the arrow that clears a stored key —
now refuses a headset press and says “Not currently supported in VR” on the panel.
A ray press used to reach the handler, which would have tried to open a dialog over a window that
is never shown. The desktop window is unchanged.

## 0.110.1 — What can I do?

The ask box's empty text is now a question you can actually type. It read "Ask D47 something";
it reads "What can I do?", and on a Commander's first run still offers "where am I" and
"what's your status" beside it.

## 0.75 — placeholder

**The release history for this version was not recovered.** This heading exists because the
checkout is missing its changelog and the build embeds one. See the comment above.

## 0.1.0 — placeholder

**The first release's entry was not recovered either.** Present so the file carries the
newest-first ordering the real one has.
