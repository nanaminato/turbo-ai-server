# RelaxKon 渐进式迁移

本轮目标是原地将 GitHub `nanaminato/turbo-ai-server` 改名为 `nanaminato/relaxkon-server`，前端 `nanaminato/turbo-user` 改名为 `nanaminato/relaxkon-web`，保留同一仓库及其 Git 历史。代码分支只更新展示名称、文档和只读配额查询，不执行远端仓库改名、生产部署或数据库迁移。GitHub 实际改名状态以仓库设置为准。

2026-11-06 服务端仓库已验证原地改名成功：仓库 ID 仍为 `994192000`，默认分支仍是 `master`，改名前后基准提交均为 `cf09c4e2324331b0419d7315e2427e54a503efe8`。当前 checkout 的 origin 已更新为新 URL。外部部署平台配置需另行核对。

## 必须保留的兼容标识

- `Turbo.AI.Server.sln`、`src/Turbo.Auth`、`src/Turbo.Kit`、测试项目、命名空间、嵌入资源名及 `Turbo.Auth.dll` 保留，既有构建和服务启动命令不变。
- `ConnectionStrings:ciko`、已有数据库名称、表、主键、账户、角色编码、聊天历史与任务记录保留。本次改名无需 SQL，不运行 `init.sql`、`--init-db`、`--create-admin` 或已有一次性升级脚本。
- 保留部署中现有 JWT issuer、audience、SecretKey、RefreshTokenPepper、会话与缓存前缀。示例中的 `turbo-ai-server` 和 `turbo-ai-client` 是认证协议标识，不是遗漏的品牌文本。不能因仓库改名替换，否则旧 token 无法验证。
- 保留 Data Protection 应用名 `Turbo.Auth`、密钥环及 `DataProtection:KeyPath`。密码重置、注册验证码和任务恢复回执依赖该身份；只改目录名或重建密钥会破坏未到期凭据。
- 保留 `/api/*` 路由、请求响应字段、流式协议、原 API 域名、前端生产 `apiUrl: '/'`、`wwwroot` 部署目录和默认开发端口 5111。GitHub 改名不会自动改应用域名。
- 前端仍需保留 IndexedDB/localStorage/设备身份等持久化键，不能因品牌改名清空或搬迁用户数据。

## 运维核对顺序

1. 改名前记录 GitHub 仓库 ID、默认分支 HEAD、各分支/标签及部署引用。使用仓库设置的 Rename，不能删除/重新创建仓库。
2. 验证新 URL 返回相同仓库 ID、默认分支和历史。GitHub 旧 URL 重定向可以帮助过渡，不应代替更新 checkout、CI、部署平台、webhook、徽章及外部引用；不要创建占用旧名称的新仓库。
3. 已有 checkout 更新 remote，不必重新克隆：

   ```bash
   git remote set-url origin https://github.com/nanaminato/relaxkon-server.git
   git fetch origin
   git remote -v
   ```

4. 审阅迁移分支并完成构建测试。仓库没有提交生产部署工作流，不能据此认定外部部署配置已全部更新。核对容器构建路径、服务启动命令、生产配置、共享密钥环和实际前端产物。
5. 在独立测试环境验证现有账户登录/刷新、聊天历史读写、SSE、图片回执恢复、角色变更、配额拒绝与代理，再按原发布流程发布。不要在业务库执行测试初始化。

## 动态权限与配额

现有 `admin`、`user`、`vip` 不可变角色编码、实时授权、每功能分钟/UTC 日请求数和 HTTP 并发租约继续沿用。管理员没有 AI 配额豁免，不自动增减现有角色授权或清空计数。

新增 `GET /api/feature-access/usage`，仅返回当前登录账户的只读快照。它复用已有表，无新增迁移，旧前端及旧 `/me`、`/policies` 契约不变。前端先发布时应对旧后端的 404/不可用状态显示“用量暂不可用”，不能把缺失数据当成 0。完整语义见 [功能权限与配额](feature-access.md)。

“AI 配额”仍是准入请求次数与 HTTP 并发数，不代表供应商 token、图片张数、远端在途任务或费用预算。APIMart 轮询使用独立 `image-task` 配额，429 应遵循 `Retry-After` 后再查询，不重新提交生成任务。计费单位扩展需要另行设计预留、结算与供应商用量对账，不应在本轮隐式改变现有计数语义。

## 回滚

本轮代码回滚无需回滚数据库；保留原发布产物、配置和密钥环即可。新前端遇到不提供 usage 的旧后端应继续正常工作。GitHub 反向改名需先确认旧名称仍可用及所有外部集成引用，不能通过新建同名仓库恢复。回滚期间同样不能重置账户、历史或令牌相关配置。

## 本轮验证（2026-11-06）

- .NET SDK 10.0.403 完成依赖恢复、Release 构建及 publish；发布产物仍包含 `Turbo.Auth.dll`。编译有 11 项既有 nullable/未使用变量警告，无错误。
- 普通测试 265 项通过，包含本地 Kestrel HTTP 契约、旧聊天流协议、新用量 camelCase/nullable JSON、身份隔离、存储失败、UTC 窗口、角色合并和新展示名称。
- 35 项显式 MySQL 用例未执行，其中包含本轮新增 2 项用量快照测试。没有连接或修改业务库、调用真实供应商、合并 PR 或生产部署。现有账户登录/刷新、真实 MySQL 多实例并发和生产前后端联通仍需在获准的测试/部署环境验收。
- 本轮新增只读端点及 CORS `Retry-After` 可见性，不改变既有计数单位或准入算法。仓库改名验证与代码测试分开记录，不能以构建通过代替仓库设置变更成功。
