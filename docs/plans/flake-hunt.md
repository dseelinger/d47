# Hunting the headless-session flake

**A prompt for a fresh session, written 2026-08-21.** Paste the section marked *The brief* into a
new Claude Code session in this repository. Everything above it is context for a human deciding
whether to run it; everything below it is the evidence the session should not have to rediscover.

This is written down because the fault has now been re-diagnosed from scratch four times, and each
time the reasoning was reconstructed rather than read. The standing rule in `CLAUDE.md` applies
throughout: **a lead is not a diagnosis** — reproduce before fixing, and afterwards reintroduce the
fault and watch the new test fail.

---

## The brief

> There is a long-standing intermittent test failure in this repository's `D47.App.Tests` suite. It
> has been carried by ten different unrelated tests over five months, it has never been diagnosed,
> and on 2026-08-21 it cost four CI/release workflow runs to publish one version. It is the single
> most expensive thing about shipping this project.
>
> **Your job is to diagnose it, not to fix it.** A fix without a reproduction is what the last four
> attempts produced, and none of them held. Produce a diagnosis backed by an observation, and only
> then a fix with a test that fails when the fault is reintroduced.
>
> Read, in this order:
>
> 1. `bugs.md`, the section *"a headless-session cleanup failure that ten different tests have now
>    carried"* — every occurrence, in order, with what each one ruled in or out.
> 2. This file below the brief, which is the same evidence assembled rather than narrated.
> 3. `tests/D47.App.Tests/HeadlessApp.cs`, which is the whole of the headless test-application
>    setup.
>
> **Start by reproducing it locally.** The fifth occurrence proves it is not a property of the CI
> runner: it has happened in a local `dotnet test -c Release`. Loop the App suite in Release until
> it fires. Then instrument, under a debugger or with the thread ids captured at throw time.
>
> **The one instrumentation that has been asked for and never written** is in the third occurrence's
> entry: capture the managed thread id owning `Dispatcher.UIThread` and the thread id the session is
> dispatching on, both at the moment of the throw, and print them with the test name. Everything
> else is inference.
>
> Take as much thinking as this needs. It has defeated four quick looks.

---

## Every occurrence

| # | When | Test | Symptom |
|---|---|---|---|
| 1 | 0.38.0 release | `AuditionDoesNotCommitTests.PlayingASecondVoiceCancelsTheFirst` | 5 s timeout awaiting a cancellation |
| 2 | 0.38.0 release | `RowWidthTests.TheWholeChoiceLabelIsOnTheTooltipWhenTheBoxClipsIt` | cleanup `InvalidOperationException` |
| 3 | v0.39.0 release | `PickerShowsEverythingTests.EveryChoiceIsListedAndTheIdIsNotInTheBox` | cleanup, inside `EnsureIsolatedApplication` |
| 4 | v0.41.1, runner | `AuditionDoesNotCommitTests.PlayingASecondVoiceCancelsTheFirst` | 5 s timeout |
| 5 | v0.41.1, **local Release** | `LoadoutTabTests.KeepingWhatIsFittedListsThatModulesRolls` | cleanup, same stack |
| 6 | — | (the pair above counted separately in `bugs.md`) | |
| 7 | v0.44.1 release | `TheReworkedChromeRendersToACaptureTests.APlanThatWasMade` | cleanup, same stack |
| 8 | v0.46.0 `ci` | `EngineersTabTests.PromotingOffersTheChain` | cleanup, same stack |
| 9 | v0.46.0 `ci` re-run | `AuditionDoesNotCommitTests` — **two** tests | both 5 s timeouts |
| 10 | v0.46.0 release | `SearchTheTabTests.AFilterOpensTheCardItMatchedInAndClosesItAgainAfter` | cleanup, same stack |

**No carrier test is related to any other**, and none is related to the change being released. The
audition pair is the only repeat, and it has now appeared three times.

## Two symptoms, and the question of whether they are one fault

**Symptom A — the cleanup throw.** `System.InvalidOperationException: The calling thread cannot
access this object because a different thread owns it`, raised by xUnit as a *Test Case Cleanup
Failure* rather than from inside the test body, with this stack every time:

```
Avalonia.Threading.Dispatcher.<VerifyAccess>g__ThrowVerifyAccess|17_0()
Avalonia.Rendering.DefaultRenderLoop.Add(IRenderLoopTask)
Avalonia.Rendering.Composition.Server.ServerCompositor..ctor(...)
Avalonia.Rendering.Composition.Compositor..ctor(...)
Avalonia.Headless.AvaloniaHeadlessPlatform.Initialize(AvaloniaHeadlessPlatformOptions)
Avalonia.Headless.AvaloniaHeadlessPlatformExtensions.<>c__DisplayClass0_0.<UseHeadless>b__0()
Avalonia.AppBuilder.SetupUnsafe()
Avalonia.Headless.HeadlessUnitTestSession.EnsureIsolatedApplication()
Avalonia.Headless.XUnit.AvaloniaTestRunner.Run(...)
```

**Read that stack carefully, because it is the strongest single clue and it has been under-used.**
The throw is not in the named test. It is the headless platform being **stood up** —
`EnsureIsolatedApplication` → `Initialize` → a new `Compositor` → `DefaultRenderLoop.Add` — on a
thread that does not own the dispatcher. So the *named* test is merely the first one to ask for an
application after something went wrong; it is a victim, not a cause. That is why ten unrelated
tests can carry it and why chasing any of them individually has failed.

The implication worth testing: the session's application is being torn down or re-created when it
should not be, or the session thread has changed identity. Whatever leaves the session needing a
*new* isolated application mid-run is the actual fault.

**Symptom B — the audition timeout.** `PlayingASecondVoiceCancelsTheFirst` waits five seconds on
`cancelled.Task.WaitAsync` and the second press does not cancel the first. `bugs.md` records the
untested hypothesis — a stale button, detached by a rebuild, raising `Click` into nothing — and
notes that what this test leaves behind when it fails is **an infinite delay awaiting a token
nobody cancelled**, which is a strong candidate for the thing that then wedges the session.

**The open question is whether B causes A.** They have appeared in the same runs. If the audition
test leaks a pending task or a live dispatcher timer, that would be the state the next
`EnsureIsolatedApplication` trips over. Occurrence 9 — where the audition pair failed *without* any
accompanying cleanup failure — is evidence either way and should be examined first.

## What has already been ruled out

- **Not a property of the CI runner.** Occurrence 5 is a local `dotnet test -c Release`. Every
  argument of the form "it passes locally, so it is the runner" has been made four times and has
  been wrong four times. Do not make it again.
- **Not the two dispatcher races already fixed.** Commits `670997f` and `5066025` closed genuine
  races around the *first* audition press being underway before the second arrives. Both are still
  in place and still correct. This is later in the sequence.
- **Not the test under the name.** See the stack reading above.
- **Not caused by any particular release's changes.** Ten carriers across five months and unrelated
  feature work.

## The setup, in full

- `tests/D47.App.Tests/HeadlessApp.cs` declares `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`.
- The app builder uses **real Skia rendering**, not the null drawing backend:
  `UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })`. This is
  deliberate — capture tests render real frames — and it is also what puts a real `Compositor` and
  render loop in the picture, which is exactly where the throw is. **Worth an experiment:** does the
  fault reproduce with `UseHeadlessDrawing = true`? If not, that narrows it enormously, and the
  capture tests could plausibly be segregated.
- Packages: `Avalonia.Headless.XUnit` 12.1.1, `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5,
  `Microsoft.NET.Test.Sdk` 18.8.1, on `net10.0-windows10.0.26100.0`.
- There is **no `xunit.runner.json`** and no `CollectionBehavior` attribute in the assembly, so the
  suite runs on xUnit v3's defaults. **Check what those defaults are for parallelism in v3** — they
  changed from v2, and `HeadlessUnitTestSession` is a per-assembly shared resource. A run where two
  collections dispatch onto one session concurrently is precisely the shape of the throw.
- The suite is ~660 tests and takes 40-50 s locally.

## Things worth trying, roughly in order of cheapness

1. **Reproduce locally.** Loop `dotnet test tests/D47.App.Tests -c Release` until it fires; record
   how many runs it took, because that number is the measure everything else is judged against.
2. **Answer the parallelism question** above from the xUnit v3 defaults, and try
   `DisableTestParallelization` to see whether the rate changes. This is a diagnostic, not a fix —
   serialising the suite would be paying for the answer forever.
3. **Instrument the throw** with the two thread ids, as the third occurrence asked.
4. **Try `UseHeadlessDrawing = true`** and re-run the loop, to find out whether the real compositor
   is required for the fault.
5. **Examine the audition test's failure state.** If it leaves an uncancelled token and a pending
   continuation, find out whether that survives into the next test's session.

## What a finished job looks like

- A reproduction that can be run on demand, with its rate stated.
- A one-paragraph diagnosis naming the mechanism, not a suspect.
- A fix, and a test that **fails when the fault is reintroduced** — watched failing, not assumed to.
- `bugs.md` updated: the section removed if it is truly closed, or amended with what was ruled out
  if it is not. The permanent record is the `CHANGELOG.md` line under the release that carries it.
- If the answer turns out to be an upstream Avalonia bug, an issue link and a pinned version or
  workaround, and the section stays open with that recorded.
