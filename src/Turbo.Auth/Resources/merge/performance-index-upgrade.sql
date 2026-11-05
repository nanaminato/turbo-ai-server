-- Additive performance indexes for an existing current schema (MySQL 8).
-- Run outside a transaction: CREATE INDEX implicitly commits. Schedule for low traffic.
-- No rows, existing indexes, constraints, or column definitions are changed.
-- Repeated runs skip indexes already present under the exact expected name.
-- A same-named index with a different column order/uniqueness aborts before DDL.
CREATE TEMPORARY TABLE turbo_performance_index_guard (Valid int NOT NULL CHECK (Valid=1));
INSERT INTO turbo_performance_index_guard
SELECT IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Accounts' AND INDEX_NAME='IX_Accounts_Username'
 GROUP BY TABLE_NAME,INDEX_NAME
 HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') <> 'Username' OR MIN(NON_UNIQUE) <> 1
 OR SUM(SUB_PART IS NOT NULL) > 0
),0,1);
INSERT INTO turbo_performance_index_guard
SELECT IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Accounts' AND INDEX_NAME='IX_Accounts_Email'
 GROUP BY TABLE_NAME,INDEX_NAME
 HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') <> 'Email' OR MIN(NON_UNIQUE) <> 1
 OR SUM(SUB_PART IS NOT NULL) > 0
),0,1);
INSERT INTO turbo_performance_index_guard
SELECT IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='GenerateTasks' AND INDEX_NAME='IX_GenerateTasks_AccountId_DateTime_TaskId'
 GROUP BY TABLE_NAME,INDEX_NAME
 HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') <> 'AccountId,DateTime,TaskId' OR MIN(NON_UNIQUE) <> 1
 OR SUM(SUB_PART IS NOT NULL) > 0
),0,1);
INSERT INTO turbo_performance_index_guard
SELECT IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='RefreshTokens' AND INDEX_NAME='IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt'
 GROUP BY TABLE_NAME,INDEX_NAME
 HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') <> 'AccountId,RevokedAt,ExpiresAt' OR MIN(NON_UNIQUE) <> 1
 OR SUM(SUB_PART IS NOT NULL) > 0
),0,1);
DROP TEMPORARY TABLE turbo_performance_index_guard;

SET @turbo_performance_ddl = IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Accounts' AND INDEX_NAME='IX_Accounts_Username'
), 'SELECT 1', 'CREATE INDEX IX_Accounts_Username ON Accounts (`Username`)');
PREPARE turbo_performance_stmt FROM @turbo_performance_ddl;
EXECUTE turbo_performance_stmt;
DEALLOCATE PREPARE turbo_performance_stmt;

SET @turbo_performance_ddl = IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Accounts' AND INDEX_NAME='IX_Accounts_Email'
), 'SELECT 1', 'CREATE INDEX IX_Accounts_Email ON Accounts (`Email`)');
PREPARE turbo_performance_stmt FROM @turbo_performance_ddl;
EXECUTE turbo_performance_stmt;
DEALLOCATE PREPARE turbo_performance_stmt;

SET @turbo_performance_ddl = IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='GenerateTasks' AND INDEX_NAME='IX_GenerateTasks_AccountId_DateTime_TaskId'
), 'SELECT 1', 'CREATE INDEX IX_GenerateTasks_AccountId_DateTime_TaskId ON GenerateTasks (`AccountId`, `DateTime`, `TaskId`)');
PREPARE turbo_performance_stmt FROM @turbo_performance_ddl;
EXECUTE turbo_performance_stmt;
DEALLOCATE PREPARE turbo_performance_stmt;

SET @turbo_performance_ddl = IF(EXISTS (
 SELECT 1 FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='RefreshTokens' AND INDEX_NAME='IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt'
), 'SELECT 1', 'CREATE INDEX IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt ON RefreshTokens (`AccountId`, `RevokedAt`, `ExpiresAt`)');
PREPARE turbo_performance_stmt FROM @turbo_performance_ddl;
EXECUTE turbo_performance_stmt;
DEALLOCATE PREPARE turbo_performance_stmt;

