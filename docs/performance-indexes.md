# 数据库查询索引（2026-11-05）

新增四个非唯一索引，与 EF Core 显式映射、新库合并脚本及单独建表脚本保持一致：

| 表 | 索引 | 列顺序 | 对应查询 |
| --- | --- | --- | --- |
| Accounts | IX_Accounts_Username | Username | 登录及用户名存在性检查 |
| Accounts | IX_Accounts_Email | Email | 邮箱存在性检查 |
| GenerateTasks | IX_GenerateTasks_AccountId_DateTime_TaskId | AccountId, DateTime, TaskId | 按账户读取任务，按时间与任务 ID 稳定排序 |
| RefreshTokens | IX_RefreshTokens_AccountId_RevokedAt_ExpiresAt | AccountId, RevokedAt, ExpiresAt | 查找账户未撤销且未过期的会话 |

不将用户名或邮箱改为唯一约束；不清理、合并或改写已有重复账户。本次索引只改善查询路径，不改变业务唯一性语义。既有索引全部保留。额外索引会增加写入和存储成本。

## 已有数据库

完成既有结构升级后执行 `src/RelaxKon.Auth/Resources/merge/performance-index-upgrade.sql`。该脚本只添加上述索引，没有 DELETE、UPDATE、DROP INDEX 或 ALTER COLUMN。按精确索引名称判重，可重复运行；若发现同名索引的列顺序、唯一性或前缀长度不符，会在添加任何持久索引前拒绝执行，需人工核查差异。不同名称的等价索引不会被自动删除或复用。

脚本需要选定数据库以及索引创建权限，客户端需支持用户变量和 PREPARE。不要在业务事务内执行：MySQL CREATE INDEX 会隐式提交。建议低峰期执行，耗时和锁影响取决于表大小及服务器负载；中断后可重新运行。校验失败后先关闭执行会话，再修复结构并重试，以释放临时校验表。

已在 2026-11-05 对开发业务库 niko 执行本脚本，新增四个索引成功。执行前确认没有其他应用连接，执行后核对全部 22 张表的行数保持不变。执行前后核对记录保存在 Git 忽略的 TestResults/performance-upgrade-20261105-231035/{before,verified}。未更改列、删除索引或修改业务行。应用查询仍可使用尚未添加索引的旧结构；索引缺失主要影响性能。

## 验证

使用被 Git 忽略的 `tests/mysql.runsettings.user` 中的测试连接。测试清空连接中的默认数据库，仅创建和删除随机的 `turbo_arch_test_<guid>` 临时库，不选择既有业务库。

~~~powershell
dotnet test tests/RelaxKon.Auth.Tests/RelaxKon.Auth.Tests.csproj --configuration Release --no-restore --settings tests/mysql.runsettings.user --filter FullyQualifiedName~PerformanceIndexDatabaseTests --logger 'trx;LogFileName=performance-indexes.trx' --results-directory TestResults/performance-indexes
~~~

MySQL 8.4.13 上四项检查通过：

- 对历史结构重复执行升级，四个索引与新库定义一致；Accounts、GenerateTasks、RefreshTokens 的全部测试行内容保持不变。
- 同名异构索引触发前置拒绝，不创建其他索引。
- 新库与历史升级库分别填充 1,000 个账户、6,001 个任务、6,000 条刷新令牌，更新统计信息后检查真实 `EXPLAIN FORMAT=JSON`。
- 用户名、邮箱、按账户排序任务、活跃会话查询均由优化器选择对应新索引；任务按 DateTime DESC、TaskId DESC 查询不需要 filesort。没有使用 FORCE INDEX。这里验证代表性查询计划，不宣称生产耗时或吞吐量提升比例。

已有 ArchitectureDatabaseTests 的历史升级路径也执行此脚本，12/12 检查通过，继续对比所有列、索引、外键和触发器，避免新旧初始化漂移。测试结果在 Git 忽略的 `TestResults/performance-indexes` 与 `TestResults/performance-architecture`。
