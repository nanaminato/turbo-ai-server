-- Run once on an existing database, after taking a backup.
ALTER TABLE Accounts ADD COLUMN SecurityStamp char(32) NULL AFTER Password;
UPDATE Accounts SET SecurityStamp = REPLACE(UUID(), '-', '')
WHERE SecurityStamp IS NULL OR SecurityStamp = '';
ALTER TABLE Accounts MODIFY COLUMN SecurityStamp char(32) NOT NULL;

CREATE TABLE RefreshTokens
(
    RefreshTokenId    char(36) NOT NULL PRIMARY KEY,
    AccountId         int NOT NULL,
    TokenHash         char(64) NOT NULL,
    SessionId         char(36) NOT NULL,
    CreatedAt         datetime(6) NOT NULL,
    ExpiresAt         datetime(6) NOT NULL,
    RevokedAt         datetime(6) NULL,
    LastUsedAt        datetime(6) NULL,
    ReplacedByTokenId char(36) NULL,
    DeviceName        varchar(256) NULL,
    CreatedByIp       varchar(64) NULL,
    CONSTRAINT FK_RefreshTokens_Accounts_AccountId
        FOREIGN KEY (AccountId) REFERENCES Accounts (AccountId)
            ON DELETE CASCADE,
    CONSTRAINT UX_RefreshTokens_TokenHash UNIQUE (TokenHash)
);

CREATE INDEX IX_RefreshTokens_AccountId_SessionId ON RefreshTokens (AccountId, SessionId);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON RefreshTokens (ExpiresAt);
