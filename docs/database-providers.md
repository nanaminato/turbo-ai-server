# 可配置数据库：MySQL / SQLite

## 选择与兼容

通过 `Database:Provider` 选择 `MySql` 或 `Sqlite`（不区分大小写）。不配置时仍是 **MySQL**；未知值启动失败，不自动回退。`ConnectionStrings:ciko` 仅连接选中的数据库。SQLite 不会尝试连接 MySQL，也不要求安装 MySQL 服务。

- MySQL：保留原连接字符串、8.4 provider、InnoDB 行锁、初始化/升级脚本，适合多服务器部署和较多并发写入。
- SQLite：自带运行时驱动，适合单服务器小规模部署。使用绝对文件路径、持久化本地磁盘、WAL、外键与有界等待；不支持内存数据库或 URI 连接。普通启动使用 `ReadWrite`，文件丢失不会自动创建空库。
- JWT issuer/audience、密钥、`Turbo.Auth` Data Protection 身份、账户哈希、API 与聊天协议不变。配置切换不会搬迁任何数据。

## SQLite 首次部署（无需 MySQL）

先构建发布产物。下例在发布目录运行；首次初始化命令也读取当前目录的 appsettings 文件与环境变量，环境名与 Web 启动一致：DOTNET_ENVIRONMENT 优先于 ASPNETCORE_ENVIRONMENT，未设置为 Production。管理员密码通过隐藏输入，不放入命令行参数。

```bash
dotnet restore
dotnet publish src/Turbo.Auth/Turbo.Auth.csproj -c Release -o ./publish
cd publish

# 使用专属服务账户；容器需挂载本机持久卷。限制本机其他账户访问。
umask 077
mkdir -p /absolute/persistent/relaxkon
chmod 700 /absolute/persistent/relaxkon
export Database__Provider=Sqlite
export ConnectionStrings__ciko='Data Source=/absolute/persistent/relaxkon/relaxkon.db;Default Timeout=30'
# 不用 Redis 时覆盖示例/旧配置中的 Redis 地址。
export ConnectionStrings__Redis=''

# 仅用于全新空库；交互输入首个管理员用户名、真实邮箱、密码。
dotnet Turbo.Auth.dll --init-db

# 用独立强随机值配置下列两个 secret；不能使用示例值。
export Jwt__SecretKey='replace-with-a-long-random-secret'
export Jwt__RefreshTokenPepper='replace-with-another-long-random-secret'
export DataProtection__KeyPath='/absolute/persistent/relaxkon/keys'
export Cors__AllowedOrigins__0='https://app.example.com'
ASPNETCORE_ENVIRONMENT=Production dotnet Turbo.Auth.dll
```

`--init-db` 创建完整 schema、9 个公开模型条目、三个基础角色及首个管理员，密码存储为 Identity 哈希。管理员可直接登录，无需 SMTP 设置初始密码；公开注册和邮件重置仍需单独配置 SMTP。不会创建真实供应商密钥。功能配额默认不授权，登录后由管理员在权限页面显式配置角色功能。

已有完整 SQLite 库且没有管理员时可使用 `--create-admin`。重复初始化、重复管理员或用户名/邮箱冲突均拒绝，不覆盖账户。普通启动只读检查完整表、关键列、唯一索引、12 个路由触发器、版本种子和 WAL，不自动建表、修复或升级。

文件路径必须为当前 OS 的绝对本地路径；Windows 可用 `Data Source=C:\relaxkon-data\relaxkon.db`。应用账户需能写数据库所在目录（含 `-wal` / `-shm`）。连接统一强制开启 foreign_keys、Private cache，等待超时限制为 1–60 秒，默认 30 秒。Unix 新建数据库文件自动使用 0600，不修改已有文件权限。已有数据库及备份需由服务账户拥有，文件权限设为 0600、父目录 0700；Windows 配置等价仅服务账户访问的 ACL。文件本身及备份含账号、会话和供应商密钥，不要放进 wwwroot 或 Git。

## 并发、配额与部署边界

SQLite 在数据库层使用 **BEGIN IMMEDIATE** 串行化写事务，而非进程内锁。分钟/UTC 日计数、并发租约、策略 revision、任务归属、同步游标、注册与安全会话不能通过另开连接绕过。配额仍是已准入 HTTP 请求数，不是 token、费用或远端任务数。数据库忙、缺表、文件不可用会失败，不回退到内存配额，也不会伪装为零用量。

只读用量查询与路由快照使用 deferred 事务，在 WAL 中读取同一提交快照，不占用写锁。租约时间和 UTC 固定窗口取数据库时钟。

推荐 SQLite **单应用实例、单服务器**。测试中的多个独立连接/Store 验证同一文件的数据库锁，不是对任意分布式部署的承诺。SQLite 同时只允许一个 writer，高并发时请求等待，可能出现超时；需要高吞吐或多服务器时选择 MySQL。

禁止把 SQLite 文件放在 NFS/SMB 等网络文件系统，也不能给每个副本独立 SQLite 文件后声称共享账号/配额。程序无法可靠判定所有网络挂载或多个副本路径，部署者必须保证同一持久本地文件；多服务器使用 MySQL。Redis 仅共享缓存，不能把独立 SQLite 文件合并成共享数据库。IP 限流、熔断等既有进程级状态不因数据库支持而变为分布式状态。

## 排序规则与迁移

SQLite 用户名、邮箱、角色显示名称使用 `NOCASE`，只对 ASCII 字母忽略大小写；角色 Code、功能/租约/任务 ID 保持 BINARY 精确匹配。它 **不等价于** MySQL 的 Unicode、重音不敏感 `utf8mb4_0900_ai_ci`。SQLite 不隐式改变 Unicode 名称，也不在数据库上制造自定义不稳定排序规则。

没有自动 MySQL↔SQLite 数据迁移工具。不要把 MySQL SQL dump 或 merge/upgrade 脚本导入 SQLite，也不要指向新文件把原账号“迁移”掉。既有 MySQL 升级仍按原流程且不需为本功能改变表结构。

若将来需要搬迁：停写、备份，审计姓名/邮箱/角色/模型标识的排序规则差异和重复项，在隔离库做显式转换，保留全部主键、关系、密码哈希、SecurityStamp、会话和任务归属、配额窗口/租约、同步游标和变更记录，再验证登录、刷新、权限、历史、供应商路由、日期/JSON和配额。保留 JWT/pepper/密钥环。未完成校验前不切换生产配置；跨引擎迁移和生产部署不包含在此 PR。

## 备份与回滚

停服务并关闭全部连接后再复制数据库，或使用 SQLite Online Backup API / sqlite3 `.backup` 生成一致备份。**不要只复制正在运行的 `.db` 文件而忽略 WAL**。同时备份 Data Protection 密钥与配置。恢复到隔离环境验证后再用于服务。不要删除 WAL/SHM 来“解锁”。

MySQL 部署可回滚本次代码且无需数据库降级；SQLite 部署不能回滚到仅支持 MySQL 的旧二进制。保留兼容版本的程序、完整 SQLite 备份及密钥环。

## 验证

普通 `dotnet test Turbo.AI.Server.sln -c Release` 包含真实临时 SQLite 文件、独立连接并发和完整初始化 schema 测试，不使用业务数据库或真实供应商。

MySQL 集成测试是显式测试，需专用 `TURBO_TEST_MYSQL` 连接，测试创建并删除自己的随机临时库。用单独类过滤器运行并确认执行数量，不能把普通测试中的 skipped 当通过。现有历史 MySQL 验证见 [MySQL 验证记录](mysql-verification.md)；本轮实际结果以 PR 记录为准。

PR 工作流 `Database compatibility` 提供普通/SQLite 回归和隔离 MySQL 8.4 服务测试两个 job。MySQL 每组显式类单独运行，最后确认至少 35 条结果全部 Passed；工作流配置本身不代表已经执行成功，应查看对应提交的 Actions 结果。

### 本轮本地验证（2026-11-06）

- .NET SDK 10.0.403 restore、Release build、publish 成功；仍有 11 条既有 nullable/未使用变量警告。
- 307 项普通测试全部通过，包含新增 42 项 provider/完整 SQLite schema/真实文件并发/只读快照测试。35 项显式 MySQL 用例未在本地执行，缺少隔离 MySQL 服务与测试连接；没有用业务库代替。
- 对发布产物实际执行 `--init-db`、启动本地 Kestrel，验证管理员大小写登录、usage（8 功能且 no-store）、策略读取、会话列表、刷新轮换与退出。仅临时 SQLite 文件，无 MySQL、Redis 或真实供应商服务。
- 没有修改生产数据、迁移既有账户、部署、合并或启用自动合并。
