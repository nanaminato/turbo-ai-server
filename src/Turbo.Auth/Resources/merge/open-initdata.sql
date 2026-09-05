# data
insert into Accounts(AccountId, username, password, email)
values (1, 'niko', 'nikoniko', '2@qq.com');
insert into Roles(roleid, name)
values (1, 'admin'),
       (2, 'user'),
       (3, 'vip');
insert into AccountRoles (AccountId, RoleId)
values (1, 1),
       (1, 2),
       (1, 3);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-3.5-turbo', 1, 'gpt-3.5-turbo', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o1-mini', 1, 'o1-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o1-preview', 1, 'o1-preview', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4', 1, 'gpt-4', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4o', 1, 'gpt-4o', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'chatgpt-4o-latest', 1, 'chatgpt-4o-latest', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4o-mini', 1, 'gpt-4o-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4-turbo-preview', 1, 'gpt-4-turbo-preview', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4-turbo', 1, 'gpt-4-turbo', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'o3', 1, 'o3', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'o4-mini', 1, 'o4-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-4.1', 1, 'gpt-4.1', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-4.1-nano', 1, 'gpt-4.1-nano', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-4.1-mini', 1, 'gpt-4.1-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'o1-preview', 1, 'o1-preview', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-3.5-turbo-16k', 1, 'gpt-3.5-turbo-16k', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-4.5-preview', 1, 'gpt-4.5-preview', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-4o-search-preview', 1, 'gpt-4o-search-preview', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'gpt-4o-mini-search-preview', 1, 'gpt-4o-mini-search-preview', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES ( 1, 'o1-preview', 1, 'o1-preview', 0);

