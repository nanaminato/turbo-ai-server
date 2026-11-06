
-- 此脚本不创建带默认密码的账户。SQLite 管理员由显式 --init-db / --create-admin 命令安全创建。
insert into Roles(roleid, code, name)
values (1, 'admin', 'admin'),
       (2, 'user', 'user'),
       (3, 'vip', 'vip');
-- Account-role bindings are created by --init-db / --create-admin after the account exists.

-- Curated chat examples, verified 2026-11-05. See docs/model-catalog-and-routing.md.
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-6.1-sol', 1, 'gpt-6.1-sol', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-6.1-terra', 1, 'gpt-6.1-terra', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-6-astra', 1, 'gpt-6-astra', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-6-sol', 1, 'gpt-6-sol', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-6-luna', 1, 'gpt-6-luna', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'claude-sonnet-5-5', 1, 'claude-sonnet-5-5', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'claude-haiku-5-5', 1, 'claude-haiku-5-5', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'claude-opus-5-5', 1, 'claude-opus-5-5', 1);
INSERT INTO AvailableModels (Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-3.8-flash', 1, 'gemini-3.8-flash', 1);
