// The whole of d47's backend (https://github.com/dseelinger/d47/issues/175).
//
// A Worker in front of an R2 bucket, and nothing else: no VM, no framework, no database. The
// Worker is the ONLY writer. There is no public bucket, no S3 credential in the shipped binary,
// and no path from a donor to storage that does not come through this file — which is what makes
// this file's own ceiling the bucket's ceiling.
//
// **It is JavaScript, and that is a second language in the tree.** That is the honest cost of the
// proposal, argued in #175 rather than smuggled in. It is about a hundred lines and it lives
// outside src/, where nothing builds it and nothing tests it with dotnet.
//
// **Nothing here bills.** Three hard stops, each of which fails closed:
//
//   1. Workers free returns Error 1027 at 100,000 requests a day and there is no overage on the
//      plan — exceeding it needs an explicit upgrade, not a card.
//   2. Payload size, enforced below, before any write.
//   3. R2's free tier: 10 GB, 1M Class A ops a month, 10M Class B, zero egress, permanently.
//
// **Every byte that arrives here is untrusted**, including every header. A donation comes from a
// stranger's machine over the open internet, and the same rule the journal and in-game comms
// already live under applies with more force: nothing that arrives chooses a key, a prefix or a
// retention rule, and nothing is copied into object metadata without being bounded first.

/** The envelope version this endpoint understands. Anything else is refused rather than guessed at. */
const FORMAT = 1;

/**
 * Two retention classes, so two prefixes and two lifecycle rules (#175).
 *
 * A donated corpus is PERMANENT by design — that is the whole payoff, a regression case that
 * cannot rot. An excerpt is evidence for one defect and should go when the issue closes. Opposite
 * rules, which is exactly why they cannot share a prefix: a lifecycle rule is written against a
 * prefix, and one prefix cannot hold both "expire in 30 days" and "keep for ever".
 *
 * The retention itself is a BUCKET rule, not code here. See README.md — a number a person
 * remembers is not retention, which is #168's stated acceptance criterion.
 */
const KINDS = {
  excerpt: {
    prefix: 'excerpts/',
    suffix: '.md.gz',
    // A kilobyte payload with a wide margin. The window it comes from already warns past 60,000
    // characters, on the grounds that nobody reads more than that — and the yes it asks for is a
    // yes to something read.
    mostBytes: 4 * 1024 * 1024,
    contentType: 'text/markdown',
  },
  corpus: {
    prefix: 'corpus/',
    suffix: '.jsonl.gz',
    // A full 13-month history is about 383 MB of JSON lines and about 32.5 MB gzipped. This is
    // roughly three times that, which leaves room for a longer history than anybody currently has
    // while keeping one donation well under a hundredth of the 10 GB tier.
    mostBytes: 96 * 1024 * 1024,
    contentType: 'application/x-ndjson',
  },
};

/** Thirty-two lowercase hex characters, which is what d47 writes and the only thing accepted. */
const TOKEN = /^[0-9a-f]{32}$/;

/** SHA-256 as lowercase hex. Of the SCRUBBED PAYLOAD, never of the compressed bytes — see below. */
const SHA256 = /^[0-9a-f]{64}$/;

/**
 * How much of a header is copied into object metadata. R2 caps the whole custom-metadata block, so
 * one long value fails the write rather than being truncated — and the only header whose length
 * d47 does not choose is the build stamp, which a local build can make arbitrarily long.
 */
const MOST_METADATA = 200;

export default {
  async fetch(request, env) {
    if (request.method !== 'POST') {
      // Deliberately not a landing page. There is nothing here to browse, and a GET that answered
      // with anything friendly would be an invitation to find out what else it answers.
      return said(405, 'This endpoint takes donations by POST and does nothing else.');
    }

    if (new URL(request.url).pathname !== '/donate') {
      return said(404, 'Not found.');
    }

    const format = request.headers.get('d47-format');
    if (format !== String(FORMAT)) {
      // **Refuse rather than guess.** This is the whole reason the envelope carries a version: a
      // newer client's payload stored under this build's assumptions is a corpus nobody can trust
      // and nobody can tell is wrong.
      return said(400, `This endpoint speaks donation format ${FORMAT}, and this is ${format ?? 'unversioned'}.`);
    }

    const kind = KINDS[request.headers.get('d47-kind')];
    if (!kind) {
      return said(400, 'Unknown donation kind. There are two, and each has its own retention rule.');
    }

    const donor = request.headers.get('d47-donor');
    if (!TOKEN.test(donor ?? '')) {
      // The donor token is part of the object key, so this check is a path check as much as a
      // format one: a hex-only alphabet of fixed length cannot contain a slash, a dot or a
      // traversal, whatever anybody sends.
      return said(400, 'A donation needs a well-formed installation identifier.');
    }

    const sha256 = request.headers.get('d47-sha256');
    if (!SHA256.test(sha256 ?? '')) {
      return said(400, 'A donation needs the SHA-256 of its scrubbed payload.');
    }

    // **Length before body, always.** Content-Length is what lets an oversized donation be refused
    // BEFORE any write and before any of it is read — which is the second of the three hard stops,
    // and the one that has to hold for the other two to matter.
    const length = Number(request.headers.get('content-length'));
    if (!Number.isInteger(length) || length <= 0) {
      return said(411, 'A donation must declare its length.');
    }

    if (length > kind.mostBytes) {
      return said(413, `That is larger than this endpoint accepts for a ${request.headers.get('d47-kind')}.`);
    }

    // **The key is derived here and never accepted from the request.** A client-supplied object
    // name is a stranger choosing a path inside somebody else's bucket, and no amount of escaping
    // makes that a good idea. Every component below is either a constant or something already
    // checked against a fixed alphabet.
    const key = `${kind.prefix}${donor}/${stamp()}-${sha256.slice(0, 16)}${kind.suffix}`;

    try {
      await env.DONATIONS.put(key, request.body, {
        httpMetadata: {
          contentType: kind.contentType,
          // Stored compressed, and said so on the object: a reader who fetches one should not have
          // to work out why it will not parse. What is decompressed is decompressed once, by
          // whoever replays it — a Worker that decompressed would have to defend itself against a
          // decompression bomb for no gain, since the store is billed by what it holds.
          contentEncoding: 'gzip',
        },
        customMetadata: {
          // The consent hash, kept beside the bytes. It is over the payload rather than over what
          // arrived on the wire, ON PURPOSE: gzip output is not reproducible from the payload —
          // levels and implementations differ — so a hash over it would prove the transfer and
          // nothing a donor cares about. This one is checkable by anyone holding the object with an
          // ordinary sha256sum after a gunzip, and against the receipt d47 kept on the donor's own
          // machine.
          sha256,
          format: String(FORMAT),
          build: bounded(request.headers.get('d47-build')),
          takenAt: bounded(request.headers.get('d47-taken-at')),
          bytes: bounded(request.headers.get('d47-bytes')),
        },
      });
    } catch (error) {
      // Nothing about the store goes back to the donor. What went wrong here is between this
      // Worker and its bucket, and a stranger learning which is not part of donating.
      console.error('Donation write failed', error);
      return said(503, 'The store did not accept it. Nothing was written; try again later.');
    }

    // The key, so the donor's receipt can name one object and no other — which is what makes a
    // deletion request a single delete rather than a search.
    return new Response(JSON.stringify({ ok: true, key, sha256 }), {
      status: 201,
      headers: { 'content-type': 'application/json' },
    });
  },
};

/** UTC, sorting in time order, and nothing an object name has to escape. */
function stamp() {
  return new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
}

/** A header value, made safe to store: printable ASCII only, and short. */
function bounded(value) {
  return (value ?? '').replace(/[^\x20-\x7e]/g, '').slice(0, MOST_METADATA);
}

/** Plain text, because the one reader of a refusal is a person looking at a d47 log. */
function said(status, text) {
  return new Response(text + '\n', { status, headers: { 'content-type': 'text/plain' } });
}
