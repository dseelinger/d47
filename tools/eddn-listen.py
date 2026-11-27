"""Subscribe to the EDDN relay and keep the messages one piece of software sent.

Needs pyzmq: pip install pyzmq

    python tools/eddn-listen.py <out.jsonl> [softwareName]

softwareName defaults to "Directive 47". Each kept message is appended to out.jsonl as one line of JSON,
exactly as the relay carried it, and prints one line: schema, event, StarSystem, BodyName. Ctrl+C prints
a count per schema; so does Ctrl+Break. The file holds the Commander's positions and scans: keep it out of the repository.
"""

import json
import signal
import sys
import zlib
from collections import Counter
from datetime import datetime

import zmq

RELAY = "tcp://eddn.edcd.io:9500"
SCHEMA_PREFIX = "https://eddn.edcd.io/schemas/"


def schema_name(ref):
    return ref[len(SCHEMA_PREFIX):] if ref.startswith(SCHEMA_PREFIX) else ref


def describe(envelope):
    message = envelope.get("message", {})
    parts = [schema_name(envelope.get("$schemaRef", "?"))]
    for key in ("event", "StarSystem", "BodyName"):
        value = message.get(key)
        if value is not None:
            parts.append(str(value))
    return "  ".join(parts)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    if hasattr(signal, "SIGBREAK"):
        signal.signal(signal.SIGBREAK, signal.default_int_handler)

    out_path = sys.argv[1]
    software = sys.argv[2] if len(sys.argv) > 2 else "Directive 47"

    context = zmq.Context()
    socket = context.socket(zmq.SUB)
    socket.setsockopt(zmq.SUBSCRIBE, b"")
    socket.setsockopt(zmq.RCVTIMEO, 1000)
    socket.connect(RELAY)

    counts = Counter()
    seen = 0
    print(f"Listening on {RELAY} for softwareName {software!r}; writing {out_path}", flush=True)

    try:
        with open(out_path, "a", encoding="utf-8") as out:
            while True:
                try:
                    frame = socket.recv()
                except zmq.Again:
                    continue

                seen += 1
                try:
                    envelope = json.loads(zlib.decompress(frame))
                except (zlib.error, ValueError):
                    continue

                if envelope.get("header", {}).get("softwareName") != software:
                    continue

                out.write(json.dumps(envelope, ensure_ascii=False) + "\n")
                out.flush()
                counts[schema_name(envelope.get("$schemaRef", "?"))] += 1
                print(f"{datetime.now():%H:%M:%S}  {describe(envelope)}", flush=True)
    except KeyboardInterrupt:
        pass
    finally:
        socket.close(linger=0)
        context.term()

    print(f"\n{sum(counts.values())} kept of {seen} relayed")
    for schema, count in sorted(counts.items()):
        print(f"{schema} {count}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
