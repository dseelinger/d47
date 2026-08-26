"""Does OpenAI's TTS drift language on Elite system names, and is `speed` honoured?

Answers GitHub issue 48, which blocks writing Phase 58 of
docs/plans/per-role-voice-providers.md. Finding:
docs/spikes/openai-tts-language-and-speed.md.

    python spike/OpenAiVoiceProbe/probe_speech.py [--out DIR]

Needs an OpenAI key. Set OPENAI_API_KEY, or leave it unset and this reads the one d47 already
holds — `openai.apiKey` from the DPAPI store beside the executable, decrypted in this process and
never printed.

Three questions, in the order they decide Phase 58:

1. **Language.** Synthesise a Guardian-voice line seeded with the proper nouns the brief names,
   then send the audio back through OpenAI's *multilingual* transcription and read off the
   language it detects. A local Whisper cannot answer this: d47 ships `ggml-base.en`, and an
   English-only model transcribes whatever it hears as English words, which is precisely the
   failure being looked for.
2. **Speed.** The same input at each documented rate, measured as WAV duration. Honoured means
   duration scales as 1/speed; ignored means it does not move.
3. **The closed schema.** `language` is sent once, on purpose, so the claim Phase 58 rests on is
   measured rather than read: `additionalProperties: false` should *reject* it rather than ignore
   it.

The audio is kept so a person can listen. Nothing here can hear an accent, and an accent on the
proper nouns is a pass while a switch of language is the failure — so the clips are the evidence
and this is the instrument.
"""

import argparse
import base64
import ctypes
import ctypes.wintypes
import io
import json
import os
import struct
import sys
import urllib.error
import urllib.request

MODEL = "gpt-4o-mini-tts-2025-12-15"

# The transcription model is chosen for one property: it reports the language it heard.
TRANSCRIBE_MODEL = "whisper-1"

VOICE = "onyx"

# A Guardian core, in its own register, carrying every proper noun the issue names. The numerals
# are sent as written rather than expanded, because that is what reaches a provider today.
LINE = (
    "Directive forty-seven acknowledged. Course is laid to Shinrarta Dezhra by way of Ngalinn "
    "and Deciat. LHS 3447 remains within the tolerance you set. HIP 21991 and HIP 63835 are held "
    "in reserve. Your inferior systems will be optimised."
)

# The same sentence shape with no proper nouns in it. If the seeded line drifts and this one does
# not, the names are the cause rather than the voice or the model.
CONTROL = (
    "Directive forty-seven acknowledged. The course is laid and the tolerance you set is being "
    "held. Two alternatives are in reserve. Your inferior systems will be optimised."
)

SPEEDS = [0.25, 0.5, 1.0, 1.5, 2.0, 4.0]


def key_from_environment():
    return os.environ.get("OPENAI_API_KEY")


def key_from_d47():
    """d47's own stored key, decrypted here and returned — never printed, never written out."""
    stores = [
        os.path.join("dev-install", "data", "secrets.json"),
        os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", "d47", "data", "secrets.json"),
    ]

    for store in stores:
        if not os.path.isfile(store):
            continue

        with io.open(store, encoding="utf-8") as handle:
            held = json.load(handle)

        if "openai.apiKey" not in held:
            continue

        blob = base64.b64decode(held["openai.apiKey"])
        plain = unprotect(blob)

        if plain:
            return plain.decode("utf-8"), store

    return None, None


class Blob(ctypes.Structure):
    _fields_ = [("cbData", ctypes.wintypes.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]


def unprotect(ciphertext):
    """DPAPI, CurrentUser, no entropy — the shape `DpapiSecretProtector` writes."""
    source = Blob(len(ciphertext), ctypes.cast(ctypes.create_string_buffer(ciphertext),
                                               ctypes.POINTER(ctypes.c_char)))
    out = Blob()

    ok = ctypes.windll.crypt32.CryptUnprotectData(
        ctypes.byref(source), None, None, None, None, 0, ctypes.byref(out))

    if not ok:
        return None

    try:
        return ctypes.string_at(out.pbData, out.cbData)
    finally:
        ctypes.windll.kernel32.LocalFree(out.pbData)


def speak(key, text, speed=None, extra=None, response_format="wav"):
    """One /v1/audio/speech call. Answers (status, bytes-or-error-text)."""
    body = {"model": MODEL, "input": text, "voice": VOICE, "response_format": response_format}

    if speed is not None:
        body["speed"] = speed

    if extra:
        body.update(extra)

    request = urllib.request.Request(
        "https://api.openai.com/v1/audio/speech",
        data=json.dumps(body).encode("utf-8"),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
        method="POST")

    try:
        with urllib.request.urlopen(request, timeout=120) as answer:
            return answer.status, answer.read()
    except urllib.error.HTTPError as failed:
        return failed.code, failed.read()


def transcribe(key, wav_path):
    """Send the audio back and read off what was heard, and in what language."""
    boundary = "----d47probe"
    parts = []

    def field(name, value):
        parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n"
                     f"{value}\r\n".encode("utf-8"))

    field("model", TRANSCRIBE_MODEL)
    field("response_format", "verbose_json")

    with io.open(wav_path, "rb") as handle:
        audio = handle.read()

    parts.append(
        (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; "
         f"filename=\"{os.path.basename(wav_path)}\"\r\nContent-Type: audio/wav\r\n\r\n"
         ).encode("utf-8"))
    parts.append(audio)
    parts.append(f"\r\n--{boundary}--\r\n".encode("utf-8"))

    request = urllib.request.Request(
        "https://api.openai.com/v1/audio/transcriptions",
        data=b"".join(parts),
        headers={"Authorization": f"Bearer {key}",
                 "Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST")

    try:
        with urllib.request.urlopen(request, timeout=180) as answer:
            return answer.status, json.loads(answer.read())
    except urllib.error.HTTPError as failed:
        return failed.code, {"error": failed.read().decode("utf-8", "replace")[:400]}


def wav_seconds(raw):
    """Duration off the RIFF header, so nothing has to be decoded to measure it."""
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        return None

    at = 12
    rate = channels = bits = None

    while at + 8 <= len(raw):
        chunk = raw[at:at + 4]
        size = struct.unpack("<I", raw[at + 4:at + 8])[0]
        body = raw[at + 8:at + 8 + size]

        if chunk == b"fmt " and len(body) >= 16:
            channels, rate = struct.unpack("<HI", body[2:8])
            bits = struct.unpack("<H", body[14:16])[0]
        elif chunk == b"data" and rate:
            frame = max(1, channels * (bits // 8))
            return len(body) / (rate * frame)

        at += 8 + size + (size % 2)

    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="openai-tts-spike")
    parser.add_argument("--only", choices=["all", "schema", "language", "speed"], default="all")
    arguments = parser.parse_args()

    key = key_from_environment()
    source = "OPENAI_API_KEY"

    if not key:
        key, store = key_from_d47()
        source = f"d47's secret store ({store})"

    if not key:
        print("No OpenAI key. Set OPENAI_API_KEY, or store one in d47's settings.")
        return 2

    os.makedirs(arguments.out, exist_ok=True)

    print(f"model     {MODEL}")
    print(f"voice     {VOICE}")
    print(f"key from  {source}")
    print(f"audio to  {os.path.abspath(arguments.out)}")

    # ---------------------------------------------------------------- 3. the closed schema
    #
    # This is the half that came back the opposite way round from the brief, so it is asked in
    # three parts rather than one. `language` being *accepted* is only interesting if the endpoint
    # is actually reading it — an API that waves through any unknown property accepts `language`
    # for the same reason it accepts nonsense, and that is not a capability.
    if arguments.only in ("all", "schema"):
        print("\n== Can it be told a language at all ==")

        for label, extra in (
                ("language=fr", {"language": "fr"}),
                ("a field that cannot mean anything", {"d47_nonsense_field": "banana"}),
                ("a valid field with an invalid value", {"speed": 99}),
        ):
            status, answer = speak(key, "Test.", extra=extra)
            detail = answer.decode("utf-8", "replace") if isinstance(answer, bytes) else str(answer)

            if status == 200:
                print(f"  {label:<38} -> HTTP 200, accepted")
            else:
                shown = detail.strip().replace("\n", " ")[:180]
                print(f"  {label:<38} -> HTTP {status}: {shown}")

        # And the decisive one: does the field change the audio, or is it merely tolerated? Same
        # English input twice, once with a French tag, both read back by a multilingual ear.
        print("\n  Does the tag change what comes out?")

        for label, extra in (("no language field", None), ("language=fr", {"language": "fr"})):
            status, audio = speak(key, CONTROL, extra=extra)

            if status != 200:
                print(f"    {label}: HTTP {status}")
                continue

            path = os.path.join(arguments.out, f"tag-{label.replace(' ', '-').replace('=', '-')}.wav")

            with io.open(path, "wb") as handle:
                handle.write(audio)

            status, transcript = transcribe(key, path)
            seconds = wav_seconds(audio)

            if status == 200:
                print(f"    {label:<20} {seconds:5.2f}s  heard as [{transcript.get('language')}]: "
                      f"{transcript.get('text', '').strip()[:110]}")
            else:
                print(f"    {label:<20} transcription HTTP {status}")

    # ---------------------------------------------------------------- 1. language
    heard = {}

    if arguments.only in ("all", "language"):
        print("\n== Language, on the seeded line ==")

        for name, text in (("seeded", LINE), ("control", CONTROL)):
            status, audio = speak(key, text)

            if status != 200:
                print(f"  {name}: HTTP {status} {audio[:200]}")
                continue

            path = os.path.join(arguments.out, f"{name}.wav")

            with io.open(path, "wb") as handle:
                handle.write(audio)

            seconds = wav_seconds(audio)
            print(f"  {name}: {len(audio):,} bytes, {seconds:.2f}s -> {path}")

            status, transcript = transcribe(key, path)

            if status == 200:
                heard[name] = transcript
                print(f"    heard as [{transcript.get('language')}]: {transcript.get('text', '').strip()}")
            else:
                print(f"    transcription HTTP {status}: {transcript}")

        # Which of the seeded names came back intact, spelled as they were sent. A name transcribed
        # as something else is not proof of drift on its own — it is where to listen first.
        if "seeded" in heard:
            said = heard["seeded"].get("text", "")
            print("\n  proper nouns, as transcribed back:")

            for name in ["Shinrarta", "Dezhra", "Ngalinn", "Deciat", "LHS", "3447", "21991", "63835"]:
                print(f"    {'yes' if name.lower() in said.lower() else 'NO '}  {name}")

    # ---------------------------------------------------------------- 2. speed
    if arguments.only not in ("all", "speed"):
        return 0

    print("\n== Is `speed` honoured ==")
    baseline = None

    for speed in SPEEDS:
        status, audio = speak(key, CONTROL, speed=speed)

        if status != 200:
            print(f"  speed={speed}: HTTP {status} {audio[:200]}")
            continue

        seconds = wav_seconds(audio)
        path = os.path.join(arguments.out, f"speed-{speed}.wav")

        with io.open(path, "wb") as handle:
            handle.write(audio)

        if speed == 1.0:
            baseline = seconds

        print(f"  speed={speed:<5} {seconds:6.2f}s", end="")

        if baseline:
            print(f"   {baseline / seconds:5.2f}x faster than 1.0 (expected {speed:.2f}x)")
        else:
            print()

    if baseline:
        print(f"\n  1.0 is {baseline:.2f}s. A rate that is ignored leaves every row at that figure.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
