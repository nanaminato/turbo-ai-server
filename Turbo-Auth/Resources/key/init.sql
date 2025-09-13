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

