# Accounts
create table __efmigrationshistory
(
    MigrationId    varchar(150) not null
        primary key,
    ProductVersion varchar(32)  not null
);

create table Accounts
(
    AccountId int auto_increment
        primary key,
    Username  varchar(20) not null,
    Password  varchar(512) not null,
    SecurityStamp char(32) not null default '',
    Email     varchar(50) not null
);

create table RefreshTokens
(
    RefreshTokenId    char(36) not null primary key,
    AccountId         int not null,
    TokenHash         char(64) not null,
    SessionId         char(36) not null,
    CreatedAt         datetime(6) not null,
    ExpiresAt         datetime(6) not null,
    RevokedAt         datetime(6) null,
    LastUsedAt        datetime(6) null,
    ReplacedByTokenId char(36) null,
    DeviceName        varchar(256) null,
    CreatedByIp       varchar(64) null,
    constraint FK_RefreshTokens_Accounts_AccountId
        foreign key (AccountId) references Accounts (AccountId)
            on delete cascade,
    constraint UX_RefreshTokens_TokenHash unique (TokenHash)
);

create index IX_RefreshTokens_AccountId_SessionId on RefreshTokens (AccountId, SessionId);
create index IX_RefreshTokens_ExpiresAt on RefreshTokens (ExpiresAt);

create table Roles
(
    RoleId int auto_increment
        primary key,
    Name   longtext not null
);

create table AccountRoles
(
    AccountRoleId int auto_increment
        primary key,
    AccountId     int not null,
    RoleId        int not null,
    constraint FK_AccountRoles_Accounts_AccountId
        foreign key (AccountId) references Accounts (AccountId)
            on delete cascade,
    constraint FK_AccountRoles_Roles_RoleId
        foreign key (RoleId) references Roles (RoleId)
            on delete cascade
);

create index IX_AccountRoles_AccountId
    on AccountRoles (AccountId);

create index IX_AccountRoles_RoleId
    on AccountRoles (RoleId);


# key
create table Models
(
    ModelId     int auto_increment
        primary key,
    Name        varchar(200)         not null,
    IsChatModel tinyint(1)           not null comment '是否是对话模型',
    ModelValue  varchar(200)         not null,
    Vision      tinyint(1) default 0 not null
);

create table SupplierKeys
(
    SupplierKeyId     int auto_increment
        primary key,
    BaseUrl           varchar(200) not null,
    RequestIdentifier int          not null,
    Enable            tinyint(1)   not null,
    ApiKey            varchar(200) not null
);


create table ModelFees
(
    ModelFeeId    int auto_increment
        primary key,
    SupplierKeyId int    not null,
    ModelId       int    not null,
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



# sync

create table ChatHistories
(
    ChatHistoryId bigint auto_increment
        primary key,
    UserId        bigint   not null,
    DataId        bigint   not null,
    Title         longtext null
);

create table ChatMessages
(
    ChatMessageId bigint auto_increment
        primary key,
    DataId        bigint     not null,
    ChatHistoryId bigint     not null,
    Role          longtext   null,
    Content       longtext   null,
    ShowType      int        not null,
    Finish        tinyint(1) not null,
    Model         longtext   null,
    constraint FK_ChatMessages_ChatHistories_ChatHistoryId
        foreign key (ChatHistoryId) references ChatHistories (ChatHistoryId)
            on delete cascade
);

create index IX_ChatMessages_ChatHistoryId
    on ChatMessages (ChatHistoryId);

create table FileAdds
(
    FileAddsId    bigint auto_increment
        primary key,
    FileName      longtext null,
    FileType      longtext null,
    FileSize      bigint   not null,
    FileContent   longtext null,
    ParsedContent longtext null,
    ChatMessageId bigint   not null,
    constraint FK_FileAdds_ChatMessages_ChatMessageId
        foreign key (ChatMessageId) references ChatMessages (ChatMessageId)
            on delete cascade
);

create index IX_FileAdds_ChatMessageId
    on FileAdds (ChatMessageId);

# tasks
create table GenerateTasks
(
    TaskId    varchar(255) not null
        primary key,
    TaskType  longtext     null,
    AccountId int          not null,
    DateTime  datetime(6)  null,
    Images    json         null,
    Videos    json         null,
    constraint FK_GenerateTasks_Accounts_AccountId
        foreign key (AccountId) references Accounts (AccountId)
            on delete cascade
);

create index IX_GenerateTasks_AccountId
    on GenerateTasks (AccountId);



create table ModelKeyBinds
(
    ModelKeyBindId int auto_increment
        primary key,
    Enable         tinyint(1) not null,
    SupplierKeyId  int        not null,
    ModelId        int        not null,
    Fee            double     not null,
    RoutingWeight  double     null,
    ProviderModelValue varchar(200) not null,
    Priority       int        not null default 0,
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
    ModelId     int auto_increment
        primary key,
    Enable      tinyint(1)           not null,
    Name        varchar(200)         not null,
    IsChatModel tinyint(1)           not null comment '是否是对话模型',
    ModelValue  varchar(200)         not null,
    Vision      tinyint(1) default 0 not null
);

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
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX IX_FeatureTaskAccount (AccountId)
) ENGINE=InnoDB;

CREATE UNIQUE INDEX UX_ChatHistories_UserId_DataId ON ChatHistories (UserId, DataId);
CREATE UNIQUE INDEX UX_ChatMessages_ChatHistoryId_DataId ON ChatMessages (ChatHistoryId, DataId);
CREATE TABLE SyncAccounts (
    AccountId int NOT NULL PRIMARY KEY,
    Version bigint NOT NULL DEFAULT 0,
    CONSTRAINT FK_SyncAccounts_Accounts FOREIGN KEY (AccountId) REFERENCES Accounts (AccountId) ON DELETE CASCADE
);
CREATE TABLE SyncChanges (
    AccountId int NOT NULL,
    Version bigint NOT NULL,
    OperationId varchar(32) NOT NULL,
    RequestHash varchar(64) NOT NULL,
    Kind varchar(16) NOT NULL,
    Action varchar(16) NOT NULL,
    EntityId varchar(255) NOT NULL,
    HistoryId bigint NULL,
    Payload longtext NULL,
    PRIMARY KEY (AccountId, Version),
    UNIQUE KEY UX_SyncChanges_AccountId_OperationId (AccountId, OperationId),
    CONSTRAINT FK_SyncChanges_SyncAccounts FOREIGN KEY (AccountId) REFERENCES SyncAccounts (AccountId) ON DELETE CASCADE
);

-- Registration email verification; additive migration for existing databases.
CREATE TABLE RegistrationEmailChallenges (
    EmailHash varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    CodeHash varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ExpiresAt datetime(6) NOT NULL,
    NextSendAt datetime(6) NOT NULL,
    AttemptsLeft int NOT NULL
);

-- Every replica polls this shared revision; all catalog writers, including SQL maintenance, invalidate it.
CREATE TABLE RoutingConfiguration (Id int NOT NULL PRIMARY KEY, Version bigint NOT NULL);
INSERT INTO RoutingConfiguration (Id, Version) VALUES (1, 1);
CREATE TRIGGER routing_Models_insert AFTER INSERT ON Models FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_Models_update AFTER UPDATE ON Models FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_Models_delete AFTER DELETE ON Models FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_SupplierKeys_insert AFTER INSERT ON SupplierKeys FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_SupplierKeys_update AFTER UPDATE ON SupplierKeys FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_SupplierKeys_delete AFTER DELETE ON SupplierKeys FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_ModelKeyBinds_insert AFTER INSERT ON ModelKeyBinds FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_ModelKeyBinds_update AFTER UPDATE ON ModelKeyBinds FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_ModelKeyBinds_delete AFTER DELETE ON ModelKeyBinds FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_AvailableModels_insert AFTER INSERT ON AvailableModels FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_AvailableModels_update AFTER UPDATE ON AvailableModels FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
CREATE TRIGGER routing_AvailableModels_delete AFTER DELETE ON AvailableModels FOR EACH ROW UPDATE RoutingConfiguration SET Version=Version+1 WHERE Id=1;
