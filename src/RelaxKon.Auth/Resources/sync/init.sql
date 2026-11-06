
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
