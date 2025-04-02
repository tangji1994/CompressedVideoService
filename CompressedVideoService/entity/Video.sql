CREATE TABLE VideoFiles (
    Id             INTEGER  PRIMARY KEY AUTOINCREMENT,
    FilePath       TEXT     NOT NULL
                            UNIQUE,
    Hash           TEXT     NOT NULL,
    Status         INTEGER  NOT NULL,  -- 0:New, 1:Compressed, 2:Archived, 3:Error
    CompressedDate DATETIME,
    ArchivedDate   DATETIME,
    OutputPath     TEXT,
    ErrorMessage   TEXT,
    LastModified   DATETIME NOT NULL,
    Version        INTEGER  NOT NULL
                            DEFAULT 1
);
