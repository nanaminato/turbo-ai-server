insert into Accounts(AccountId, username, password, email)
values (1, 'niko', 'suzumiya', '2@qq.com');
insert into Roles(roleid, name)
values (1, 'admin'),
       (2, 'user'),
       (3, 'vip');
insert into AccountRoles (AccountId, RoleId)
values (1, 1),
       (1, 2),
       (1, 3);
