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