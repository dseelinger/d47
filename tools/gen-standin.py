#!/usr/bin/env python3
"""Render the bundled stand-in clip for the Guardian voice Test button (#226).

This is *not* part of the build. Its output — `assets/standin/guardian-standin.wav` — is
committed, and the app never runs this script; it exists so the shipped clip is reproducible
rather than a binary nobody can account for. Run it only when the stand-in line changes:

    python tools/gen-standin.py [--model-dir PATH] [--voice ID]

Test plays this clip, untreated, whenever no free and unbilled voice can be heard — see
`AppHost.StandInFor` and `docs/capabilities/speech.md`.

Requires numpy and onnxruntime, and a Kokoro install to read from: the same `model.onnx`,
`tokenizer.json` and `voices/*.bin` that `KokoroTtsProvider` (src/D47.Tts) reads at runtime,
already on disk once the app's local voice has been downloaded once (Settings > Speech > Kokoro).
By default this looks in `%LOCALAPPDATA%\\Programs\\d47\\data\\models\\kokoro`, matching the
installer's per-user, unversioned install path (`installer/d47.iss`).

The stand-in line is spelled out here as plain text and looked up word by word in Kokoro's own
pronunciation dictionary (`phoneme_dict.json`, the `en_us` table) — the same dictionary
`PhonemeDictionary` reads, and the top rung of D47's own phonemisation ladder. Changing the line
means every word in it must still be in that dictionary; the script fails loudly if one is not,
rather than guessing a pronunciation.
"""

import argparse
import json
import os
import re
import wave

import numpy as np
import onnxruntime

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")

DEFAULT_MODEL_DIR = os.path.join(
    os.environ.get("LOCALAPPDATA", ""), "Programs", "d47", "data", "models", "kokoro")

OUTPUT_PATH = os.path.join(ROOT, "assets", "standin", "guardian-standin.wav")

DEFAULT_VOICE = "af_heart"

# The clip Test plays when no free, unbilled voice is available to prove the treatments on
# (case 4 of #226's four). It says plainly that it is a stand-in, since a Commander hearing it
# has no other way to tell.
LINE = (
    "Guardian core online, Commander. This is a stand in voice. The one you chose will speak "
    "once it costs nothing."
)

MODEL_SAMPLE_RATE = 24_000
STYLE_DIMENSIONS = 256


def read_vocabulary(model_dir):
    with open(os.path.join(model_dir, "tokenizer.json"), encoding="utf-8") as handle:
        return json.load(handle)["model"]["vocab"]


def read_dictionary(model_dir):
    with open(os.path.join(model_dir, "phoneme_dict.json"), encoding="utf-8") as handle:
        return json.load(handle)["en_us"]


def phonemise(line, dictionary):
    """Word-by-word dictionary lookup, joined by Kokoro's own word-boundary symbol.

    Not a general phonemiser — every word in LINE must already be in the dictionary. That is
    the deliberate trade: a fixed, checked line rather than reimplementing D47's phonemisation
    ladder (rules, stress, heteronyms) for a clip that is spoken once and then never again.
    """
    words = re.findall(r"[A-Za-z']+", line.lower())
    missing = [word for word in words if word not in dictionary]

    if missing:
        raise SystemExit(f"not in the pronunciation dictionary: {', '.join(missing)}")

    return " ".join(dictionary[word] for word in words)


def encode(phonemes, vocabulary):
    ids = [0]

    for symbol in phonemes:
        if symbol in vocabulary:
            ids.append(vocabulary[symbol])

    ids.append(0)
    return np.array(ids, dtype=np.int64)


def style_for(model_dir, voice, tokens):
    with open(os.path.join(model_dir, "voices", f"{voice}.bin"), "rb") as handle:
        floats = np.frombuffer(handle.read(), dtype=np.float32)

    buckets = len(floats) // STYLE_DIMENSIONS
    bucket = max(0, min(tokens, buckets - 1))
    return floats[bucket * STYLE_DIMENSIONS:(bucket + 1) * STYLE_DIMENSIONS]


def synthesise(model_dir, voice, line):
    vocabulary = read_vocabulary(model_dir)
    dictionary = read_dictionary(model_dir)
    phonemes = phonemise(line, dictionary)

    tokens = encode(phonemes, vocabulary)

    if len(tokens) <= 2:
        raise SystemExit("nothing sayable came out of that line")

    style = style_for(model_dir, voice, len(tokens))

    session = onnxruntime.InferenceSession(os.path.join(model_dir, "model.onnx"))
    result = session.run(None, {
        "input_ids": tokens.reshape(1, -1),
        "style": style.reshape(1, -1),
        "speed": np.array([1.0], dtype=np.float32),
    })

    return result[0].reshape(-1)


def upsample_double(samples_24k):
    """24 kHz to the arbiter's 48 kHz: each sample kept, a midpoint inserted after it — the
    exact doubling `PcmUpsample.Double` performs on the 16-bit PCM at playback time."""
    doubled = np.empty(len(samples_24k) * 2, dtype=np.float32)
    doubled[0::2] = samples_24k
    midpoints = (samples_24k[:-1] + samples_24k[1:]) / 2
    doubled[1:-1:2] = midpoints
    doubled[-1] = samples_24k[-1]
    return doubled


def write_wav(path, samples_48k):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = np.clip(samples_48k, -1, 1).astype(np.float32) * 32767

    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(48_000)
        handle.writeframes(pcm.astype("<i2").tobytes())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-dir", default=DEFAULT_MODEL_DIR)
    parser.add_argument("--voice", default=DEFAULT_VOICE)
    args = parser.parse_args()

    samples = synthesise(args.model_dir, args.voice, LINE)
    write_wav(OUTPUT_PATH, upsample_double(samples))

    seconds = len(samples) / MODEL_SAMPLE_RATE
    print(f"{os.path.relpath(OUTPUT_PATH, ROOT)}  {seconds:.2f}s  voice={args.voice}")


if __name__ == "__main__":
    main()
