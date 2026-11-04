# 架构

## 组件

```text
浏览器 / 前端应用
        │ HTTP + JWT / SSE
        ▼
Turbo.Auth (ASP.NET Core)
 ├── Controllers：认证、管理、聊天、媒体、同步、文件提取
 ├── Repositories：账户、角色、模型、密钥、历史和任务的数据访问
 ├── KeyLoader + StableKeyPoolRepository：构建并原子发布可用的密钥/模型路由快照
 ├── Provider registry：按供应商类型解析已注册的聊天适配器
 ├── Chat handlers：OpenAI-compatible、Gemini、Anthropic、DashScope 等适配器
 └── Turbo.Kit：TXT、DOCX、PDF 文本提取
        │
        ├── MySQL：业务数据和供应商配置
        ├── Redis：生产环境的分布式缓存（未配置时使用内存缓存）
        └── 上游 AI API
```

`src/Turbo.Auth/Program.cs` 是组合根：它注册数据库上下文、认证授权、缓存、控制器和处理器，由 `RoutingRefreshService` 在启动接受流量前调用 `IKeyLoader.LoadKeys()`，随后轮询共享配置版本。`src/Turbo.Kit` 是独立类库，供文件提取接口和测试项目共享。

## 仓库布局

```text
src/
├── Turbo.Auth/          ASP.NET Core 服务
│   ├── Application/     聊天适配器、供应商目录和模型路由
│   ├── Contracts/       HTTP 请求、响应与输入校验
│   ├── Controllers/     HTTP 端点，按业务能力分组
│   ├── Data/Contexts/   Entity Framework Core 上下文
│   ├── Models/          数据库实体与内部模型
│   └── Repositories/    数据访问实现
└── Turbo.Kit/           文档文本提取类库
tests/
└── Turbo.Auth.Tests/    单元测试
docs/                    架构、接口、部署和开发文档
```

## 聊天请求路径

1. 客户端以 Bearer JWT 调用 `POST /api/ai/chat`，并指定已配置的 `model`。
2. `QuickModel` 从不可变的内存路由快照中，先按 `Priority` 选择优先级最高的健康路由，再按费用权重选择 `ModelKey`。
3. `PlayMixModelBacker` 解析实际模型标识；`ChatHandlerObtain` 从 DI 注册表按供应商类型选择适配器。
4. `ChatService` 协调路由与故障切换；适配器返回 `IAsyncEnumerable<string>`，Controller 统一输出 SSE 或旧客户端纯文本。

逻辑模型、供应商密钥和路由配置存放在 MySQL。路由包含 `ProviderModelValue`（上游模型名）、`Priority` 和 `Fee`，因此同一个逻辑模型可映射到不同供应商的不同上游模型。数据库触发器为配置变更递增共享版本，各实例默认每 5 秒自动刷新。`POST /api/sync/loadKeys` 可立即检查当前实例；无需逐台手动刷新。

在请求尚未写出响应前，路由失败会被记录，并尝试同一逻辑模型的下一条健康路由。连续失败达到 `AiRouting:FailureThreshold` 后，该路由会熔断 `AiRouting:BreakDurationSeconds`；流式响应已经开始后不会切换路由，以免拼接两个供应商的输出。

## 认证与权限

- `POST /api/auth/login` 返回 JWT；除登录、注册、验证码和公开模型接口外，控制器均需要 JWT。
- `admin` 可管理账户、角色、供应商密钥和模型。
- 聊天、媒体和文件功能按持久化的角色功能策略执行；`admin` 不绕过配额。见 [功能访问控制](feature-access.md)。
- CORS 仅允许 `Cors:AllowedOrigins` 配置中的绝对 URL。生产环境不要使用通配来源。

## 扩展供应商

添加供应商时，先在 `ProviderCatalog` 增加标识，再实现 `IChatHandler` 并在 `Program.cs` 注册。管理端的密钥类型列表也由该目录生成，无须维护第二份编号映射；最后通过管理接口创建 `SupplierKey`、`Model` 和关联记录。新适配器应支持取消、超时和不记录 API 密钥的结构化日志。

## 数据同步与持久化边界

`DataSyncService` 是历史、消息及生成任务的唯一写入口。新接口和旧 `/api/receiver` 都进入该服务；旧仓储保留读取。服务按账户锁定 `SyncAccounts`，把实体变化、删除事件、操作 ID 与递增版本一起提交到 `SyncChanges`。这避免了先删后插丢数据，以及使用提交顺序不确定的时间戳/全局自增 ID 作为同步游标。

前端保有独立聊天草稿及持久化操作队列。首次返回完整快照，后续按游标分页；客户端提交新数据与游标使用一个本地事务，并拒绝其他标签页已经超越的响应。

`IChatHandler` 不再依赖 Controller 请求模型或 `HttpResponse`。OpenAI 聊天与媒体通过 `OpenAiClientFactory` 注入实际命名客户端；Google 与 Anthropic 使用官方 SDK 的类型化流接口，保留配置的 BaseUrl 和系统指令；Alibaba 通过官方 OpenAI-compatible 协议复用 OpenAI 客户端。所有适配器使用受管理的传输和取消令牌。适配器必须检查终止标记，不能将提前 EOF 作为成功。

实施明细、协议兼容、配置失效与验证限制见 [架构审查与实施记录](architecture-review.md)。

Controller 请求与响应从 EF / SDK 模型分离，媒体调用由 MediaService 处理，实施范围与兼容性见 [Controller 契约与官方 SDK](api-contracts-and-sdks.md)。
