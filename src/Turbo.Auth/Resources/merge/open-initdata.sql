# data
-- 此脚本不创建带默认密码的账户。请填写并导入 Resources/account/create-admin.sql，通过邮件设置首个管理员密码。
insert into Roles(roleid, name)
values (1, 'admin'),
       (2, 'user'),
       (3, 'vip');
-- Account-role bindings are created by --init-db / --create-admin after the account exists.

-- Curated chat examples, verified 2026-11-05. See docs/model-catalog-and-routing.md.
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-6-luna', 1, 'gpt-6-luna', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'claude-sonnet-5-5', 1, 'claude-sonnet-5-5', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'claude-haiku-5-5', 1, 'claude-haiku-5-5', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'claude-opus-5-5', 1, 'claude-opus-5-5', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-3.8-flash', 1, 'gemini-3.8-flash', 1);
