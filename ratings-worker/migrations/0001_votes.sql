CREATE TABLE votes (
  story TEXT NOT NULL,
  voter TEXT NOT NULL,
  stars INTEGER NOT NULL CHECK (stars BETWEEN 1 AND 5),
  at    TEXT NOT NULL,
  PRIMARY KEY (story, voter)
);
