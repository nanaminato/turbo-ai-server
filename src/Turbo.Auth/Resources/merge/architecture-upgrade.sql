-- Architecture upgrade: stop writers, back up the database and resolve duplicate IDs before applying.
-- These statements intentionally fail rather than delete or merge existing records.
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
