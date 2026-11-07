-- Apply before enabling the aggregate concurrency limits on an existing MySQL database.
CREATE TABLE IF NOT EXISTS FeatureAccessConcurrencyLocks (
    Bucket SMALLINT UNSIGNED NOT NULL PRIMARY KEY
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS FeatureAccessLeaseSources (
    LeaseId CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    SourceKey BINARY(32) NOT NULL,
    INDEX IX_FeatureLeaseSource (SourceKey),
    CONSTRAINT FK_FeatureLeaseSource_Lease FOREIGN KEY (LeaseId)
        REFERENCES FeatureAccessLeases (LeaseId) ON DELETE CASCADE
) ENGINE=InnoDB;
