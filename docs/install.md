---
title: Installing Directive 47
---

# Installing and verifying a build

Download **`d47-setup.exe`** from the
[latest release](https://github.com/dseelinger/d47/releases/latest) and run it. There is no
runtime prerequisite, and it never asks for administrator rights.

It installs for your account only, into `%LOCALAPPDATA%\Programs\d47`, and appears in
Add/Remove Programs like anything else. Running a newer installer upgrades the existing
install in place rather than leaving two copies behind.

## The portable zip

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

## Uninstalling

Uninstall from Add/Remove Programs. It removes the program and asks — once — whether to
delete your `data` folder as well. Answering no keeps your settings, your saved keys and any
speech models you downloaded, so a later install picks up where you left off. The default is
to keep them.

## Verifying the download

Builds are unsigned, so Windows SmartScreen will likely warn — on the installer as readily as
on the executable. Rather than ask you to trust that, every release publishes a SHA-256
beside each download. Compare whichever you took:

```powershell
Get-FileHash .\d47-setup.exe -Algorithm SHA256
```

Match the result against `d47-setup.exe.sha256` from the same release — or `d47.zip.sha256`
if you took the zip. If it differs, do not run or unpack the file.

## Where things are written

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

## Finding it again

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

## Update checks

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

## What leaves the machine

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
