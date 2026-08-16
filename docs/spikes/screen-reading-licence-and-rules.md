# Reading the screen: the licence graph, and Frontier's rules

**Checked 2026-08-16.** Desk research — no code was written and no hardware was involved.

This backs `list.md` Phase 22, *Spike: is there anything in the mirror to read*, which deliberately
scopes two desk questions **into** the spike rather than after it:

- **A.** What are the actual licences of the computer-vision candidates, and of everything they drag
  in? The item records Emgu.CV as *"understood to be GPL with a paid commercial option"* and OpenCV,
  OpenCvSharp and ONNX Runtime as *"understood to be permissive"* — and says outright that
  understood is not verified, and that the invariant is to **verify the transitive graph, not the
  direct reference**.
- **B.** Does reading the screen sit inside Frontier's published rules for third-party applications,
  checked rather than assumed?

**This page cannot answer the spike's primary question.** Whether a panel can be located at all in
the desktop mirror needs three screenshots at three head angles in a headset. That measurement is
untaken, and nothing below substitutes for it.

---

## The verdict in one paragraph

**Phase 22 survives both desk questions, and both narrow it.** Question A ends in a licence-clean
path that is *one specific package*, not a family: the obvious Windows package fails the invariant,
and the package beside it passes while happening to contain exactly the modules the spike needs.
Question B ends in a genuine unknown that no amount of further reading will close, because **no
published Frontier rule addresses reading the screen either way** — but it also ends with a named
route to ask, and with the observation that the risk is **not uniform across Phase 22's two use
cases**. The contacts panel, which is the case that justifies the whole phase, is the one the EULA's
text most directly names. The Galaxy Map is not. That reordering is the most actionable thing on
this page.

---

## 1. Question A, headline: a package declaring `Apache-2.0` ships an LGPL binary

This is the finding the invariant exists to catch, and it is not subtle. `OpenCvSharp4.runtime.win`
— the Windows native package every OpenCvSharp tutorial tells you to reference — declares a
permissive licence expression and packs an LGPL binary, in the same twelve lines of the same file:

```xml
<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
...
<None Include="../src/build/OpenCvSharpExtern/Release/OpenCvSharpExtern.dll"
      Pack="true" PackagePath="runtimes/win-x64/native" />
<None Include="../opencv_artifacts/x64/vc18/bin/opencv_videoio_ffmpeg4130_64.dll"
      Pack="true" PackagePath="runtimes/win-x64/native" />
```

— [`nuget/OpenCvSharp4.runtime.win.csproj`, branch `4.x`](https://github.com/shimat/opencvsharp/blob/4.x/nuget/OpenCvSharp4.runtime.win.csproj)

What that second DLL is, in OpenCV's own words:

> "The pre-built opencv_videoio_ffmpeg*.dll is: * LGPL library, not BSD libraries."
>
> — [opencv/3rdparty/ffmpeg/readme.txt](https://github.com/opencv/opencv/blob/4.x/3rdparty/ffmpeg/readme.txt)

The same file states the remedy, and states it as routine rather than as a workaround:

> "If LGPL/GPL software can not be supplied with your OpenCV-based product, simply exclude
> opencv_videoio_ffmpeg*.dll from your distribution"

OpenCvSharp's maintainers are not hiding this — their own package README says the full Linux
package **"includes FFmpeg (LGPL v2.1)"** in as many words. The problem is only that the *NuGet
licence expression*, which is the field a dependency-scanning tool reads and the field a developer
glances at, says `Apache-2.0` and stops there.

**A licence expression describes the package author's own code. It does not describe the binaries
the package carries.** That is the whole of the lesson, and it would have been missed by any check
that read the four candidates' licences and called the job done.

## 2. The candidates, verified

| Candidate | Declared | Verified at source | Verdict |
|---|---|---|---|
| **Emgu.CV** `4.13.0.5924` | `LICENSE.txt` (file) | **GPL-3.0**, and a separate paid commercial licence | **Out** — see §3 |
| **OpenCV** (upstream) | — | **Apache-2.0** ([LICENSE](https://github.com/opencv/opencv/blob/4.x/LICENSE)) | Clean |
| **OpenCvSharp4** `4.13.0.20260627` (managed) | `Apache-2.0` | `Apache-2.0`; deps `System.Memory`, `System.Runtime.CompilerServices.Unsafe` — both MIT | Clean |
| **OpenCvSharp4.runtime.win** | `Apache-2.0` | Apache-2.0 **+ an LGPL-2.1 FFmpeg DLL**, and a nonfree-enabled build | **Fails the invariant as shipped** |
| **OpenCvSharp4.runtime.win.slim** | `Apache-2.0` | Apache-2.0, **one DLL, no FFmpeg, no contrib** | **Clean — this is the one** |
| **ONNX Runtime** `1.29.0` | MIT | MIT; graph contains **Eigen under MPL-2.0** | Clean-ish — see §5 |

Managed dependency graphs, read from the nuspecs on `api.nuget.org` rather than recalled:

```
OpenCvSharp4 4.13.0.20260627
  └── System.Memory 4.6.3                          (MIT)
  └── System.Runtime.CompilerServices.Unsafe 6.1.2 (MIT, netstandard2.1 only)

Microsoft.ML.OnnxRuntime 1.29.0
  └── Microsoft.ML.OnnxRuntime.Managed 1.29.0      (MIT)
        └── System.Memory 4.5.5                    (MIT)
        └── System.Numerics.Tensors 9.0.0          (MIT)
```

Both graphs terminate in Microsoft/.NET Foundation MIT packages. **The managed side of this question
was never the risk.** The risk was entirely in the native payloads, which do not appear in a
dependency graph at all.

## 3. Emgu.CV is out, and now for two reasons rather than one

The `list.md` wording — GPL with a paid commercial option — is **correct on both halves**, verified:

- The repository's `LICENSE` is the **GNU General Public License, Version 3**
  ([emgucv/emgucv](https://github.com/emgucv/emgucv/blob/master/LICENSE)).
- Emgu's own platform table lists, for each platform, an open-source column licensed **"GPL"** beside
  a **"Commercial License"** column ([emgu.com wiki, Main Page](https://www.emgu.com/wiki/index.php/Main_Page)).

GPL-3.0 fails the no-copyleft invariant outright, so the paid option was the only thing keeping the
row alive. **The maintainer ruled out paid licences on 2026-08-16 — no purchase at any price.** So
the commercial escape hatch is closed by project decision as well as the free one being closed by
the invariant, and Emgu.CV needs no further consideration in this or any later phase.

## 4. The native OpenCV build carries two problems; one package drops both

Beyond FFmpeg, the second problem is a single line in OpenCvSharp's build configuration, present on
**both** the `4.x` and `main` branches:

```cmake
set(OPENCV_ENABLE_NONFREE  ON      CACHE BOOL   "" FORCE)
```

— [`cmake/opencv_build_options.cmake`](https://github.com/shimat/opencvsharp/blob/4.x/cmake/opencv_build_options.cmake)

"Nonfree" in OpenCV means the patent-encumbered algorithms in `opencv_contrib`'s `xfeatures2d`,
of which **SURF** is the one that matters here — it is declared in
[`xfeatures2d/nonfree.hpp`](https://github.com/opencv/opencv_contrib/blob/4.x/modules/xfeatures2d/include/opencv2/xfeatures2d/nonfree.hpp)
and nowhere else. SURF is also the algorithm a developer reaches for first when the task is written
down as *feature matching and a homography*, which is exactly how Phase 22 words it. So this is not
a theoretical exposure; it is the default path.

**The slim package drops both problems at once**, and this is where the answer turns out well. Per
OpenCvSharp's own [runtime package README](https://github.com/shimat/opencvsharp/blob/4.x/nuget/README.runtime.md):

| | Modules |
|---|---|
| **Enabled in slim** | `core`, `imgproc`, `imgcodecs`, `calib3d`, `features2d`, `flann`, `objdetect`, `photo` |
| **Disabled in slim** | `contrib`, `dnn`, `ml`, `video`, `videoio`, `highgui`, `stitching`, `barcode` |

and its packaging file packs exactly one binary — `OpenCvSharpExtern.dll`, with no FFmpeg line
([`OpenCvSharp4.runtime.win.slim.csproj`](https://github.com/shimat/opencvsharp/blob/4.x/nuget/OpenCvSharp4.runtime.win.slim.csproj)).

**`contrib` disabled means `xfeatures2d` is not built, so `OPENCV_ENABLE_NONFREE` has nothing to
enable. `videoio` disabled is why there is no FFmpeg DLL to ship.** The two licence problems are
consequences of two modules, and slim omits both modules.

### The part that makes this a clean answer rather than a compromise

The slim module set is not a reduced version of what Phase 22 needs. It is a superset of it:

| What the spike needs | Module | In slim? |
|---|---|---|
| Feature detection and description | `features2d` | **yes** |
| Descriptor matching | `flann` | **yes** |
| `findHomography` | `calib3d` | **yes** |
| Loading a captured frame | `imgcodecs` | **yes** |
| Greyscale, resize, threshold | `imgproc` | **yes** |

And the detectors survive the cut. `SIFT`, `BRISK`, `ORB` and `AKAZE` are all declared in **main**
`features2d.hpp` — verified by reading the header, at lines 266, 353, 423 and 866 respectively
([features2d.hpp](https://github.com/opencv/opencv/blob/4.x/modules/features2d/include/opencv2/features2d.hpp)).
Only SURF lives in nonfree contrib. So dropping contrib costs the one patent-encumbered detector and
keeps four unencumbered ones, including SIFT, whose patent expired in March 2020 and which moved
into the main repository as a result.

Losing `videoio` costs nothing either, because **d47 would not have captured frames through OpenCV
anyway** — capturing a specific window on Windows 11 is Windows Graphics Capture or DXGI, not
`cv::VideoCapture`. The module being dropped is one the design had no use for.

**Recommendation: `OpenCvSharp4.Windows.Slim` (verified present on NuGet, `4.13.0.20260627`), never
`OpenCvSharp4.Windows`.** The difference between those two package names is the difference between
passing and failing the invariant, and nothing in either name says so.

## 5. ONNX Runtime is MIT; its graph is not quite

`Microsoft.ML.OnnxRuntime` is **MIT** and its managed dependencies are MIT. But its own
[`ThirdPartyNotices.txt`](https://github.com/microsoft/onnxruntime/blob/main/ThirdPartyNotices.txt)
— 6,343 lines — carries two entries worth naming:

- **Eigen, under MPL-2.0.** Eigen is header-only and compiled into the CPU kernels, so it is in the
  shipped `onnxruntime.dll`. MPL-2.0 is **file-level (weak) copyleft**: its obligations attach to the
  MPL-licensed files and their source, and do **not** reach d47's own code the way GPL would. Whether
  that clears a bar written as "no copyleft" is a judgement for the maintainer rather than a fact this
  page can settle — so it is recorded plainly rather than waved through. It is flagged here precisely
  because "ONNX Runtime is MIT" is true, is what the direct reference says, and is not the whole
  answer.
- **Mbed TLS, dual-licensed Apache-2.0 **or** GPLv2+.** Not a problem, and it is worth saying why:
  the notices file resolves the choice itself, stating that the distribution uses Mbed TLS under the
  Apache licence. A dual licence with the permissive arm already elected is clean.

### The question this raises about ONNX Runtime being in Phase 22 at all

Phase 22's stated technique is feature matching and a homography — **that is OpenCV, and it needs no
neural network.** ONNX Runtime only enters if the design later wants a learned detector or a learned
OCR model. Two consequences:

- **The MPL-2.0 question does not need answering yet.** It is not on the spike's critical path, and
  deciding it now would be deciding it without a use case.
- **If the need turns out to be OCR specifically**, Windows 11 ships an OCR engine in the OS
  (`Windows.Media.Ocr`), which carries no third-party licence at all and would sidestep this section
  entirely. It is not free of cost: d47's shared TFM is `net10.0-windows` (`Directory.Build.props:7`),
  and reaching WinRT projections needs a platform-versioned TFM such as
  `net10.0-windows10.0.19041.0` — a repo-wide change, not a package reference. Recorded as a lead,
  not a recommendation; nothing here has measured whether it reads Elite's font.

## 6. Question B — what Frontier actually publishes

Four documents were read, three of them behind a 403. **None of them is a third-party application
policy, because Frontier has not published one.** What they do say, in descending order of relevance:

### 6.1 The EULA is the only binding document, and its text is against this

The [Elite Dangerous EULA](https://www.frontier.co.uk/legal/elite-dangerous/eula) — read in a
browser, 403 to an automated fetch — carries four restrictions that bear on reading the screen:

> **3(c)** "use cheats, automation software, hacks, mods, or any other unauthorized software designed
> to modify or defeat the purpose or experience of the Game"
>
> **3(d)** "use any unauthorized software that harvests or otherwise collections information about
> others or the Game, including about a character or the game environment"
>
> **3(e)** "use any robot, spider, scraper, or other automated or manual means to access the Game or
> any Online Features or copy any content or information from the Game or any Online Features"
>
> **4.5** "You may not collect or harvest any information or data from the Game, the Online Features
> or our systems"

(The `collections` in 3(d) is Frontier's typo, reproduced as written.)

**A literal reading of 4.5 and 3(e) prohibits every journal reader ever written** — EDMC,
EDDiscovery, EDDI, EDEngineer, and d47 as it exists today at Phase 21. All of them collect
information from the Game by automated means. Since Frontier **built the journal for those tools and
hosts their release threads on its own forum**, the literal reading cannot be the operative one; it
proves far too much.

What carries the real weight is the word **"unauthorized"** in 3(c) and 3(d). And that is precisely
where the journal and the screen part company.

### 6.2 The journal is authorized, in Frontier's own words — and *why* it exists is the point

From Frontier's own [Player Journal manual](https://hosting.zaonce.net/community/journal/v32/Journal_Manual-v32.pdf),
§1 Introduction (read by extracting the PDF text):

> "Third-party tools developers have been reading some of the entries in the network log file, mainly
> in order to track the player's location."
>
> "There is a clear demand from players for third-party tools, and from tools developers for more
> information from the game and/or server api."
>
> "The new Player Journal provides a stream of information about gameplay events which can be used by
> tools developers to provide richer, more detailed tools to enhance the player experience."

**Read the first sentence again.** Tools were reading an undocumented channel that Frontier had not
intended for the purpose — the network log. Frontier's response was not enforcement. It was to
**publish a sanctioned channel and document it**, and the journal is that channel.

That is the single most relevant fact on this page, and it cuts both ways. It establishes that
Frontier's disposition toward tools reading the client is accommodating rather than hostile. It also
establishes that **the journal exists because reading an unpublished channel was the unsatisfactory
state of affairs** — and reading rendered pixels is squarely an unpublished channel. Phase 22 is
proposing to do the 2016 thing again, for data the 2016 fix does not cover.

### 6.3 The one place Frontier set out ground rules for tools is about servers, not clients

The [locked sticky at the head of the Player Tools & API forum](https://forums.frontier.co.uk/threads/regarding-this-forum-a-quote-from-zac.225062/)
(Frontier staff, January 2016) is the closest thing to a published policy. Quoting Zac Antonaci:

> "We are passionate about the development of community created content and very much see the value
> in the tools that are being created"
>
> "We will bring in guidelines and an approval process over time too, but for now please be aware
> that we will stop apps that are not well behaved."

and the forum's own ground rules, which name what "not well behaved" means:

> "Using the current API in a malicious way or to cause a severe degradation of services"
>
> "Hammering the current API server with requests will result with us severely limiting the service."
>
> "All tools are considered third party."

**Every concern named is server-side** — polling, load, degradation of service. Nothing addresses how
a tool reads the local client. And the promised guidelines and approval process, announced in 2016,
appear never to have arrived: ten years of the forum's stickies contain no such document, which is
itself the finding rather than a gap in the search.

### 6.4 Two documents that turned out not to govern

- **[Elite Dangerous Media Usage Rules](https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/)**
  — the thread this repo already stands on for game data. Read in full in a browser. It governs
  **assets and content**: fan art, fan videos, community sites, news pieces. It requires
  non-commercial use and attribution, both of which d47 already satisfies via `NOTICE`. It would bite
  if d47 *published* captured imagery; Phase 22's design has nothing leave the machine, so it does
  not. It is not an application policy and does not pretend to be one.
- **[Mod Policy](https://www.frontier.co.uk/legal/mod-policy)** — incorporated into the EULA by
  reference, and it does *not* govern here. Its definition of a Mod is "modifications, customisations
  and/or upgrades to levels, characters, creatures, vehicles, buildings, rides, parks, blueprints and
  maps, saves, audio files and textures" — things that alter the game. d47 alters nothing. Worth
  having checked, because it contains a clause that would have been alarming if it *did* apply:
  where Frontier provides no modding tools for a game, creating Mods for it is not permitted.

### 6.5 What the community has actually done, unchallenged

Not a rule, and recorded as what it is — precedent rather than permission:

- **EliteOCR** read commodity market data out of Elite screenshots, was announced on Frontier's own
  forum, is catalogued on EDCodex, and was never actioned. Its function was later superseded when
  Frontier published `market.json` beside the journal — **the network-log pattern, run a second
  time**: community reads pixels, Frontier eventually publishes the data properly.
- **EDHM**, a HUD *mod*, has sat on Frontier's Player Tools forum since 2020 across 2,000 replies and
  485,000 views. It modifies how the game renders. A passive reader that modifies nothing is a
  strictly smaller ask.

### 6.6 There is a route to ask, and it is cheap

Frontier maintains a [3rd Party Devs submission form](https://forums.frontier.co.uk/threads/3rd-party-devs-submission-form.349330/)
(locked sticky, Frontier staff) for tool developers to register, and the Media Usage Rules name
`community@frontier.co.uk` for exactly the case of *"a purpose that isn't listed above"* where a
creator "would like clarification on our position".

**So the honest state of Question B is not "unknown and unknowable".** It is: unaddressed by anything
published, with a named channel for getting it addressed, which nobody has yet used for this
question.

## 7. The risk is not uniform across Phase 22's two use cases

This is the finding that should change what gets built first, and it comes straight out of §6.1's
clause 3(d) — *"information about others … including about a character or the game environment"*.

| Phase 22 use case | What is read | Exposure under 3(d) |
|---|---|---|
| **Contacts panel** (the case that justifies the phase) | Other Commanders and NPCs — **other characters** | **Directly named.** This is the clause's central example, almost word for word. |
| **Galaxy Map** (*Read a system name*) | System names — public astrography, already in the journal for visited systems and in every third-party galaxy index | **Weak.** Not information about others; not a character. |

`list.md` orders the contacts panel first and calls it "the case that justifies it", on the entirely
sound reasoning that Phase 15's rival-Power warning is otherwise unreachable and nobody can read a
contacts list while under fire. **That reasoning is unaffected — the feature is still the most
valuable one here.** What has changed is that it is also the most exposed one, and the two facts were
not previously known together.

## 8. So does Phase 22 survive?

**It survives. It does not shrink to desktop-only, and it does not close** — but note carefully that
neither of those outcomes was ever in these two questions' gift. Desktop-only versus VR is decided by
whether a panel can be located in the mirror, which is the spike's primary question and remains
unmeasured. A licence answer and a rules answer cannot decide it.

What the two desk answers do decide:

- **Question A closes cleanly and in Phase 22's favour.** There is a licence-clean, no-copyleft,
  no-patent, no-payment path, and it is a single named package whose module set happens to be exactly
  the required one. The invariant did its job: the obvious package would have shipped an LGPL binary
  under an Apache-2.0 label, and no direct-reference check would have seen it.
- **Question B does not close, and cannot be closed by reading.** The answer is that Frontier has
  published nothing on point; that the EULA's literal text catches the journal readers too and so
  cannot be read literally; that the operative word is "unauthorized"; that the journal is authorized
  and the screen is not; and that Frontier's own history is to respond to this exact situation by
  publishing a channel rather than by enforcing.

## 9. What this changes

| Question the item asked | Answer | What ships |
|---|---|---|
| Is Emgu.CV GPL with a paid option? | **Yes, both halves verified** — GPL-3.0 in the repo, commercial column on Emgu's own platform table | Emgu.CV is out permanently. The paid option is also out by maintainer decision, 2026-08-16. |
| Are OpenCV / OpenCvSharp / ONNX Runtime permissive? | **Directly, yes.** Apache-2.0, Apache-2.0, MIT | Nothing — but see the next two rows, which is the point. |
| Does the transitive graph agree? | **No.** `OpenCvSharp4.runtime.win` declares Apache-2.0 and packs an LGPL-2.1 FFmpeg DLL; the build sets `OPENCV_ENABLE_NONFREE ON` | **Reference `OpenCvSharp4.Windows.Slim`, never `OpenCvSharp4.Windows`.** Record the reason beside the reference, because the package names do not carry it. |
| *(not asked)* Does slim cost the spike anything? | **No.** `features2d`, `flann`, `calib3d`, `imgproc`, `imgcodecs` all present; SIFT/ORB/BRISK/AKAZE all in main `features2d`; only SURF is lost | Nothing. Use ORB or SIFT. Never SURF — and it will not be available to reach for by accident. |
| *(not asked)* Is ONNX Runtime's graph clean? | MIT, but contains **Eigen under MPL-2.0** (weak, file-level copyleft) | A decision the maintainer owes — **but not yet.** Phase 22's stated technique needs no model at all. |
| Is screen reading inside Frontier's published rules? | **There are no published rules on it.** The EULA's text is against it, but equally against every journal reader Frontier built the journal for | No code decision. **Ask** — the 3rd Party Devs form and `community@frontier.co.uk` both exist. |
| *(not asked)* Is the risk the same across the phase? | **No.** EULA 3(d) names "information about others … about a character" — the contacts panel almost exactly; the Galaxy Map barely at all | **Build the Galaxy Map case first.** Gate the contacts panel on having asked. This reverses `list.md`'s ordering, and the reason is written above. |

## 10. Where these answers came from

The recorded trap held on both questions, and would have produced a wrong finding on each.

**`forums.frontier.co.uk` answered 403** to every automated fetch and rendered perfectly in the
browser — behind it sat the Media Usage Rules, the tools-forum ground rules and the 3rd Party Devs
form, which are three of the four documents Question B turns on.

**`www.frontier.co.uk/legal/…` answered 403 too**, which was not previously recorded. That is where
the EULA and the Mod Policy live — the only *binding* documents in this investigation. A fetch-only
pass would have concluded that Frontier publishes nothing at all, which is the exact opposite of the
truth.

**`www.emgu.com` answered 403** to a fetch and renders fine in a browser. Emgu is the one candidate
`list.md` had already flagged, and the fetcher would have left it unverified.

A fourth costume, and a new one: **the answer was in a file that needed decoding rather than
fetching.** Frontier's Player Journal manual is a PDF, and §6.2 — the most load-bearing quotation on
this page — was unreadable until its text was extracted. Not a 403; the fetch succeeded and returned
1.5 MB of compressed streams. **A successful fetch that returns unreadable bytes is the same mistake
wearing better clothes**, and the honest sentence is still *where the looking stopped*.

Two claims here are deliberately **not** upgraded beyond their evidence: that Frontier never shipped
the 2016 promise of guidelines is an absence observed across the forum's stickies rather than a
statement Frontier has made, and the EliteOCR precedent is *unchallenged use*, which is not
permission and is not recorded as permission.
