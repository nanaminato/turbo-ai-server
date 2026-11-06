# 可配置功能访问与请求频率

## 策略与接口

权限由 MySQL `FeatureAccessPolicies` 单行 JSON 配置持久化，不从前端或 JWT 中的历史角色决定。每次认证会重新查询账户当前角色并替换 JWT 角色声明；功能准入也查询当前角色。管理员只有策略管理权，没有 AI 功能或配额豁免。

- `GET /api/feature-access/me`：当前账户的 `canManagePolicies` 与 `features` 有效策略。
- `GET /api/feature-access/usage`：当前账户的只读用量快照，见下文；不接收其他账户 ID。
- `GET /api/feature-access/policies`：管理员读取 `{revision,features}`。
- `PUT /api/feature-access/policies`：管理员提交完整文档，携带读到的 revision。成功返回 revision+1；并发编辑返回 HTTP 409，必须重新读取并人工合并。
- 每个 feature 为 `{key,roles:{"user":{enabled,requestsPerMinute,requestsPerDay,maxConcurrent}}}`。角色键必须是 Roles 中已有的不可变 Code，大小写精确匹配，不是可修改的显示名称。普通用户对应既有 `user`，管理员 `admin`，VIP `vip`；可以配置其他现有角色。
- 限制为正整数，`null` 为无限制；`enabled:false` 为禁用。零/负数拒绝。缺少角色配置意味着不授权。
- 多角色取已启用授权的并集，各项限制取最宽松值（任一 null 则无限制）。禁用条目不覆盖其他角色的允许。角色身份必须由管理员管理；注册接口不接受客户端自授角色。

功能 key：`chat`、`image`（所有 OpenAI/APIMart 生图入口共享）、`tts`、`transcription`、`translation`、`file-extraction`、`image-task`、`image-proxy`。模型枚举是认证读取；公开模型目录保持公开。聊天历史同步保持既有身份/所有权规则；身份验证、密码重置与控制平面保留现有独立认证/速率策略，不通过 AI 策略授予管理员权限。新增收费/计算功能必须添加 key、端点元数据和测试。

SQLite 支持说明：本页 MySQL/InnoDB 描述仍适用于默认 provider；SQLite 在同一本机持久文件上使用 immediate 写事务和 deferred 读快照，部署与并发边界见 [数据库 provider](database-providers.md)。

## 只读用量快照

`GET /api/feature-access/usage` 返回 `{observedAt,policyRevision,features}`，时间为 UTC ISO 8601。每项包含原有效策略字段 `key/enabled/requestsPerMinute/requestsPerDay/maxConcurrent`，以及 `minuteUsed/dayUsed/activeRequests`、`minuteRemaining/dayRemaining/concurrentRemaining`、`minuteResetsAt/dayResetsAt`。

- 账户 ID 只取认证身份，不接受查询其他账户。响应 `Cache-Control: no-store`，不包含其他账户、提示词、任务 ID、供应商密钥或租约 ID。
- 所有角色、策略、计数和租约来自同一 MySQL `REPEATABLE READ` 快照，观察时间来自数据库时钟。失效窗口按 0 显示，过期租约不计入 `activeRequests`；查询不删除租约、不消耗配额、不重置计数。
- 启用且不限额时 remaining 为 `null`；禁用时为 0。降低限额后已用量仍保留，剩余量下限为 0。没有使用记录的功能返回 0 已用量，但缺表或存储失败返回 503 `feature_access_unavailable`，不能伪造为“未使用”。
- 快照是观察值，不是容量预留。其他会话/实例可能立即消费配额，实际请求仍可能返回 429。页面应显示更新时间，不把 `activeRequests` 当成供应商远端运行任务数。此端点不需另行授权 AI 功能，管理员也只能看自己的用量。
- 请求单位仍是一项 HTTP 操作，不是 token、图片数量或费用。图像任务查询各消耗一次 `image-task` 配额；收到 429 应等待 `Retry-After`，不能重新提交生成任务。低日查询配额可能使任务状态查询暂停至 UTC 次日，但不会删除已提交的任务。

新端点不改变现有 `/me` 或 `/policies` 响应，不需要 SQL 升级。普通测试覆盖窗口投影、剩余量、身份隔离与失败响应；显式 `FeatureUsageDatabaseTests` 覆盖真实 MySQL 读取、当前角色和策略、过期租约及无写入，需独立测试库运行。

## 计数与多实例

按账户 ID + 功能分区，多个会话、模型和服务实例共享配额。MySQL InnoDB 行锁在同一事务内检查分钟计数、UTC 自然日计数及并发租约并消费一次请求。使用数据库时钟，不依赖应用节点时间。固定窗口边界允许相邻窗口突发；不是滑动窗口。已准入请求即计费次数，之后参数错误、供应商失败、断线或取消均不退款，防止重试绕过。拒绝请求不增加次数。

HTTP 429 返回 `code:feature_limit_exceeded` 和 `Retry-After`（秒）。HTTP 403 为 `feature_disabled`；认证失败为 401；数据库/配额服务故障为 503，不回退到本机计数。配额跨应用重启保留，策略更新不清空计数。配额数据仅存账户 ID、feature、次数和租约，不存提示词。

`maxConcurrent` 是正在处理的 HTTP 请求数，包括整个聊天响应流，不是 APIMart 远端异步任务数量。APIMart 提交成功后 HTTP 租约释放；远端任务仍可能运行。租约每 20 秒续期、90 秒到期；连接断开和异常使用 finally 释放，进程崩溃由到期回收。续期失败中止 HTTP 并取消上游调用。网络中断后远端供应商可能仍继续任务，此系统不保证取消远端已接受的工作。角色或策略修改作用于后续准入，不强制终止已经准入的响应流。

## 部署与迁移

1. 备份数据库，先执行 `src/Turbo.Auth/Resources/merge/feature-access.sql`；新安装 `--init-db` 的嵌入式 schema 已包含这些表。不要在生产环境调用测试初始化。
2. 所有副本必须共享同一 MySQL 主库；不能将限流读写路由至异步只读副本。
3. **默认没有功能授权：升级后所有受管功能拒绝，包含管理员。** 使用已有管理员账户打开前端策略页面，明确保存各角色开关、分钟/每日请求数及并发限制后再开放流量。没有自动把原来的 VIP 策略转换成无限权限。
4. 图片代理改为 Bearer 认证请求。前端使用认证 blob 请求后创建对象 URL。设置 `ImageProxy:AllowedHosts` 为供应商实际图片 CDN 的精确主机名，不支持通配符；默认空名单拒绝。仅允许 HTTPS/443、公共地址，连接时解析并固定 IP，禁止重定向和环境代理，限制为 20 MiB raster image、30 秒。
5. APIMart 查询必须匹配服务端提交时记录的账户/任务 ID。旧任务没有可靠所有权记录，升级后不能通过该接口查询，需由运维从可信提交日志验证并迁移；不要信任客户端可写的历史同步记录来回填所有权。
6. 维护数据生命周期：账户删除后可删除对应 Usage/Leases/Tasks；未到期租约不能删除，当前 UTC 日使用记录不能清零。已结束旧任务的所有权可按产品保留策略清理，清理后不可再次查询。
7. 修改/删除角色后显式移除过时策略映射；不能把旧映射隐式迁移到一个新角色。

生产上线前需真实 MySQL 集成验收：两个服务副本同时请求不能超额、跨 UTC 窗口、服务崩溃租约回收、续期失败、取消流、管理员降权旧 token、策略冲突、缺表失败关闭。单元测试不替代这些部署验证。

## 自动化验证

普通测试包括角色合并、默认拒绝、非法配置、计算入口元数据覆盖、图片代理内网/特殊 IP 检查。

```bash
dotnet test tests/Turbo.Auth.Tests/Turbo.Auth.Tests.csproj --configuration Release
# 仅使用隔离测试 MySQL，连接需有创建/删除临时库权限；测试不会使用连接中的数据库。
# 通过安全环境变量设置 TURBO_TEST_MYSQL 后：
dotnet test tests/Turbo.Auth.Tests/Turbo.Auth.Tests.csproj --filter FullyQualifiedName~Distributed_admission_policy_conflicts_and_ownership_are_atomic
```

数据库测试覆盖两个独立 Store 的 20 路同时请求、日/分钟计数、租约续期/释放/过期、策略 revision 冲突、任务所有权。
早期功能策略实现时，普通测试通过；当时 MySQL 8.4.7 官方本地运行包已初始化，但运行环境拒绝服务器 Unix socket（Operation not permitted），显式配额集成测试无法连接而失败，**该次数据库配额并发验证未通过**。其他数据库功能的后续验证见各功能文档；本轮改名与用量查询验证见 [迁移说明](relaxkon-migration.md)。上线前仍须在允许运行 MySQL 的隔离环境执行上述配额集成测试及 HTTP 端到端验收。
