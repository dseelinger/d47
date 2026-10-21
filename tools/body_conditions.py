"""Spansh access and the body-condition fold shared by gen-exobiology.py and gen-geology.py.

The fold mirrors `BodyConditionFold` in src/D47.Core/Knowledge/: spansh's words are folded when a
table is generated, the journal's when it is looked up.
"""

from __future__ import annotations

import json
import re
import time
import urllib.error
import urllib.request

SEARCH = "https://spansh.co.uk/api/bodies/search"

PAGE_SIZE = 250

PAUSE = 0.3

# A categorical value stays in a row once it is seen on at least this share of the sampled bodies.
CATEGORICAL_THRESHOLD = 0.02


def fetch(url: str, payload: dict[str, object] | None = None, agent: str = "d47-gen") -> dict[str, object]:
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"User-Agent": agent}

    if data is not None:
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(url, data=data, headers=headers)

    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            if attempt == 2:
                raise
            time.sleep(2 * (attempt + 1))

    raise AssertionError("unreachable")


def search(name: str, page: int, agent: str = "d47-gen") -> list[dict[str, object]]:
    body = fetch(SEARCH, {
        "filters": {"landmark_subtype": {"value": [name]}},
        "sort": [],
        "size": PAGE_SIZE,
        "page": page,
    }, agent)
    results = body.get("results", [])
    assert isinstance(results, list)
    return results


def normalise_planet_type(raw: str | None) -> str:
    words = ["body" if word == "world" else word for word in re.findall(r"[a-z]+", (raw or "").lower())]
    return "".join(words)


def normalise_atmosphere(raw: str | None) -> str:
    text = (raw or "").strip().lower()

    if text in ("", "no atmosphere"):
        return "none"

    text = text.replace("sulphur", "sulfur")
    stopwords = {"thin", "thick", "hot", "atmosphere"}

    return "".join(word for word in re.findall(r"[a-z]+", text) if word not in stopwords)


def normalise_volcanism(raw: str | None) -> str:
    text = (raw or "").strip().lower()

    if text in ("", "no volcanism"):
        return "none"

    return "".join(word for word in re.findall(r"[a-z]+", text) if word != "volcanism")


def categorical(values: list[str], threshold: float) -> list[str]:
    counts: dict[str, int] = {}

    for value in values:
        counts[value] = counts.get(value, 0) + 1

    kept = [value for value, count in counts.items() if count / len(values) >= threshold]
    return sorted(kept)
