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
