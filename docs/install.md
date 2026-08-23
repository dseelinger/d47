---
title: Installing
group: General help
nav_order: 1
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published rather than locally.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Download it, check it, run it. No administrator. Nothing to install first.</p>
<section>
<h2><span class="num">1</span> Two ways to get it. Same result.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="The installer and the portable zip both produce one folder">
 <rect x="20" y="26" width="255" height="84" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="147" y="62" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">d47-setup.exe</text>
 <text x="147" y="86" text-anchor="middle" font-size="14" fill="var(--text-muted)">it puts itself away</text>
 <rect x="20" y="130" width="255" height="84" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="147" y="166" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">d47.zip</text>
 <text x="147" y="190" text-anchor="middle" font-size="14" fill="var(--text-muted)">you unpack it yourself</text>
 <path d="M288 68 L404 102" fill="none" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="418,106 400,96 396,110" fill="var(--accent-muted)"/>
 <path d="M288 172 L404 138" fill="none" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="418,134 396,130 400,144" fill="var(--accent-muted)"/>
 <rect x="440" y="52" width="300" height="136" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="590" y="104" text-anchor="middle" font-size="20" font-weight="700" fill="var(--text)">ONE FOLDER</text>
 <text x="590" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">that you can pick up and move</text>
 <text x="590" y="160" text-anchor="middle" font-size="14" fill="var(--text-muted)">wherever you like</text>
 <text x="440" y="230" text-anchor="middle" font-size="14" fill="var(--text-muted)">Not into Program Files — somewhere you own, so it never needs to ask for permission.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Check it is really ours.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="Compare the downloaded file's fingerprint against the published one">
 <rect x="20" y="34" width="230" height="106" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="112" y="52" width="46" height="54" rx="5" fill="var(--surface-alt)" stroke="var(--accent)" stroke-width="2"/>
 <text x="135" y="128" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">WHAT YOU GOT</text>
 <text x="300" y="76" text-anchor="middle" font-size="14" fill="var(--text-muted)">Get-FileHash</text>
 <line x1="266" y1="92" x2="320" y2="92" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="334,92 318,84 318,100" fill="var(--accent-muted)"/>
 <rect x="348" y="34" width="200" height="106" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <line x1="378" y1="66" x2="518" y2="66" stroke="var(--accent)" stroke-width="4" stroke-linecap="round"/>
 <line x1="378" y1="82" x2="486" y2="82" stroke="var(--accent)" stroke-width="4" stroke-linecap="round"/>
 <text x="448" y="128" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">A FINGERPRINT</text>
 <text x="598" y="76" text-anchor="middle" font-size="14" fill="var(--text-muted)">compare</text>
 <line x1="564" y1="92" x2="618" y2="92" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="632,92 616,84 616,100" fill="var(--accent-muted)"/>
 <rect x="646" y="34" width="214" height="106" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <line x1="678" y1="66" x2="828" y2="66" stroke="var(--accent-muted)" stroke-width="4" stroke-linecap="round"/>
 <line x1="678" y1="82" x2="796" y2="82" stroke="var(--accent-muted)" stroke-width="4" stroke-linecap="round"/>
 <text x="753" y="128" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">THE PUBLISHED ONE</text>
 <rect x="150" y="172" width="260" height="52" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="280" y="204" text-anchor="middle" font-size="16" font-weight="700" fill="var(--accent)">SAME — run it</text>
 <rect x="470" y="172" width="260" height="52" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2"/>
 <text x="600" y="204" text-anchor="middle" font-size="16" font-weight="700" fill="var(--danger)">DIFFERENT — do not</text>
 <text x="440" y="252" text-anchor="middle" font-size="14" fill="var(--text-muted)">Builds are unsigned, so Windows will warn you. This is how you check instead of trusting.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> What ends up in the folder.</h2>
<svg viewBox="0 0 880 316" role="img" aria-label="The folder holds the executable, its runtimes folder, and a data folder">
 <rect x="40" y="16" width="800" height="268" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="96" y="66" font-size="19" font-weight="700" fill="var(--accent)">d47.exe</text>
 <text x="96" y="106" font-size="19" font-weight="700" fill="var(--accent)">runtimes\</text>
 <text x="96" y="154" font-size="19" font-weight="700" fill="var(--accent)">data\</text>
 <text x="130" y="188" font-size="15" fill="var(--text-muted)">settings.json</text>
 <text x="130" y="216" font-size="15" fill="var(--text-muted)">secrets.json</text>
 <text x="130" y="244" font-size="15" fill="var(--text-muted)">models\</text>
 <text x="130" y="270" font-size="15" fill="var(--text-muted)">logs\</text>
 <path d="M420 48 h16 v66 h-16" fill="none" stroke="var(--danger)" stroke-width="2.5" stroke-linejoin="round"/>
 <text x="452" y="76" font-size="16" font-weight="700" fill="var(--danger)">keep these two together</text>
 <text x="452" y="100" font-size="14" fill="var(--text-muted)">apart, it starts — but cannot hear you</text>
 <path d="M420 136 h16 v140 h-16" fill="none" stroke="var(--accent)" stroke-width="2.5" stroke-linejoin="round"/>
 <text x="452" y="198" font-size="16" font-weight="700" fill="var(--accent)">everything it writes</text>
 <text x="452" y="222" font-size="14" fill="var(--text-muted)">your settings, your keys, your logs</text>
 <text x="452" y="246" font-size="14" fill="var(--text-muted)">move the folder and all of it moves</text>
 <text x="440" y="308" text-anchor="middle" font-size="14" fill="var(--text-muted)">Never your Windows profile. Everything is here, beside the program.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Updating itself.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="D47 tells you, you press the button, it verifies and swaps">
 <rect x="20" y="34" width="195" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="117" y="82" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">IT TELLS YOU</text>
 <text x="117" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">a banner appears</text>
 <polygon points="226,80 240,90 226,100" fill="var(--accent-muted)"/>
 <rect x="252" y="34" width="195" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="349" y="82" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">YOU PRESS IT</text>
 <text x="349" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">nothing moves before this</text>
 <polygon points="458,80 472,90 458,100" fill="var(--accent-muted)"/>
 <rect x="484" y="34" width="175" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="571" y="82" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">IT CHECKS</text>
 <text x="571" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">the fingerprint again</text>
 <polygon points="670,80 684,90 670,100" fill="var(--accent-muted)"/>
 <rect x="696" y="34" width="164" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="778" y="82" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">IT SWAPS</text>
 <text x="778" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">and restarts</text>
 <text x="440" y="186" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">If any step fails, it puts everything back.</text>
 <text x="440" y="212" text-anchor="middle" font-size="14" fill="var(--text-muted)">You never end up with half an update.</text>
</svg>
</section>
<section>
<h2><span class="num">5</span> What leaves your PC.</h2>
<svg viewBox="0 0 880 316" role="img" aria-label="Only a version check and a one-off model download leave the machine">
 <rect x="30" y="40" width="510" height="212" rx="12" fill="none" stroke="var(--accent-muted)" stroke-width="2.5" stroke-dasharray="9 7"/>
 <text x="54" y="70" font-size="14" font-weight="700" fill="var(--text-muted)">YOUR PC</text>
 <rect x="110" y="86" width="350" height="42" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="285" y="113" text-anchor="middle" font-size="16" fill="var(--text)">your voice</text>
 <rect x="110" y="140" width="350" height="42" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="285" y="167" text-anchor="middle" font-size="16" fill="var(--text)">your journal</text>
 <rect x="110" y="194" width="350" height="42" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="285" y="221" text-anchor="middle" font-size="16" fill="var(--text)">your keys</text>
 <line x1="548" y1="122" x2="592" y2="122" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="606,122 590,114 590,130" fill="var(--accent-muted)"/>
 <rect x="618" y="88" width="242" height="68" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="739" y="118" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">A VERSION NUMBER</text>
 <text x="739" y="140" text-anchor="middle" font-size="14" fill="var(--text-muted)">the update check — switchable off</text>
 <line x1="548" y1="200" x2="592" y2="200" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="606,200 590,192 590,208" fill="var(--accent-muted)"/>
 <rect x="618" y="166" width="242" height="68" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="739" y="196" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">A SPEECH MODEL</text>
 <text x="739" y="218" text-anchor="middle" font-size="14" fill="var(--text-muted)">once, ~75 MB, coming in</text>
 <text x="440" y="288" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">No analytics. No metrics. No crash reports.</text>
 <text x="440" y="310" text-anchor="middle" font-size="14" fill="var(--text-muted)">Nothing in the dashed box ever crosses the line.</text>
</svg>
</section>
</div></div>

## The details

Download **`d47-setup.exe`** from the
[latest release](https://github.com/dseelinger/d47/releases/latest) and run it. There is no
runtime prerequisite, and it never asks for administrator rights.

It installs for your account only, into `%LOCALAPPDATA%\Programs\d47`, and appears in
Add/Remove Programs like anything else. Running a newer installer upgrades the existing
install in place rather than leaving two copies behind.

### The portable zip

`d47.zip` is published beside the installer for anyone who would rather not install: unpack
it into a folder you own — **not** `Program Files`, which D47 could not write to without
elevation it never asks for — and run `d47.exe`.

Either way the layout is the same: one executable, plus the speech-recognition native
libraries in a `runtimes` folder beside it. The executable is a self-contained publish, with
the .NET runtime and every managed dependency inside it; the natives sit beside it as plain
files because that is where their loader looks. **Keep them together** — `d47.exe` without
its `runtimes` folder starts fine but cannot transcribe a word.

What you can rely on, installed or portable:

- No .NET runtime needs to be installed first.
- No elevation, ever.
- Everything D47 writes lives in a `data` folder beside the executable. Move the whole
  folder, and the program, your settings and your secrets all move together.

### Uninstalling

Uninstall from Add/Remove Programs. It removes the program and asks — once — whether to
delete your `data` folder as well. Answering no keeps your settings, your saved keys and any
speech models you downloaded, so a later install picks up where you left off. The default is
to keep them.

### Verifying the download

Builds are unsigned, so Windows SmartScreen will likely warn — on the installer as readily as
on the executable. Rather than ask you to trust that, every release publishes a SHA-256
beside each download. Compare whichever you took:

```powershell
Get-FileHash .\d47-setup.exe -Algorithm SHA256
```

Match the result against `d47-setup.exe.sha256` from the same release — or `d47.zip.sha256`
if you took the zip. If it differs, do not run or unpack the file.

### Where things are written

Everything is under `data`, beside the executable:

```text
d47.exe
runtimes\
  win-x64\             speech-recognition natives — shipped with the build, not downloaded
data\
  settings.json        plain JSON, hand-editable, unknown keys rejected on load
  secrets.json         API keys, DPAPI-encrypted for your Windows account only
  models\              speech models — only if you chose one and agreed to the download
  logs\
    d47-20260812.log     human-readable
    d47-20260812.jsonl   the same events as newline-delimited JSON
```

### Finding it again

The installer adds a Start Menu entry, so this section is mostly for the portable zip.

Either way D47 is just the folder it lives in — the exe, its `runtimes` libraries, and
everything it writes in `data/` beside them. Copy that folder
and the whole thing, program and state, comes with it; move only `d47.exe` and it leaves its
ears behind.

The cost of that is a program you can only reach by remembering where you left it, so the
first run offers to add a Start Menu entry — unless the installer already made one, in which
case it stays quiet. It is one shortcut, for your account only, needs
no elevation, and you can delete it like any other. Decline it and you will not be asked
again; **Settings → About → Add to Start Menu** is there if you change your mind.

The shortcut points at the file rather than at a copy of it, so an in-place update leaves it
pointing at the new build — the same reason a pinned taskbar icon keeps working.

Only one D47 runs at a time. Starting a second one raises the copy you already have instead,
whichever version that is: two copies would mean two journal readers, two microphones and two
writers over one `data/` folder.

### Update checks

On startup, D47 asks GitHub's public releases API for the latest tag and compares it against
its own version. If a newer build exists, a banner offers **Update now**.

**Update now** downloads that release's `d47.zip`, checks it against the `d47.zip.sha256`
published beside it, and puts each file it contains where the running one is — the executable
and the `runtimes` libraries both — renaming what it replaces to `.old` rather than
overwriting it, then starts the new build. The `.old` files are deleted the next time D47
starts, and a failure part-way puts everything back; you never end up with half an update. If
any step fails — no build attached, the download did not finish, the checksum did not match,
the archive was not a d47 build, or the folder D47 lives in needs elevation to write — it says
which, and opens the release page so you can do it by hand.

Builds older than v0.5.14 predate the zip and cannot install it; their update banner opens the
release page instead, and moving up is the three download-unpack-run steps at the top of this
page once more.

Nothing is downloaded unless you press the button, and D47 will only fetch a URL that is an
asset on a release of this repository.

Offline or GitHub unreachable is treated as "no update" — startup never waits on the check and
never fails because of it.

### What leaves the machine

Nothing about you or your game. There is no analytics, no metrics endpoint and no crash
reporter.

Out of the box D47 makes two kinds of outbound request, and neither carries anything about
you:

- **The update check** described above — a GET with no Commander data, no journal data and
  nothing identifying. Switch it off in Settings and D47 makes no network call of its own.
  Pressing **Update now** on the banner it raises adds one more: downloading that release from
  GitHub. That is a download rather than an upload, and it happens only when you press it.
- **A speech model download** — once. D47 ships with the smallest English model selected and
  fetches it from `huggingface.co` on first run, about 75 MB; choosing a different one fetches
  that instead. Only the model file is transferred, nothing about you goes with the request, and
  once it is on disk transcription runs entirely on this machine — no audio and no transcript
  leaves it. Set the speech model to `none` if you would rather it fetched nothing.

Providers that send game-derived data off the machine — a cloud LLM, a paid voice, INARA, web
search — are each enabled individually and each states what it transmits.

You never have to take this page's word for any of it. D47 computes the answer from your
current settings: ask it *"what are you sending"*, or open the **Privacy and egress** section
of Settings. A page can go stale; that report cannot.

`secrets.json` is encrypted with Windows DPAPI scoped to your user account. Copying it to
another machine or another account leaves it undecryptable, and D47 treats an unreadable
secret as that capability being switched off rather than as an error.

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="conversation.html"><span class="ct">Talking to Directive 47 &rarr;</span><span class="cd">What happens between your question and its answer.</span></a>
<a class="card" href="capabilities/privacy.html"><span class="ct">Privacy &rarr;</span><span class="cd">The full report, worked out from your settings rather than promised here.</span></a>
<a class="card" href="capabilities/settings.html"><span class="ct">Settings &rarr;</span><span class="cd">Every switch, and what each one turns on.</span></a>
</div>
</div>
</div></div>
