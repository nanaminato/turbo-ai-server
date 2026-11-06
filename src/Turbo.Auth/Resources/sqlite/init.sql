-- SQLite schema for explicit --init-db only. Never apply over an existing database.

create table __efmigrationshistory
(
    MigrationId    TEXT not null
        primary key,
    ProductVersion TEXT  not null
);

create table Accounts
(
    AccountId INTEGER PRIMARY KEY AUTOINCREMENT,
    Username  TEXT COLLATE NOCASE not null,
    Password  TEXT not null,
    SecurityStamp TEXT not null default '',
    Email     TEXT COLLATE NOCASE not null
);

create table RefreshTokens
(
    RefreshTokenId    TEXT not null primary key,
    AccountId         INTEGER not null,
    TokenHash         TEXT not null,
    SessionId         TEXT not null,
    CreatedAt         TEXT not null,
    ExpiresAt         TEXT not null,
    RevokedAt         TEXT null,
    LastUsedAt        TEXT null,
    ReplacedByTokenId TEXT null,
    DeviceName        TEXT null,
    CreatedByIp       TEXT null,
    constraint FK_RefreshTokens_Accounts_AccountId
        foreign key (AccountId) references Accounts (AccountId)
            on delete cascade,
    constraint UX_RefreshTokens_TokenHash unique (TokenHash)
);

create index IX_RefreshTokens_AccountId_SessionId on RefreshTokens (AccountId, SessionId);
create index IX_RefreshTokens_ExpiresAt on RefreshTokens (ExpiresAt);

create table Roles
(
    RoleId INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT not null,
    Name TEXT COLLATE NOCASE not null,
    Description TEXT null,
    constraint UX_Roles_Code unique (Code)
);

create table AccountRoles
(
    AccountRoleId INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountId     INTEGER not null,
    RoleId        INTEGER not null,
    constraint FK_AccountRoles_Accounts_AccountId
        foreign key (AccountId) references Accounts (AccountId)
            on delete cascade,
    constraint FK_AccountRoles_Roles_RoleId
        foreign key (RoleId) references Roles (RoleId)
            on delete cascade
);

create unique index UX_AccountRoles_AccountId_RoleId on AccountRoles (AccountId, RoleId);

create index IX_AccountRoles_AccountId
    on AccountRoles (AccountId);

create index IX_AccountRoles_RoleId
    on AccountRoles (RoleId);



create table Models
(
    ModelId     INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT         not null,
    IsChatModel INTEGER           not null,
    ModelValue  TEXT         not null,
    Vision      INTEGER default 0 not null
);

create table SupplierKeys
(
    SupplierKeyId     INTEGER PRIMARY KEY AUTOINCREMENT,
    BaseUrl           TEXT not null,
    RequestIdentifier INTEGER          not null,
    Enable            INTEGER   not null,
    ApiKey            TEXT not null
);


create table ModelFees
(
    ModelFeeId    INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierKeyId INTEGER    not null,
    ModelId       INTEGER    not null,
    Fee           double not null,
    constraint FK_ModelFees_Models_ModelId
        foreign key (ModelId) references Models (ModelId)
            on delete cascade,
    constraint FK_ModelFees_SupplierKeys_SupplierKeyId
        foreign key (SupplierKeyId) references SupplierKeys (SupplierKeyId)
            on delete cascade
);

create index IX_ModelFees_ModelId
    on ModelFees (ModelId);

create index IX_ModelFees_SupplierKeyId
    on ModelFees (SupplierKeyId);





create table ChatHistories
(
    ChatHistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId        INTEGER   not null,
    DataId        INTEGER   not null,
    Title         TEXT null
);

create table ChatMessages
(
    ChatMessageId INTEGER PRIMARY KEY AUTOINCREMENT,
    DataId        INTEGER     not null,
    ChatHistoryId INTEGER     not null,
    Role          TEXT   null,
    Content       TEXT   null,
    ShowType      INTEGER        not null,
    Finish        INTEGER not null,
    Model         TEXT   null,
    constraint FK_ChatMessages_ChatHistories_ChatHistoryId
        foreign key (ChatHistoryId) references ChatHistories (ChatHistoryId)
            on delete cascade
);

create index IX_ChatMessages_ChatHistoryId
    on ChatMessages (ChatHistoryId);

create table FileAdds
(
    FileAddsId    INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName      TEXT null,
    FileType      TEXT null,
    FileSize      INTEGER   not null,
    FileContent   TEXT null,
    ParsedContent TEXT null,
    ChatMessageId INTEGER   not null,
    constraint FK_FileAdds_ChatMessages_ChatMessageId
        foreign key (ChatMessageId) references ChatMessages (ChatMessageId)
            on delete cascade
);

create index IX_FileAdds_ChatMessageId
    on FileAdds (ChatMessageId);


create table GenerateTasks
(
    TaskId    TEXT not null
        primary key,
    TaskType  TEXT     null,
    AccountId INTEGER          not null,
    DateTime  TEXT  null,
    Images    TEXT         null CHECK (Images IS NULL OR json_valid(Images)),
    Videos    TEXT         null CHECK (Videos IS NULL OR json_valid(Videos)),
    constraint FK_GenerateTasks_Accounts_AccountId
        foreign key (AccountId) references Accounts (AccountId)
            on delete cascade
);

create index IX_GenerateTasks_AccountId
    on GenerateTasks (AccountId);



create table ModelKeyBinds
(
    ModelKeyBindId INTEGER PRIMARY KEY AUTOINCREMENT,
    Enable         INTEGER not null,
    SupplierKeyId  INTEGER        not null,
    ModelId        INTEGER        not null,
    Fee            double     not null,
    RoutingWeight  double     null,
    ProviderModelValue TEXT not null,
    Priority       INTEGER        not null default 0,
    constraint FK_ModelKeyBinds_Models_ModelId
        foreign key (ModelId) references Models (ModelId)
            on delete cascade,
    constraint FK_ModelKeyBinds_SupplierKeys_SupplierKeyId
        foreign key (SupplierKeyId) references SupplierKeys (SupplierKeyId)
            on delete cascade
);

create index IX_ModelKeyBinds_ModelId
    on ModelKeyBinds (ModelId);

create index IX_ModelKeyBinds_SupplierKeyId
    on ModelKeyBinds (SupplierKeyId);

create index IX_ModelKeyBinds_Routing
    on ModelKeyBinds (ModelId, Enable, Priority);


create table AvailableModels
(
    ModelId     INTEGER PRIMARY KEY AUTOINCREMENT,
    Enable      INTEGER           not null,
    Name        TEXT         not null,
    IsChatModel INTEGER           not null,
    ModelValue  TEXT         not null,
    Vision      INTEGER default 0 not null
);

-- Fresh SQLite initialization only. No feature-access grants are seeded.
CREATE TABLE IF NOT EXISTS FeatureAccessPolicies (
    Id INTEGER NOT NULL PRIMARY KEY,
    Revision INTEGER NOT NULL,
    Document TEXT NOT NULL CHECK (json_valid(Document))
);
CREATE TABLE IF NOT EXISTS FeatureAccessUsage (
    AccountId INTEGER NOT NULL,
    FeatureKey TEXT NOT NULL,
    MinuteWindow INTEGER NOT NULL DEFAULT 0,
    DayWindow INTEGER NOT NULL DEFAULT 0,
    MinuteCount INTEGER NOT NULL DEFAULT 0,
    DayCount INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (AccountId, FeatureKey)
);
CREATE TABLE IF NOT EXISTS FeatureAccessLeases (
    LeaseId TEXT NOT NULL PRIMARY KEY,
    AccountId INTEGER NOT NULL,
    FeatureKey TEXT NOT NULL,
    ExpiresAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS FeatureAccessTasks (
    TaskId TEXT NOT NULL PRIMARY KEY,
    AccountId INTEGER NOT NULL,
    SupplierKeyId INTEGER NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX UX_ChatHistories_UserId_DataId ON ChatHistories (UserId, DataId);
CREATE UNIQUE INDEX UX_ChatMessages_ChatHistoryId_DataId ON ChatMessages (ChatHistoryId, DataId);
CREATE TABLE SyncAccounts (
    AccountId INTEGER NOT NULL PRIMARY KEY,
    Version INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT FK_SyncAccounts_Accounts FOREIGN KEY (AccountId) REFERENCES Accounts (AccountId) ON DELETE CASCADE
);
CREATE TABLE SyncChanges (
    AccountId INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    OperationId TEXT NOT NULL,
    RequestHash TEXT NOT NULL,
    Kind TEXT NOT NULL,
    Action TEXT NOT NULL,
    EntityId TEXT NOT NULL,
    HistoryId INTEGER NULL,
    Payload TEXT NULL,
    PRIMARY KEY (AccountId, Version),
    CONSTRAINT UX_SyncChanges_AccountId_OperationId UNIQUE (AccountId, OperationId),
    CONSTRAINT FK_SyncChanges_SyncAccounts FOREIGN KEY (AccountId) REFERENCES SyncAccounts (AccountId) ON DELETE CASCADE
);

-- Registration email verification; additive migration for existing databases.
CREATE TABLE RegistrationEmailChallenges (
    EmailHash TEXT NOT NULL PRIMARY KEY,
    CodeHash TEXT NOT NULL,
    ExpiresAt TEXT NOT NULL,
    NextSendAt TEXT NOT NULL,
    AttemptsLeft INTEGER NOT NULL
);

CREATE INDEX IX_Accounts_Username ON Accounts (`Username`);

CREATE INDEX IX_Accounts_Email ON Accounts (`Email`);

CREATE INDEX IX_GenerateTasks_AccountId_DateTime_TaskId ON GenerateTasks (`AccountId`, `DateTime`, `TaskId`);

CREATE INDEX IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt ON RefreshTokens (`AccountId`, `RevokedAt`, `ExpiresAt`);

-- Every replica polls this shared revision; all catalog writers, including SQL maintenance, invalidate it.
CREATE TABLE RoutingConfiguration (Id INTEGER NOT NULL PRIMARY KEY, Version INTEGER NOT NULL);
INSERT INTO RoutingConfiguration (Id, Version) VALUES (1, 1);
CREATE TRIGGER routing_Models_insert AFTER INSERT ON Models FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_Models_update AFTER UPDATE ON Models FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_Models_delete AFTER DELETE ON Models FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_SupplierKeys_insert AFTER INSERT ON SupplierKeys FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_SupplierKeys_update AFTER UPDATE ON SupplierKeys FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_SupplierKeys_delete AFTER DELETE ON SupplierKeys FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_ModelKeyBinds_insert AFTER INSERT ON ModelKeyBinds FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_ModelKeyBinds_update AFTER UPDATE ON ModelKeyBinds FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_ModelKeyBinds_delete AFTER DELETE ON ModelKeyBinds FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_AvailableModels_insert AFTER INSERT ON AvailableModels FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_AvailableModels_update AFTER UPDATE ON AvailableModels FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;
CREATE TRIGGER routing_AvailableModels_delete AFTER DELETE ON AvailableModels FOR EACH ROW BEGIN UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1; END;

CREATE INDEX IX_FeatureLease ON FeatureAccessLeases (AccountId, FeatureKey, ExpiresAt);
CREATE INDEX IX_FeatureTaskAccount ON FeatureAccessTasks (AccountId);
