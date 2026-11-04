# 部署与配置

## 前置条件

- .NET SDK 10（版本由 `global.json` 固定）。
- MySQL 8.4 或兼容版本。
- 生产环境可选 Redis；未设置 `ConnectionStrings:Redis` 时服务使用进程内缓存。

## 数据库初始化

首次部署只需通过 MySQL 客户端导入 SQL，不需要运行任何 .NET 初始化命令：

1. 创建空数据库，依次导入 `src/Turbo.Auth/Resources/merge/init.sql` 和 `open-initdata.sql`。
2. 在 `src/Turbo.Auth/Resources/account/create-admin.sql` 顶部填写管理员用户名与**真实可收信邮箱**，导入该文件。
3. 配置下面的邮件服务，正常部署并启动应用，在登录页选择“忘记密码”，输入用户名和邮箱，通过邮件设置初始密码。

管理员 SQL 使用空密码占位，初始无法登录，不需要填写明文或生成哈希。邮件设置密码后自动保存 Identity 密码哈希，管理员拥有 `admin`、`user`、`vip` 角色。脚本不会覆盖已有账户或已有管理员；若已经有管理员，保留原账户，只需确保邮箱可用，即可使用同一邮件重置流程。

### 邮件设置与密码重置

`appsettings.example.json` 已提供 `PasswordReset` 配置结构。默认 `Enabled=false`，未配置时接口返回 503，并显示本地化提示。部署时使用环境变量或密钥存储设置：

```bash
export PasswordReset__Enabled=true
export PasswordReset__PublicBaseUrl='https://app.example.com'
export PasswordReset__SmtpHost='smtp.example.com'
export PasswordReset__SmtpPort=587
export PasswordReset__EnableSsl=true
export PasswordReset__FromAddress='no-reply@example.com'
export PasswordReset__SmtpUsername='no-reply@example.com'
export PasswordReset__SmtpPassword='your-smtp-authorization-code'
export DataProtection__KeyPath='/var/lib/turbo-ai/keys'
```

### 实际发件账号如何配置

ASP.NET Core 自动将环境变量中的双下划线映射为配置层级。例如阿里企业邮箱：

```bash
export PasswordReset__SmtpHost='smtp.qiye.aliyun.com'
export PasswordReset__SmtpPort=465
export PasswordReset__EnableSsl=true
export PasswordReset__SmtpUsername='no-reply@your-domain.com'
export PasswordReset__FromAddress='no-reply@your-domain.com'
export PasswordReset__SmtpPassword='your-mailbox-password-or-client-security-password'
```

`SmtpUsername` 是 SMTP 认证账号；`FromAddress` 是用户在邮件里看到的实际发件地址，阿里邮箱通常应与认证账号保持一致。管理员的收件地址来自数据库 `Accounts.Email`，与发件地址分开配置。

本地开发也可将这些值放入 Git 已忽略的 `src/Turbo.Auth/appsettings.Development.json` 中的 `PasswordReset` 节。环境变量优先级高于 JSON，修改后重启后端生效。生产环境使用服务管理器、容器或托管平台的环境变量/密钥配置，不要把真实密码写入 `appsettings.example.json` 或提交到仓库。

SMTP 使用 MailKit：`EnableSsl=true` 时，465 使用隐式 TLS，其他端口使用必需的 STARTTLS，不会静默降级为明文；465 不允许关闭 TLS。阿里企业邮箱使用 `smtp.qiye.aliyun.com:465`，用户名为完整邮箱地址。未启用第三方客户端安全密码时使用邮箱登录密码；启用后必须使用独立的客户端安全密码，同时须开放 SMTP/第三方客户端登录权限。`PublicBaseUrl` 为可信的前端部署根地址，生产使用 HTTPS；本地调试允许 localhost HTTP。链接不使用客户端传入的 Host 或跳转地址。

生产环境需为 Data Protection 配置可持久化、受权限保护的密钥目录；多个实例必须共享同一密钥环与应用名。丢失或更换密钥后，尚未使用的重置链接会失效。Redis 共享发送冷却状态；IP 限流按实例执行，反向代理默认按代理 IP 聚合，如需识别客户端 IP，须按实际可信代理配置转发头。

重置链接 30 分钟有效，令牌放在 URL fragment，页面读取后从历史记录移除。重置成功后令牌及之前的其他重置链接失效，旧刷新令牌与访问会话撤销。用户名和邮箱不匹配时也返回同样的申请成功提示；同一组合有一分钟发送冷却，每 IP 15 分钟最多 10 次请求（申请与确认合计）。SMTP 发送失败仅记录错误类型，不记录收件人、链接、令牌或授权码。

### 可选的命令行初始化

仍保留 `--init-db`（空库初始化并创建管理员）和 `--create-admin`（已有库创建首个管理员），作为不使用邮件激活时的可选方式。两者不会启动 Web 服务，也不需要 JWT 配置；可复用 `ConnectionStrings__ciko` 或交互输入数据库信息。发布产物内已嵌入初始化 SQL。

`--init-db` 遇到已有表会拒绝执行；`--create-admin` 遇到已有管理员或重复用户名/邮箱会拒绝执行。MySQL DDL 不能回滚，建表中断时须先检查实际状态，命令不会自动删表重试。`Resources/account/input.sql` 仅保留作手动创建普通用户的参考。

## 配置服务

以 `src/Turbo.Auth/appsettings.example.json` 为模板创建环境专用配置。不要提交该文件或真实凭据。生产环境推荐使用环境变量或密钥存储：

```bash
export ConnectionStrings__ciko='server=db;port=3306;database=turboai;user=turboai;password=replace-me;Charset=utf8mb4'
export ConnectionStrings__Redis='redis:6379'
export Jwt__Issuer='turbo-ai-server'
export Jwt__Audience='turbo-ai-client'
export Jwt__SecretKey='a-long-random-secret'
export Jwt__RefreshTokenPepper='a-separate-long-random-secret'
export Cors__AllowedOrigins__0='https://app.example.com'
```

必须配置 `ConnectionStrings:ciko`、`Jwt:Issuer`、`Jwt:Audience`、`Jwt:SecretKey` 与独立的 `Jwt:RefreshTokenPepper`；缺少这些值时应用会拒绝启动。`Jwt:AccessTokenMinutes` 只能设为 10–15，`Jwt:RefreshTokenDays` 为 1–30。生产环境配置 Redis 后，账户安全版本和会话状态会共享缓存，以避免每次 API 请求访问 MySQL。`Cors:AllowedOrigins` 需要填写前端实际来源（协议、域名和端口）。`Diagnostics:EnableSensitiveDataLogging` 仅能在本地开发时临时设为 `true`。

`AiRouting:FailureThreshold` 指连续失败多少次后短暂熔断某一路由，`AiRouting:BreakDurationSeconds` 指熔断持续时间。默认值为 3 次和 60 秒；健康状态仅保存在进程内，重启后清空。

## 构建与运行

```bash
dotnet restore
dotnet publish src/Turbo.Auth/Turbo.Auth.csproj --configuration Release --output ./publish
ASPNETCORE_ENVIRONMENT=Production dotnet ./publish/Turbo.Auth.dll
```

示例配置中的 Kestrel 监听 `0.0.0.0:6000`。在生产环境使用反向代理终止 TLS，并将代理来源加入 CORS 白名单。Swagger 仅在 `Development` 环境注册，不应默认公开到生产网络。

## 部署前端

用户端与管理端已合并为单一 Angular 应用：[turbo-user](https://github.com/nanaminato/turbo-user)。生产构建使用环境文件中的同源 API 地址 `/`：

```bash
npm ci
npm run build
```

将构建产物中的浏览器文件部署到：

```text
src/Turbo.Auth/wwwroot
```

服务会为所有非文件路径回退到 `index.html`，因此聊天、图像、多媒体和管理路由共享同一入口。不要再创建 `wwwroot/ai`、`wwwroot/admin`，也不再支持通过 `assets/config.json` 在运行时设置 API 地址。

## 配置模型和密钥

1. 使用管理员账户登录，取得 JWT。
2. 通过 `GET /api/key/types` 选择供应商类型编号。
3. 创建启用的供应商密钥和模型，并添加它们的关联。
4. 调用 `POST /api/sync/loadKeys`，使更改立即进入内存密钥池。

详见 [API 使用](api.md)。

## 2026-11-04 架构升级

已有数据库在停止写入并备份后，运行一次 `src/Turbo.Auth/Resources/merge/architecture-upgrade.sql`；新库使用已包含这些表和触发器的 `merge/init.sql`。不要对已有库重跑完整初始化脚本。

先检查重复客户端 ID：

```sql
SELECT UserId, DataId, COUNT(*) FROM ChatHistories GROUP BY UserId, DataId HAVING COUNT(*) > 1;
SELECT ChatHistoryId, DataId, COUNT(*) FROM ChatMessages GROUP BY ChatHistoryId, DataId HAVING COUNT(*) > 1;
```

存在重复时先人工确定应保留的数据；唯一索引创建会拒绝重复，脚本不会删数据。迁移需要创建表、索引及触发器的权限，DDL 无法事务回滚。脚本中断时核对实际结构后继续，不能盲目整份重试。

新增 `SyncAccounts`、`SyncChanges` 用于按账户提交排序的同步与幂等重试；`RoutingConfiguration` 及目录触发器用于多实例路由失效。旧后端写入不会维护同步日志，不能在新客户端已经建立游标后直接回滚旧应用。

`AiRouting:RefreshIntervalSeconds` 默认 5，范围 1–60；`AiRouting:MaxStaleSeconds` 默认 30、最小 10。数据库无法确认配置超过阈值后，实例清空旧路由；失效检测还受轮询与超时影响，默认通常约 45 秒内停止使用旧配置处理新请求。已开始的流不受此开关终止。

先发布后端，再发布前端。新前端要求显式完成事件；旧客户端仍可使用纯文本协商和旧同步入口。更新 PWA 资源并验证待同步提示、断网重试、账户切换、双实例停用密钥及完整数据库联调。首次同步的导入策略和日志容量限制见 [架构实施记录](architecture-review.md)。

该升级已在 MySQL 8.4.13 的独立临时库中通过新库/旧库结构等价、数据保留、重复数据拒绝及 12 个路由触发器测试。证据、修复和适用边界见 [MySQL 验证记录](mysql-verification.md)；这不代表已经对业务数据库执行迁移。

## 启动前数据库检查

后端会在路由首次加载前，只读核对架构升级所需的表、列、唯一索引、12 个路由触发器及版本种子行。若看到“数据库架构尚未升级”或 RoutingConfiguration 不存在，说明当前连接的数据库尚未完成上述迁移；不会通过自动建表或跳过校验来绕过问题。备份并核对实际状态后完成迁移，再启动服务。

当前开发环境的复现、备份、数据保留和启动验证见 [启动修复记录](startup-repair.md)。其他部署环境仍需分别确认，不能根据测试库通过推断其已经升级。

## 注册邮箱验证

公开注册新增必填 emailCode，复用现有 PasswordReset SMTP 配置；Registration.Enabled 默认为 true。已有数据库需要先执行一次 `registration-email-verification.sql`，再配套发布前后端。配置、响应和回归证据见 [注册邮箱验证](registration-email-verification.md)。

### 独立路由权重升级（2026-11-05）

已完成架构升级的库还需执行一次 `src/Turbo.Auth/Resources/merge/routing-weight-upgrade.sql`。刷新旧快捷添加示例使用可重复运行的 `model-catalog-refresh.sql`。不要在已有库重跑 init.sql；具体升级顺序、兼容行为见 [模型目录与路由权重](model-catalog-and-routing.md)。
