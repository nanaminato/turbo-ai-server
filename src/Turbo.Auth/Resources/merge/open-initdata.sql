# data
-- 此脚本不创建带默认密码的账户。请使用 Resources/account/input.sql 创建首个管理员和其他 SQL 用户。
insert into Roles(roleid, name)
values (1, 'admin'),
       (2, 'user'),
       (3, 'vip');
insert into AccountRoles (AccountId, RoleId)
values (1, 1),
       (1, 2),
       (1, 3);

-- ====================================================================
-- OpenAI 模型（默认仅做参考；老旧/已停止支持的模型已剔除）
-- 注意：gpt-3.5-turbo、gpt-4、gpt-4-turbo、gpt-4.5-preview 等
-- 已被官方停止或标记为 legacy，本脚本不再默认导入。
-- 如确实需要历史兼容，可在管理员界面手动添加。
-- ====================================================================

-- ---- 经典 GPT-4o 系列（仍然支持；视觉/多模态） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4o', 1, 'gpt-4o', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4o-mini', 1, 'gpt-4o-mini', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'chatgpt-4o-latest', 1, 'chatgpt-4o-latest', 1);

-- ---- GPT-4.1 系列（指令增强、长上下文） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4.1', 1, 'gpt-4.1', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4.1-mini', 1, 'gpt-4.1-mini', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-4.1-nano', 1, 'gpt-4.1-nano', 0);

-- ---- GPT-5 系列（旗舰推理；支持 reasoning_effort、verbosity；不支持 temperature/top_p/penalties） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5', 1, 'gpt-5', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5-mini', 1, 'gpt-5-mini', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5-nano', 1, 'gpt-5-nano', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5-chat-latest', 1, 'gpt-5-chat-latest', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5-pro', 1, 'gpt-5-pro', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5-codex', 1, 'gpt-5-codex', 0);

-- ---- GPT-5.1 系列（reasoning_effort 支持 none） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.1', 1, 'gpt-5.1', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.1-chat-latest', 1, 'gpt-5.1-chat-latest', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.1-codex', 1, 'gpt-5.1-codex', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.1-codex-mini', 1, 'gpt-5.1-codex-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.1-codex-max', 1, 'gpt-5.1-codex-max', 0);

-- ---- GPT-5.2 系列（最新；支持 none/xhigh） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.2', 1, 'gpt-5.2', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.2-chat-latest', 1, 'gpt-5.2-chat-latest', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.2-pro', 1, 'gpt-5.2-pro', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gpt-5.2-codex', 1, 'gpt-5.2-codex', 0);

-- ---- 推理模型 O 系列（reasoning_effort；不支持 temperature/top_p/penalties） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o1', 1, 'o1', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o1-mini', 1, 'o1-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o1-pro', 1, 'o1-pro', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o3', 1, 'o3', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o3-mini', 1, 'o3-mini', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o3-pro', 1, 'o3-pro', 0);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'o4-mini', 1, 'o4-mini', 0);

-- ====================================================================
-- Google Gemini 模型（默认仅做参考；gemini-1.5 系列已停止支持，本脚本不再默认导入）
-- 新模型默认 Enable=1；如不希望启用可在管理员界面手动 Disable。
-- ====================================================================

-- ---- Gemini 2.5 系列（最新；支持 thinking_budget） ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-2.5-pro', 1, 'gemini-2.5-pro', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-2.5-flash', 1, 'gemini-2.5-flash', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-2.5-flash-lite', 1, 'gemini-2.5-flash-lite', 1);

-- ---- Gemini 2.0 系列 ----
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-2.0-pro', 1, 'gemini-2.0-pro', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-2.0-flash', 1, 'gemini-2.0-flash', 1);
INSERT INTO AvailableModels ( Enable, Name, IsChatModel, ModelValue, Vision) VALUES (1, 'gemini-2.0-flash-lite', 1, 'gemini-2.0-flash-lite', 1);