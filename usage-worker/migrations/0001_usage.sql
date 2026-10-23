CREATE TABLE counts (
  day    TEXT NOT NULL,
  tool   TEXT NOT NULL,
  caller TEXT NOT NULL,
  uses   INTEGER NOT NULL,
  PRIMARY KEY (day, tool, caller)
);

CREATE TABLE reports (
  day     TEXT PRIMARY KEY,
  reports INTEGER NOT NULL
);
