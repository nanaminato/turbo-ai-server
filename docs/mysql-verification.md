# MySQL 架构集成验证

日期：2026-11-04。实际服务器：MySQL 8.4.13。使用被 Git 忽略的 `tests/mysql.runsettings.user` 中的专用测试连接；未使用后端应用的业务数据库作为测试库。

## 执行结果

- 真实 MySQL 架构集成：10/10 通过，最终完整执行约 21 秒。
- 后端普通 Release 回归：100/100 通过，包含新增的 EF 跟踪版本缓存回归。
- 每个用例创建独立的 `turbo_arch_test_<guid>` 库；结构对比用例创建两个库，最终完整运行共创建并清理 11 个库。
- 测试连接中的默认数据库被置空；仅选择测试生成的库，清理前验证库名格式，连接池也仅清理对应临时库的池。
- 未部署应用、未修改既有业务数据，测试凭据未写入提交或结果日志。代码、测试及文档的提交与推送记录以 Git 为准。

| 验证项 | 用例数 | 验证内容 |
| --- | --- | --- |
| 并发同步：新库、旧库升级后 | 2 | 24 次并发重放同一操作只产生一个版本；16 路并发提交 205 个不同消息，版本连续；200＋5 分页；冲突载荷拒绝；删除不影响另一账户相同客户端 ID |
| 路由触发器与双实例：新库、旧库升级后 | 2 | 四张目录表的 INSERT/UPDATE/DELETE 共 12 个触发器逐项递增版本；事务回滚不改变版本；两个独立 KeyLoader 感知停用、恢复、供应商模型名更改与级联删除 |
| 真实旧版本升级与结构等价 | 1 | 固定使用提交 e01436d 的原始建表脚本；执行当前升级脚本；字段、索引、外键、触发器与全新初始化完全一致；历史、消息、附件主键/内容、任务结果保留；重复运行明确拒绝 |
| 重复数据拒绝升级 | 2 | 分别制造重复历史 ID、重复消息 ID，确认 1062；原行和附件保留；消息索引失败时此前创建的历史索引仍存在，确认 MySQL DDL 的部分执行边界 |
| 写入失败原子回滚 | 1 | 用测试触发器拒绝新附件，验证原消息、原附件保留，游标和变更日志未推进 |
| 按账户独立锁 | 1 | 显式持有账户 1 的行锁，账户 1 写入等待，而账户 2 仍可完成；释放后账户 1 正常完成 |
| EF 上下文复用 | 1 | 同一上下文先读取，其他连接提交后再写入，确保使用最新版本而非旧跟踪值 |

## 发现并修复的缺陷

第一次扩展集成执行为 9 项通过、1 项失败。失败场景是复用 `AuthContext`：第一次读取把 `SyncAccount.Version=0` 留在 EF 跟踪缓存中；另一连接提交版本 1 后，后续 `SELECT ... FOR UPDATE` 虽然取得行锁，EF 身份映射仍返回旧对象。下一次写入试图再次插入版本 1，触发 `SyncChanges.PRIMARY` 冲突。

`DataSyncService.LockAccount` 现在在 INSERT/UPSERT 已取得账户行锁后，对已跟踪的 `SyncAccount` 执行 `ReloadAsync`，然后继续锁定读取。刷新发生在同一事务和行锁内，不会引入版本竞争。新增 MySQL 用例以及普通 SQLite 用例均通过，最终 10 项真实 MySQL 用例全部通过。升级脚本本身未发现需要修改的问题。

## 复现

已有本地测试配置时运行：

```powershell
dotnet test tests/Turbo.Auth.Tests/Turbo.Auth.Tests.csproj --configuration Release --no-restore --settings tests/mysql.runsettings.user --filter FullyQualifiedName~ArchitectureDatabaseTests --logger 'trx;LogFileName=architecture-mysql.trx' --results-directory TestResults/architecture-mysql
```

也可通过进程环境变量 `TURBO_TEST_MYSQL` 提供专用连接并省略 `--settings`。测试账户需要建库、删库、建表、索引及触发器权限。SQL 资源随测试程序集嵌入，不依赖运行目录；旧库基线位于 `tests/Turbo.Auth.Tests/Fixtures/pre-architecture-e01436d.sql`，升级资源直接引用实际部署脚本。

NUnit 的显式测试使用独立的类过滤器运行；混合普通测试与显式测试的 OR 过滤器可能让 NUnit 不执行显式用例，必须确认结果显示 10 项已执行。普通完整回归不会自动运行这些真实 MySQL 用例。

最终测试结果保存在 Git 忽略的 `TestResults/architecture-mysql/architecture-mysql.trx` 和 `TestResults/architecture-regression/architecture-regression.trx`。

## 边界

此验证覆盖 MySQL 8.4.13 上的临时新库和固定旧版本升级，不代表已经在生产执行迁移，也不包含真实供应商联网调用或其他数据库版本。升级脚本仍为一次性、非事务 DDL：有重复数据时需先人工处理，中断后检查实际结构，不能整份盲目重跑。
