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