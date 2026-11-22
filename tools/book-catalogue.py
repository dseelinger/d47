#!/usr/bin/env python3
"""Build the Project Gutenberg catalogue that d47 browses.

Not part of the build. Run by .github/workflows/book-catalogue.yml; writes only the --out file.

    python tools/book-catalogue.py --out <gutenberg.tsv.gz> [--feeds <url or folder>]
    python tools/book-catalogue.py --check <fixture folder>

Reads pg_catalog.csv and rdf-files.tar.bz2 from the feeds folder (default
https://gutenberg.pglaf.org/cache/epub/feeds/) and writes a gzipped UTF-8 TSV with a header row and one
row per `Type` = `Text` book, most downloaded first (ties by id). Columns: id, title (first line of
Title), author (first author as "Given Family", dates and parentheses dropped), language (first code),
subjects (d47's names joined by "; "), downloads (pgterms:downloads of the book's RDF, 0 when absent) and
issued (yyyy-MM-dd). Tabs and line breaks inside a field become a space.

The RDF bundle is streamed, not extracted. --check runs the fixture folder's pg_catalog.csv and
rdf-files.tar.bz2 and compares the rows with its expected.tsv.
"""
import argparse
import csv
import gzip
import io
import os
import re
import sys
import tarfile
import urllib.error
import urllib.request

FEEDS_URL = "https://gutenberg.pglaf.org/cache/epub/feeds/"
USER_AGENT = "d47-book-catalogue (+https://github.com/dseelinger/d47)"
HEADER = "id\ttitle\tauthor\tlanguage\tsubjects\tdownloads\tissued"

# (d47 subject, Bookshelves terms matched whole, Subjects terms matched as a substring), all lower case.
SUBJECTS = (
    ("Science fiction", ("category: science-fiction & fantasy",), ("science fiction",)),
    ("Adventure", ("category: adventure",), ()),
    ("Mystery", ("category: crime, thrillers and mystery",), ("detective and mystery",)),
    ("Horror", (), ("horror", "ghost stories")),
    ("Gothic fiction", (), ("gothic",)),
    ("Historical fiction", ("category: historical novels",), ("historical fiction",)),
    ("Humour", ("category: humour",), ("humor",)),
    ("Poetry", ("category: poetry",), ("poetry",)),
)

DOWNLOADS = re.compile(rb"<pgterms:downloads[^>]*>\s*(\d+)\s*<")
RDF_NAME = re.compile(r"(?:^|/)pg(\d+)\.rdf$")


def clean(text):
    return " ".join(text.split())


def terms(text):
    return [t.strip().lower() for t in text.split(";") if t.strip()]


def first_line(text):
    lines = text.replace("\r", "\n").split("\n")
    return clean(lines[0]) if lines else ""


def display_author(authors):
    """First author in reading order, without dates, parentheses or a bracketed role."""
    first = authors.split(";")[0] if authors else ""
    first = re.sub(r"\([^)]*\)|\[[^\]]*\]", " ", first)
    parts = [p.strip() for p in first.split(",") if p.strip()]
    while len(parts) > 1 and re.search(r"\d|\bcent\b", parts[-1]):
        parts.pop()
    if len(parts) > 1:
        return clean(" ".join(parts[1:]) + " " + parts[0])
    return clean(parts[0]) if parts else ""


def subjects_of(subjects, bookshelves):
    shelves = terms(bookshelves)
    subs = terms(subjects)
    names = []
    for name, shelf_terms, subject_terms in SUBJECTS:
        if any(s in shelf_terms for s in shelves) or any(t in s for t in subject_terms for s in subs):
            names.append(name)
    return "; ".join(names)


def field(text):
    return clean(text)


def read_downloads(stream):
    """Maps book id to its downloads from a .tar.bz2 stream of RDF files."""
    downloads = {}
    with tarfile.open(fileobj=stream, mode="r|bz2") as tar:
        for member in tar:
            m = RDF_NAME.search(member.name) if member.isfile() else None
            if not m:
                continue
            found = DOWNLOADS.search(tar.extractfile(member).read())
            if found:
                downloads[m.group(1)] = int(found.group(1))
    return downloads


def read_rows(csv_text, downloads):
    rows = []
    for r in csv.DictReader(io.StringIO(csv_text, newline="")):
        if (r.get("Type") or "").strip() != "Text":
            continue
        book_id = (r.get("Text#") or "").strip()
        if not book_id.isdigit():
            continue
        language = (r.get("Language") or "").split(";")[0]
        count = downloads.get(book_id, 0)
        rows.append((count, int(book_id), [
            book_id,
            field(first_line(r.get("Title") or "")),
            field(display_author(r.get("Authors") or "")),
            field(language),
            field(subjects_of(r.get("Subjects") or "", r.get("Bookshelves") or "")),
            str(count),
            field(r.get("Issued") or ""),
        ]))
    rows.sort(key=lambda x: (-x[0], x[1]))
    return [HEADER] + ["\t".join(cells) for _, _, cells in rows]


def open_feed(feeds, name):
    if feeds.startswith("http"):
        request = urllib.request.Request(feeds.rstrip("/") + "/" + name, headers={"User-Agent": USER_AGENT})
        try:
            return urllib.request.urlopen(request, timeout=300)
        except (urllib.error.URLError, OSError) as e:
            raise SystemExit(f"could not fetch {name} from {feeds}: {e}")
    return open(os.path.join(feeds, name), "rb")


def build(feeds):
    with open_feed(feeds, "pg_catalog.csv") as f:
        csv_text = f.read().decode("utf-8-sig")
    with open_feed(feeds, "rdf-files.tar.bz2") as f:
        downloads = read_downloads(f)
    return read_rows(csv_text, downloads)


def write_gzip(path, lines):
    data = ("\n".join(lines) + "\n").encode("utf-8")
    with open(path, "wb") as raw, gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0, compresslevel=9) as gz:
        gz.write(data)


def check(folder):
    actual = build(folder)
    with open(os.path.join(folder, "expected.tsv"), encoding="utf-8", newline="") as f:
        expected = f.read().splitlines()
    if actual != expected:
        print("book-catalogue check failed", file=sys.stderr)
        for line in actual:
            print("  got      " + line, file=sys.stderr)
        for line in expected:
            print("  expected " + line, file=sys.stderr)
        return 1
    print(f"book-catalogue check passed: {len(actual) - 1} rows")
    return 0


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out")
    p.add_argument("--feeds", default=FEEDS_URL)
    p.add_argument("--check")
    a = p.parse_args()
    if a.check:
        return check(a.check)
    if not a.out:
        p.error("--out is required")
    lines = build(a.feeds)
    if len(lines) < 2:
        raise SystemExit("the catalogue has no Text rows")
    write_gzip(a.out, lines)
    print(f"rows {len(lines) - 1}, size {os.path.getsize(a.out)} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
