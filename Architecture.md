# 架构

## 组件

```text
浏览器 / 前端应用
        │ HTTP + JWT / SignalR
        ▼
Turbo-Auth (ASP.NET Core)
 ├── Controllers：认证、管理、聊天、媒体、同步、文件提取
 ├── Repositories：账户、角色、模型、密钥、历史和任务的数据访问
 ├── KeyLoader + StableKeyPoolRepository：加载并选择可用的密钥/模型组合
 ├── Chat handlers：OpenAI-compatible、Gemini、Anthropic、DashScope 等适配器
 └── Turbo-Kit：TXT、DOCX、PDF 文本提取
        │
        ├── MySQL：业务数据和供应商配置
        ├── Redis：生产环境的分布式缓存（未配置时使用内存缓存）
        └── 上游 AI API
```

`Turbo-Auth/Program.cs` 是组合根：它注册数据库上下文、认证授权、缓存、控制器和处理器，并在启动时调用 `IKeyLoader.LoadKeys()`。`Turbo-Kit` 是独立类库，供文件提取接口和测试项目共享。

## 聊天请求路径

1. 客户端以 Bearer JWT 调用 `POST /api/ai/chat`，并指定已配置的 `model`。
2. `QuickModel` 从内存密钥池选择该模型可用的 `ModelKey`。
3. `PlayMixModelBacker` 解析实际模型标识；`ChatHandlerObtain` 根据供应商类型选择适配器。
4. 适配器调用上游 API，并把流式文本写回原始 HTTP 响应。

模型和密钥的配置存放在 MySQL。每次变更后调用 `POST /api/sync/loadKeys` 刷新内存池，或重启服务使其重新加载。

## 认证与权限

- `POST /api/auth/login` 返回 JWT；除登录、注册、验证码和公开模型接口外，控制器均需要 JWT。
- `admin` 可管理账户、角色、供应商密钥和模型。
- `user` 可读取可用聊天模型；`vip` 可调用聊天和媒体接口。
- CORS 仅允许 `Cors:AllowedOrigins` 配置中的绝对 URL。生产环境不要使用通配来源。

## 扩展供应商

添加供应商时，先在 `HandlerType` 增加标识，再实现 `IChatHandler`，并在 `ChatHandlerObtain` 中映射它。管理端的密钥类型列表定义在 `KeyController`；最后通过管理接口创建 `SupplierKey`、`Model` 和关联记录。新适配器应支持取消、超时和不记录 API 密钥的结构化日志。
