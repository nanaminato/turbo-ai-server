-- Apply before enabling the source IP concurrency limit on an existing SQLite database.
CREATE TABLE IF NOT EXISTS FeatureAccessLeaseSources (
    LeaseId TEXT NOT NULL PRIMARY KEY REFERENCES FeatureAccessLeases (LeaseId) ON DELETE CASCADE,
    SourceKey BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_FeatureLeaseSource ON FeatureAccessLeaseSources (SourceKey);
