#!/usr/bin/env python3
"""Build the index of rare goods that d47 sorts by profit.

Not part of the build. Run by .github/workflows/rare-index.yml; writes only the --out file.

    python tools/rare-index.py --out <rares.tsv> [--previous <rares.tsv>] [--rares <Rares.tsv>]
    python tools/rare-index.py --check <fixture folder>

Reads each rare's symbol and market id from src/D47.Core/Knowledge/Rares.tsv. For each rare it asks Ardent for
the source station's buy price, stock and updatedAt (v2/market/<market_id>/commodity/name/<symbol>). The far
sell price follows gen-rares.py: from v2/commodity/name/<symbol>/imports?maxDaysAgo=30&fleetCarriers=false, the
price the most rows report, rows with stationType FleetCarrier left out, a tie taking the higher price, blank
when fewer than three agree. The far prices are asked for again only when the previous file's are more than 20
hours old, when there is no previous file, or for a rare the previous file lacks; otherwise they are carried
forward.

A 404 from the market endpoint blanks that rare's source columns. Any other failure keeps the previous row's
source columns (or far columns, for a failed imports call). When every market call fails, the run fails before
--out is written. A reported stock of 0 keeps the previous non-zero stock and its reported_at, and takes the new
buy_price only when it is non-zero.

--check runs each subfolder of the fixture folder: rares.tsv, ardent.json (the answers, and "now"), optional
previous.tsv, and expected.tsv, compared without the "# built" line.

File: UTF-8 TSV. Lines `# built <UTC>` and `# far prices <UTC>`, the header, then one row per rare ordered by
symbol. A blank field means unknown.
"""
import argparse
import datetime
import json
import os
import sys
import time
import urllib.error
import urllib.request

MARKET_URL = "https://api.ardent-insight.com/v2/market/{market_id}/commodity/name/{symbol}"
IMPORTS_URL = ("https://api.ardent-insight.com/v2/commodity/name/{symbol}/imports"
               "?maxDaysAgo=30&fleetCarriers=false")
USER_AGENT = "d47-rare-index (+https://github.com/dseelinger/d47)"
HEADER = "symbol\tfar_sell_price\tfar_reports\tbuy_price\tstock\treported_at"
FAR_MAX_AGE = datetime.timedelta(hours=20)
MIN_AGREEING_STATIONS = 3
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_RARES = os.path.join(REPO_ROOT, "src", "D47.Core", "Knowledge", "Rares.tsv")

COLUMNS = HEADER.split("\t")
FAR = ("far_sell_price", "far_reports")
SOURCE = ("buy_price", "stock", "reported_at")


class NotFound(Exception):
    pass


class Failed(Exception):
    pass


class Ardent:
    def market(self, market_id, symbol):
        return self._get(MARKET_URL.format(market_id=market_id, symbol=symbol))

    def imports(self, symbol):
        return self._get(IMPORTS_URL.format(symbol=symbol))

    @staticmethod
    def _get(url):
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        for attempt in range(4):
            try:
                with urllib.request.urlopen(request, timeout=30) as response:
                    return json.loads(response.read())
            except urllib.error.HTTPError as e:
                if e.code == 404:
                    raise NotFound(url)
                if e.code == 429 and attempt < 3:
                    time.sleep(float(e.headers.get("Retry-After", 0)) or 5 * (attempt + 1))
                    continue
                raise Failed(f"{url}: HTTP {e.code}")
            except (urllib.error.URLError, OSError, ValueError) as e:
                raise Failed(f"{url}: {e}")
        raise Failed(url)


class Recorded:
    """Answers read from a fixture's ardent.json."""

    def __init__(self, data):
        self.data = data

    def market(self, market_id, symbol):
        answer = self.data["market"][f"{market_id}/{symbol}"]
        if answer == "404":
            raise NotFound(symbol)
        if answer == "fail":
            raise Failed(symbol)
        return answer

    def imports(self, symbol):
        if "imports" not in self.data or symbol not in self.data["imports"]:
            raise AssertionError(f"imports asked for {symbol}")
        answer = self.data["imports"][symbol]
        if answer == "fail":
            raise Failed(symbol)
        return answer


def read_rares(path):
    rares = []
    with open(path, encoding="utf-8", newline="") as f:
        header = None
        for line in f:
            line = line.rstrip("\r\n")
            if not line or line.startswith("#"):
                continue
            cells = line.split("\t")
            if header is None:
                header = cells
                continue
            rares.append((cells[header.index("symbol")], cells[header.index("market_id")]))
    return sorted(rares)


def read_index(path):
    """Returns (far_stamp, {symbol: {column: text}}); (None, {}) when the file is missing."""
    if not path or not os.path.exists(path):
        return None, {}
    stamp, rows = None, {}
    with open(path, encoding="utf-8", newline="") as f:
        for line in f:
            line = line.rstrip("\r\n")
            if line.startswith("# far prices "):
                stamp = parse_time(line[len("# far prices "):])
            elif line.startswith("#") or not line or line == HEADER:
                continue
            else:
                cells = line.split("\t")
                if len(cells) != len(COLUMNS):
                    raise SystemExit(f"{path}: row with {len(cells)} columns")
                rows[cells[0]] = dict(zip(COLUMNS, cells))
    return stamp, rows


def parse_time(text):
    return datetime.datetime.fromisoformat(text.replace("Z", "+00:00"))


def stamp_text(moment):
    return moment.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def whole(value):
    return "" if value is None else str(int(value))


def far_price(rows):
    """The sell price most non-carrier rows report, or (blank, blank) without three that agree."""
    counts = {}
    for row in rows:
        price = row.get("sellPrice") or 0
        if row.get("stationType") != "FleetCarrier" and price > 0:
            counts[price] = counts.get(price, 0) + 1
    if not counts:
        return "", ""
    price, agreeing = max(counts.items(), key=lambda item: (item[1], item[0]))
    return (str(price), str(agreeing)) if agreeing >= MIN_AGREEING_STATIONS else ("", "")


def source_columns(quote, previous):
    buy, stock, reported = whole(quote.get("buyPrice")), whole(quote.get("stock")), quote.get("updatedAt") or ""
    if stock == "0" and previous.get("stock") not in (None, "", "0"):
        return {"buy_price": buy if buy not in ("", "0") else previous.get("buy_price", ""),
                "stock": previous["stock"], "reported_at": previous.get("reported_at", "")}
    return {"buy_price": buy, "stock": stock, "reported_at": reported}


def build(rares, previous_stamp, previous, ardent, now):
    refresh_far = previous_stamp is None or now - previous_stamp > FAR_MAX_AGE
    rows, answered, far_complete = [], 0, True
    for symbol, market_id in rares:
        old = previous.get(symbol, {})
        row = {c: old.get(c, "") for c in COLUMNS}
        row["symbol"] = symbol

        try:
            quote = ardent.market(market_id, symbol)
            answered += 1
            row.update(source_columns(quote, old) if isinstance(quote, dict) else dict.fromkeys(SOURCE, ""))
        except NotFound:
            answered += 1
            row.update(dict.fromkeys(SOURCE, ""))
        except Failed as e:
            print(f"kept previous row for {symbol}: {e}", file=sys.stderr)

        if refresh_far or symbol not in previous:
            try:
                price, reports = far_price(ardent.imports(symbol))
                row["far_sell_price"], row["far_reports"] = price, reports
            except (NotFound, Failed) as e:
                far_complete = False
                print(f"kept previous far price for {symbol}: {e}", file=sys.stderr)
        rows.append(row)

    if rares and answered == 0:
        raise SystemExit("every market call failed; nothing written")

    far_stamp = now if (refresh_far and far_complete) or previous_stamp is None else previous_stamp
    return far_stamp, rows


def render(built, far_stamp, rows):
    lines = [f"# built {stamp_text(built)}", f"# far prices {stamp_text(far_stamp)}", HEADER]
    lines += ["\t".join(r[c] for c in COLUMNS) for r in rows]
    return "\n".join(lines) + "\n"


def without_built(text):
    return [line for line in text.splitlines() if not line.startswith("# built ")]


def check(folder):
    failures, cases = 0, sorted(n for n in os.listdir(folder) if os.path.isdir(os.path.join(folder, n)))
    for name in cases:
        case = os.path.join(folder, name)
        with open(os.path.join(case, "ardent.json"), encoding="utf-8") as f:
            data = json.load(f)
        now = parse_time(data["now"])
        stamp, previous = read_index(os.path.join(case, "previous.tsv"))
        far_stamp, rows = build(read_rares(os.path.join(case, "rares.tsv")), stamp, previous, Recorded(data), now)
        actual = without_built(render(now, far_stamp, rows))
        with open(os.path.join(case, "expected.tsv"), encoding="utf-8", newline="") as f:
            expected = without_built(f.read())
        if actual != expected:
            failures += 1
            print(f"rare-index check failed: {name}", file=sys.stderr)
            for line in actual:
                print("  got      " + line, file=sys.stderr)
            for line in expected:
                print("  expected " + line, file=sys.stderr)
    if failures:
        return 1
    print(f"rare-index check passed: {len(cases)} cases")
    return 0


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out")
    p.add_argument("--previous")
    p.add_argument("--rares", default=DEFAULT_RARES)
    p.add_argument("--check")
    a = p.parse_args()
    if a.check:
        return check(a.check)
    if not a.out:
        p.error("--out is required")
    now = datetime.datetime.now(datetime.timezone.utc)
    stamp, previous = read_index(a.previous)
    far_stamp, rows = build(read_rares(a.rares), stamp, previous, Ardent(), now)
    with open(a.out, "w", encoding="utf-8", newline="\n") as f:
        f.write(render(now, far_stamp, rows))
    print(f"rows {len(rows)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
