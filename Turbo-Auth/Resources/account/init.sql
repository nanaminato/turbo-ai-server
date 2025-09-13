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

