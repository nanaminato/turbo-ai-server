-- Add examples only; existing routes and history remain unchanged.
START TRANSACTION;
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6.1-sol',1,'gpt-6.1-sol',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6.1-sol');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6.1-terra',1,'gpt-6.1-terra',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6.1-terra');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6-astra',1,'gpt-6-astra',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6-astra');
INSERT INTO AvailableModels (Enable,Name,IsChatModel,ModelValue,Vision) SELECT 1,'gpt-6-sol',1,'gpt-6-sol',1 WHERE NOT EXISTS (SELECT 1 FROM AvailableModels WHERE ModelValue='gpt-6-sol');
COMMIT;
