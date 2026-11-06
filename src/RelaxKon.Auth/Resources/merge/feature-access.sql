-- Apply once to existing deployments, before starting the updated server. No grants are seeded.
CREATE TABLE IF NOT EXISTS FeatureAccessPolicies (
    Id INT NOT NULL PRIMARY KEY,
    Revision BIGINT NOT NULL,
    Document JSON NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS FeatureAccessUsage (
    AccountId INT NOT NULL,
    FeatureKey VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    MinuteWindow BIGINT NOT NULL DEFAULT 0,
    DayWindow BIGINT NOT NULL DEFAULT 0,
    MinuteCount BIGINT NOT NULL DEFAULT 0,
    DayCount BIGINT NOT NULL DEFAULT 0,
    PRIMARY KEY (AccountId, FeatureKey)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS FeatureAccessLeases (
    LeaseId CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    AccountId INT NOT NULL,
    FeatureKey VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    INDEX IX_FeatureLease (AccountId, FeatureKey, ExpiresAt)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS FeatureAccessTasks (
    TaskId VARCHAR(255) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    AccountId INT NOT NULL,
    SupplierKeyId INT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX IX_FeatureTaskAccount (AccountId)
) ENGINE=InnoDB;
