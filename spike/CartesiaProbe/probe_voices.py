"""Cartesia, measured rather than read about.

Answers the three questions Phase 60 of docs/plans/per-role-voice-providers.md says must be
answered before the phase is written:

  1. How many voices are there, and how are they tagged? The library size is unpublished, and
     variety is the entire reason the phase exists. If this comes back smaller than the
     ElevenLabs account already offers, the phase is re-argued rather than built.
  2. What is the billing unit? SpeechSpend counts characters. ElevenLabs bills per character and
     OpenAI publishes per minute, and Phase 58 had to refuse to quote a figure because the two
     do not convert. Which of those Cartesia is decides whether its settings row can show a rate.
  3. What is the speed range, and is it honoured? change-requests.md 43 was answered on
     2026-08-26 by taking ElevenLabs' range as the common denominator for every provider. If
     Cartesia's range is tighter, that ruling has to be re-taken rather than inherited.

Needs a Cartesia key. Set CARTESIA_API_KEY, or leave it unset and this reads `cartesia.apiKey`
from d47's own DPAPI store beside the executable, decrypted in this process. The key is never
printed and never written to a file - it goes into the request header and nowhere else.

    python spike/CartesiaProbe/probe_voices.py
    python spike/CartesiaProbe/probe_voices.py --only voices
    python spike/CartesiaProbe/probe_voices.py --out cartesia-spike

Throwaway, like OpenAiVoiceProbe beside it. The finding belongs in docs/spikes/, not here.
"""

import argparse
import base64
import collections
import ctypes
import ctypes.wintypes
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.cartesia.ai"

# Cartesia pins the API by date rather than by a path segment, so this is part of the finding:
# whatever is measured here was measured against this version.
VERSION = "2024-11-13"

# The same line the OpenAI probe used, so the two spikes are comparable: a Guardian sentence
# seeded with Elite proper nouns that a language-guessing model tends to mistake for French.
LINE = (
    "Commander, the route runs Shinrarta Dezhra to Ngalinn, then Deciat and LHS 3447, "
    "with HIP 20277 and HIP 12099 beyond that."
)


def key_from_environment():
    return os.environ.get("CARTESIA_API_KEY")


def key_from_d47():
    """d47's own stored key, decrypted here and returned - never printed, never written out."""

    candidates = [
        os.path.join(os.getcwd(), "dev-install", "data", "secrets.json"),
        os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "d47", "data", "secrets.json"),
    ]

    for path in candidates:
        if not os.path.exists(path):
            continue

        with open(path, encoding="utf-8") as handle:
            held = json.load(handle)

        if "cartesia.apiKey" not in held:
            continue

        return unprotect(held["cartesia.apiKey"])

    return None


def unprotect(stored):
    """DPAPI CryptUnprotectData, which is what SecretStore writes and only this user can read."""

    class Blob(ctypes.Structure):
        _fields_ = [("cbData", ctypes.wintypes.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]

    raw = base64.b64decode(stored)
    source = Blob(len(raw), ctypes.cast(ctypes.create_string_buffer(raw), ctypes.POINTER(ctypes.c_char)))
    out = Blob()

    if not ctypes.windll.crypt32.CryptUnprotectData(
        ctypes.byref(source), None, None, None, None, 0, ctypes.byref(out)
    ):
        raise RuntimeError("CryptUnprotectData refused the stored key")

    try:
        return ctypes.string_at(out.pbData, out.cbData).decode("utf-8")
    finally:
        ctypes.windll.kernel32.LocalFree(out.pbData)


def call(key, method, path, body=None, raw=False):
    request = urllib.request.Request(
        f"{API}{path}",
        method=method,
        data=json.dumps(body).encode("utf-8") if body is not None else None,
        headers={
            "X-API-Key": key,
            "Cartesia-Version": VERSION,
            **({"Content-Type": "application/json"} if body is not None else {}),
        },
    )

    started = time.time()

    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            payload = response.read()
            return response.status, payload if raw else json.loads(payload), time.time() - started
    except urllib.error.HTTPError as error:
        # The body of a refusal is the interesting half - it is what tells a wrong key from an
        # out-of-range parameter, which is the distinction every provider's Check button rests on.
        return error.code, error.read().decode("utf-8", "replace"), time.time() - started


def voices(key):
    """Question 1: how many, and tagged how."""

    print("== voices ==")

    collected = []
    path = "/voices/?limit=100"

    while path:
        status, payload, _ = call(key, "GET", path)

        if status != 200:
            print(f"  FAILED {status}: {str(payload)[:400]}")
            return

        page = payload.get("data", payload if isinstance(payload, list) else [])
        collected.extend(page)

        nxt = payload.get("next_page") if isinstance(payload, dict) else None
        has_more = payload.get("has_more") if isinstance(payload, dict) else False
        path = f"/voices/?limit=100&starting_after={nxt}" if (has_more and nxt) else None

    print(f"  {len(collected)} voices")

    if not collected:
        return

    print(f"  fields on one: {sorted(collected[0].keys())}")

    for field in ("language", "gender", "is_public", "is_owner"):
        counts = collections.Counter(
            str(voice.get(field)) for voice in collected if field in voice
        )
        if counts:
            top = ", ".join(f"{value}={count}" for value, count in counts.most_common(8))
            print(f"  by {field}: {top}")

    english = [v for v in collected if str(v.get("language", "")).startswith("en")]
    print(f"  English voices: {len(english)}")
    print(f"  ids are opaque: {not any(str(v.get('id', '')).isalpha() for v in collected[:5])}")


def speed(key, out):
    """Question 3: what the speed range is, and whether it moves the audio."""

    print("== speed ==")
    os.makedirs(out, exist_ok=True)

    status, payload, _ = call(key, "GET", "/voices/?limit=1")

    if status != 200 or not (payload.get("data") if isinstance(payload, dict) else payload):
        print(f"  no voice to speak with ({status})")
        return

    first = (payload.get("data") if isinstance(payload, dict) else payload)[0]
    voice_id = first.get("id")

    print(f"  speaking with {first.get('name', '?')}")

    # The documented enum first, then the numbers either side of it. A provider that refuses an
    # out-of-range value is one d47 can adapt to; one that accepts and ignores it is invisible,
    # which is the failure that moved the ElevenLabs pin off Multilingual 2.
    for label in ("slowest", "slow", "normal", "fast", "fastest", -1.0, 0.0, 1.0, 2.0):
        body = {
            "model_id": "sonic-2",
            "transcript": LINE,
            "voice": {"mode": "id", "id": voice_id},
            "output_format": {"container": "wav", "encoding": "pcm_s16le", "sample_rate": 44100},
            "language": "en",
            "speed": label,
        }

        status, payload, elapsed = call(key, "POST", "/tts/bytes", body, raw=True)

        if status != 200:
            print(f"  speed={label!r:>10}  REFUSED {status}: {str(payload)[:160]}")
            continue

        seconds = (len(payload) - 44) / (44100 * 2)
        name = os.path.join(out, f"speed-{str(label).replace('.', '_')}.wav")

        with open(name, "wb") as handle:
            handle.write(payload)

        print(f"  speed={label!r:>10}  {seconds:5.2f}s audio  ({elapsed:.1f}s call)  -> {name}")


def billing(key):
    """Question 2: what unit the account is charged in."""

    print("== billing unit ==")

    for path in ("/balance", "/usage", "/subscriptions/current", "/account"):
        status, payload, _ = call(key, "GET", path)
        print(f"  GET {path}: {status} {str(payload)[:220]}")

    print("  (a 404 here is a finding too - it means the unit has to come from the price page)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", choices=["voices", "speed", "billing"])
    parser.add_argument("--out", default="cartesia-spike")
    args = parser.parse_args()

    key = key_from_environment() or key_from_d47()

    if not key:
        print(
            "No key. Set CARTESIA_API_KEY, or store cartesia.apiKey in d47's secrets.json.",
            file=sys.stderr,
        )
        return 2

    print(f"Cartesia probe - API {VERSION}\n")

    if args.only in (None, "voices"):
        voices(key)
    if args.only in (None, "billing"):
        billing(key)
    if args.only in (None, "speed"):
        speed(key, args.out)

    return 0


if __name__ == "__main__":
    sys.exit(main())
