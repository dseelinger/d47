-- Times are ISO 8601 UTC strings from Date.prototype.toISOString, so they compare as text.

CREATE TABLE sessions (
  hash     TEXT PRIMARY KEY,
  customer TEXT NOT NULL,
  expires  TEXT NOT NULL
);

CREATE INDEX sessions_by_customer ON sessions (customer);

CREATE TABLE link_codes (
  hash      TEXT PRIMARY KEY,
  customer  TEXT NOT NULL,
  challenge TEXT NOT NULL,
  expires   TEXT NOT NULL
);

CREATE TABLE upload_tokens (
  hash     TEXT PRIMARY KEY,
  customer TEXT NOT NULL
);

CREATE INDEX upload_tokens_by_customer ON upload_tokens (customer);

CREATE TABLE snapshots (
  customer TEXT PRIMARY KEY,
  body     TEXT NOT NULL,
  at       TEXT NOT NULL
);

CREATE TABLE switch_offs (
  customer TEXT PRIMARY KEY,
  at       TEXT NOT NULL
);
