# Phase 37 — Routing you can see

The plan of record for a phase that is **not built**. Written 2026-08-19 and committed 2026-08-20,
after a start that was rolled back on the Commander's instruction with other priorities arriving —
so this is a design that has been thought through and costed, waiting for somebody to pick it up,
rather than a description of anything that exists.

`list.md` has no Phase 37 entry yet. Writing one is the first act of building this, not a
precondition for keeping the plan.

---

## The number

**Phase 37, and not 35.** Phase 35 (*A core per ship*) shipped as v0.38.0 and Phase 36 (*Trade
routes that d47 works out*) as v0.39.0, both on 2026-08-19. CLAUDE.md freezes 1–21 and 23–36,
retires 22 permanently, and says new phases are appended — so 37 is the next free number and
there is nothing movable below it. Recorded here because the phase was first asked for as "the
next phase, 35", and the correction is the kind that is cheap now and silent-and-expensive later.

## The phase in one sentence

A **Routing** tab on the desktop panel — **Plan · Progress · Course**, three modes of one tab in
the idiom Transcript already uses — because the three routing capabilities d47 already has can
only speak their answers, and a route is the thing a voice is worst at and a screen is best at.

## Why it is worth a phase

Everything below already exists and is already reachable by voice. The phase adds no tool, no
service, no network call and no capability: **39,918 of 40,000 bytes of tool surface are spent,
and this phase spends none of them.** What it adds is a surface for answers that do not fit in a
sentence.

The clearest case is a plotted route. `routes.md` records that a Sol-to-Colonia plot is 131
waypoints and 168 jumps, and that reading that aloud is not an answer — so the spoken form gives
totals and the next handful and says *ask again from further along*. That cap is correct for a
voice and pointless for a screen.

## The three modes

| Mode | What it shows | Where the data comes from |
|---|---|---|
| **Plan** | The three planners — jump route, Road to Riches, trade run — as forms with results | `RouteCapability`, gated on `Knowledge.GalaxySearch` |
| **Progress** | The route being flown, all of it | `NavRouteReader.Current`, the file Elite writes locally |
| **Course** | A system name onto the clipboard, and the best-effort galaxy-map drive | `NavigationCapability` |

Decided with the Commander on 2026-08-19, against two alternatives: one segment per planner
(which leaves route progress homeless), and a Plan/Progress/Trade split (which separates trade
from the two planners it shares a gate and a service with).

## Surfaces

**Desktop only**, decided the same day. `VrPanelSurface` makes no `EnableRouting` call, which is
the entire implementation — a tab appears only where a host furnishes it and `PanelView.Tab`
declines to select one nobody did. VR parity is a someday-maybe (CLAUDE.md).

A later remark from the Commander is worth keeping with the decision: **if VR ever gets this, it
may be view-only.** That is Progress exactly, and it is the mode that most wants a headset — the
one you read while flying the route. So `EnableRouting` should take its roots as flags rather than
furnishing a fixed three, and giving the headset Progress alone becomes one call with no
restructuring. Cost of building it that way now: nothing.

---

## The six items

### 1 — A Routing tab, and the strip grows to seven

`PanelTab.Routing` in the enum, `PanelView.EnableRouting` calling `Furnish` with its roots, one
call from `MainWindow` and none from `VrPanelSurface`.

**Placement: after `Transcript`, before `Checklist`.** Enum order is strip order, and the tab is
persisted nowhere, so placement is free — nothing serialises a `PanelTab` by name or by number,
which was checked rather than assumed. A route is read while flying, and the tab a hand reaches
for in flight should not be sixth along.

**The spoken route costs nothing.** `PanelPhrases` enumerates `PanelTab` values and the registered
roots, so "routing", "plan", "progress" and "course" work without touching the router grammar.
Checked: `Named` requires the phrase to be the word alone or an opener plus the word, so
"plot a course to Shinrarta" does not collide with the Course root.

**One test will need its width changed.** `TheTabStripFitsAnyWidthTests.AWideSurfaceShowsNoSteppers`
asserts no steppers at 1400px with six tabs, and a seventh may cross that. The fix is the test's
width, not the strip — steppers already exist for the narrow case.

### 2 — Progress: the route being flown, all of it

Reads the same `NavRouteReader.Current` that `RouteCallout` reads. No new source, no service, no
network, nothing added to the tick loop.

**This is the item that justifies the tab**, and it is also the cheapest. Every hop, its class
through `StarClasses`, the hazard and scoopable flags, distance remaining, jumps left, and where
the Commander is in it.

The arithmetic belongs in Core rather than in the page — `RouteProgress.For(route, currentSystem)`
returning the index, the jumps remaining and the distance left, with `OffRoute` when the Commander
is somewhere the route does not mention. It is arithmetic over records, so it is testable without a
window, which is the same argument `PanelNavigator` and `ZoomLadder` already sit in Core under.

**Distance remaining is null rather than partial when any leg's length is unknown.** `RouteHop.DistanceTo`
already returns null for a missing coordinate for a stated reason; a total that quietly omits a leg
reads as a shorter trip rather than as an incomplete answer.

**The unknown star class is drawn as itself.** `IsScoopable` returns null for a class d47 does not
recognise, and the panel must render that as *I don't know* rather than as *no* — a Commander told
a star is unscoopable routes around a star that would have refuelled them, which is the harm
`NavRoute.cs` already records.

Redraw needs a `TickRouting` on the same terms as `TickLoadout`: `NavRoute` is a record whose
reader returns the same instance until the file moves, so a reference comparison is exact and free,
and the current system is one string comparison beside it.

### 3 — Plan: three planners, one form each

**One page of three cards** — jump route, Road to Riches, trade run — each with its own form and
Plot button. Not a drill and not a nested strip: `PanelTabs.axaml` records that two stacked strips
is the thing the whole tab design avoids. A finished plot opens as a drill *level* (Plan → Sol →
Colonia) so the breadcrumb carries it and Back returns to the forms.

Gated on `Knowledge.GalaxySearch`, the flag `RouteCapability.Ready` already reads. Off draws the
sentence the tool already speaks plus a route to the settings row — a capability that is off, not
an error.

The journal fills jump range, origin and cargo capacity, as it does for the tools. **`capital` is
typed every time and never written to `data/`.** A panel makes "remember this" the obvious
convenience and it is the wrong one: the existing rule is that what a Commander is worth is nobody's
business but theirs, and a form field is exactly where a rule like that is lost by accident.

**Plots are jobs, and the panel needs what the voice never did.** A 90-second budget means a
pending state and a cancel, which no page currently has a pattern for. Trade is the exception and
is local arithmetic — seconds, not a job. If that turns into new machinery rather than a spinner,
build the simplest thing that works and say so; do not redesign the panel for it.

### 4 — One last plan, shared by the voice and the panel

The real design risk, and the item most worth getting right.

`RouteCapability` computes a plan, describes it, and keeps nothing. If the panel plots its own,
then "plot me a route to Colonia" and the Routing tab hold two different routes — which is the
`row-and-speaking-path-disagree` failure arriving somewhere new.

- A store — last jump route, last riches loop, last trade plan, each with what was asked and when.
  Written by the capability *and* by the panel, read by both. `data/route-plans.json`,
  hand-editable, the same discipline as `markets.json` and `ship-cores.json`.
- **The panel's Plot button goes through the capability rather than round it** to `IRouteService`.
  `CapabilityRegistry.InvokeAsync(tool, arguments, ct)` is the entry point and is reachable from
  the App; the model-free keyword router already invokes tools without a model, so a second caller
  of one path is the established shape, and two paths to one answer is not.
- **A panel-initiated plot does not enter the transcript.** A panel action is not a conversation
  turn. The store is what keeps the two paths in agreement, not a shared scrollback.
- Falls out for free: a voice plot populates the tab the moment it finishes, and *show me that
  route again* costs no network at all.

### 5 — Course: the clipboard, and the drive that says whether it worked

`NavigationCapability`'s two tools as controls: copy a system name, and optionally drive the galaxy
map.

Same order the docs give, because the order **is** the feature: clipboard first and always, then
the best-effort map drive, then the after-the-fact check against the route file that says whether
it took.

**Every system name drawn anywhere in the tab is a copy target** — a waypoint in Progress, a stop
in a trade plan. That join is what makes three pages worth more than three pages.

### 6 — Strip the stack trace, and make the gate able to catch it

**This item was built, measured and rolled back with the rest.** What it found is worth keeping,
because it makes the item a known quantity rather than an estimate:

`docs/capabilities/routes.md` carries a pasted `DocumentationGateTests` failure inside the
`plot_trade_route` fence — two lines, arrived with `1a03bfc`, shipped in Phase 36 and read by
nobody since. The gate does not see it because it asserts the page *contains* the schema, and it
does, with a stack trace underneath.

The fix is to require a fenced block that **is** the schema. Measured:

- **All 44 capability pages pass the tightened gate unchanged.** The worry that other pages
  legitimately mix prose into a schema fence turned out to be unfounded, so this costs no
  rewriting.
- **The fault was reintroduced once and the new assertion caught it**, naming the page and the
  tool — which the old assertion never would have. Per the standing rule, that is the only thing
  that proves a tightening is real.

It is independent of everything else here and could land on its own at any time.

---

## Not in scope, each for a stated reason

- **No new tools.** 39,918 of 40,000 is spent; this phase spends nothing more.
- **No VR**, beyond shaping `EnableRouting` so the headset could later be given Progress alone.
- **The galaxy plotter stays unwired.** `api/generic/route` wants the drive's physics and belongs
  behind the module specification table, as `routes.md` records.
- **Market saturation stays unmodelled.** The panel repeats the plan's own caveat rather than
  drawing a number nobody measured.
- **No mining routes.** `api/mining/route` is a 404; the ring index is `find_body` on the galaxy
  search and stays there.

## Order of work

1. **Item 6** — independent, two lines, and the gate tightening wants doing while the fault is
   still in hand to reintroduce.
2. **Item 1** — the tab, and the strip-width test, before anything depends on either.
3. **Item 2** — Progress. The most value for the least code: no network, no forms, no store.
4. **Item 4** — the shared store, before the panel can plot anything of its own.
5. **Item 3** — Plan.
6. **Item 5** — Course, and the copy-anywhere join that needs 2 and 3 to exist first.

A completed phase is a minor release, so whenever this is finished it lands as a `minor` through
`tools/release.ps1` — v0.40.0 from where the repository stood on 2026-08-20.
