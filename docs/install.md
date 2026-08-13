---
title: Installing d47
---

# Installing and verifying a build

d47 ships as one file. There is no installer, no runtime prerequisite, and nothing that
asks for administrator rights.

1. Download `d47.exe` from the
   [latest release](https://github.com/dseelinger/d47/releases/latest) — or pick a specific
   build from the [releases page](https://github.com/dseelinger/d47/releases).
2. Put it in a folder you own — **not** `Program Files`. d47 never asks for administrator
   rights, so it could not write there anyway; and everything it saves goes in a `data`
   folder beside the executable, so it needs somewhere you can write.
3. Run it.

## What "one file" does and does not mean

The build is a self-contained single-file publish: the .NET runtime and every managed
dependency are inside the executable, and native libraries are extracted to a temporary
folder on first run. It is not statically linked in the C sense. What you can rely on is
narrower and more useful:

- No .NET runtime needs to be installed first.
- No elevation, ever.
- Everything d47 writes lives in a `data` folder beside the executable. Move the folder,
  and your settings and secrets move with it.

First launch is slower than later ones, because that native extraction happens once.

## Verifying the download

Builds are unsigned, so Windows SmartScreen will likely warn on first run. Rather than ask
you to trust that, every release publishes a SHA-256 alongside the executable. Compare it:

```powershell
Get-FileHash .\d47.exe -Algorithm SHA256
```

Match the result against `d47.exe.sha256` from the same release. If it differs, do not run
the file.

## Where things are written

Everything is under `data`, beside the executable:

```text
d47.exe
data\
  settings.json        plain JSON, hand-editable, unknown keys rejected on load
  secrets.json         API keys, DPAPI-encrypted for your Windows account only
  models\              speech models — only if you chose one and agreed to the download
  logs\
    d47-20260812.log     human-readable
    d47-20260812.jsonl   the same events as newline-delimited JSON
```

## Update checks

On startup, d47 asks GitHub's public releases API for the latest tag and compares it against
its own version. If a newer build exists, a banner offers **Update now**.

**Update now** downloads that release's `d47.exe`, checks it against the `d47.exe.sha256`
published beside it, renames the running build to `d47.exe.old`, puts the new one in its place
and starts it. The old file is deleted the next time d47 starts. If any step fails — no build
attached, the download did not finish, the checksum did not match, or the folder d47 lives in
needs elevation to write — it says which, and opens the release page so you can do it by hand.

Nothing is downloaded unless you press the button, and d47 will only fetch a URL that is an
asset on a release of this repository.

Offline or GitHub unreachable is treated as "no update" — startup never waits on the check and
never fails because of it.

## What leaves the machine

Nothing about you or your game. There is no analytics, no metrics endpoint and no crash
reporter.

Out of the box d47 makes two kinds of outbound request, and neither carries anything about
you:

- **The update check** described above — a GET with no Commander data, no journal data and
  nothing identifying. Switch it off in Settings and d47 makes no network call of its own.
  Pressing **Update now** on the banner it raises adds one more: downloading that release from
  GitHub. That is a download rather than an upload, and it happens only when you press it.
- **A speech model download**, and only if you ask for one. No model is selected by default.
  Choosing one asks first, states the real size and the host it comes from, and downloads
  nothing until you agree. Once the file is on disk, transcription runs entirely on this
  machine — no audio and no transcript ever leaves it.

Providers that send game-derived data off the machine — a cloud LLM, a paid voice, INARA, web
search — are each enabled individually and each states what it transmits.

You never have to take this page's word for any of it. d47 computes the answer from your
current settings: ask it *"what are you sending"*, or open the **Privacy and egress** section
of Settings. A page can go stale; that report cannot.

`secrets.json` is encrypted with Windows DPAPI scoped to your user account. Copying it to
another machine or another account leaves it undecryptable, and d47 treats an unreadable
secret as that capability being switched off rather than as an error.
