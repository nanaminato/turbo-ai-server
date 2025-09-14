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
    Password  varchar(20) not null,
    Email     varchar(50) not null
);

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
    Enable      tinyint(1)           not null,
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



# models
create table NovitaModels
(
    ModelId int auto_increment
        primary key,
    Model   longtext   null,
    Cover   longtext   null,
    Type    longtext   null,
    Nsfw    tinyint(1) not null,
    Sdxl    tinyint(1) not null
);

# tasks
create table GenerateTasks
(
    TaskId    varchar(255) not null
        primary key,
    TaskType  longtext     null,
    AccountId int          not null,
    DateTime  datetime(6)  null,
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
