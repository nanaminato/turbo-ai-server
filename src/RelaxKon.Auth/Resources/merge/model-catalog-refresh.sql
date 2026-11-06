-- Transactional example refresh; does not modify operational Models or chat history.
START TRANSACTION;
DELETE FROM AvailableModels WHERE ModelValue IN ('gpt-4o','gpt-4o-mini','chatgpt-4o-latest','gpt-4.1','gpt-4.1-mini','gpt-4.1-nano','gpt-5','gpt-5-mini','gpt-5-nano','gpt-5-chat-latest','gpt-5-pro','gpt-5-codex','gpt-5.1','gpt-5.1-chat-latest','gpt-5.1-codex','gpt-5.1-codex-mini','gpt-5.1-codex-max','gpt-5.2','gpt-5.2-chat-latest','gpt-5.2-pro','gpt-5.2-codex','o1','o1-mini','o1-pro','o3','o3-mini','o3-pro','o4-mini','gemini-2.5-pro','gemini-2.5-flash','gemini-2.5-flash-lite','gemini-2.0-pro','gemini-2.0-flash','gemini-2.0-flash-lite');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6.1-sol',1,'gpt-6.1-sol',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6.1-sol');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6.1-terra',1,'gpt-6.1-terra',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6.1-terra');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6-astra',1,'gpt-6-astra',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6-astra');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6-sol',1,'gpt-6-sol',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6-sol');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6-luna',1,'gpt-6-luna',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6-luna');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'claude-sonnet-5-5',1,'claude-sonnet-5-5',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='claude-sonnet-5-5');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'claude-haiku-5-5',1,'claude-haiku-5-5',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='claude-haiku-5-5');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'claude-opus-5-5',1,'claude-opus-5-5',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='claude-opus-5-5');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gemini-3.8-flash',1,'gemini-3.8-flash',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gemini-3.8-flash');
COMMIT;
