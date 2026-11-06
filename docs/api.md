# API 使用

开发环境启动服务后可在 `/swagger` 浏览完整的请求模型和响应。以下是最常用的调用路径。

## 认证

注册和登录均使用 JSON 请求体：

```bash
curl -X POST http://localhost:5111/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"developer","password":"change-me"}'
```

成功响应包含短效 `accessToken`（兼容字段 `token`）、`refreshToken`、`sessionId` 和 `id`。访问令牌默认有效 15 分钟；刷新令牌仅可用于轮换，服务端不保存明文。客户端应在收到 401 后调用 `POST /api/auth/refresh`，并以响应中的新刷新令牌覆盖旧值。在受保护接口中传入访问令牌：

```text
Authorization: Bearer <token>
```

`POST /api/auth/register` 接收 `email`、`username`、`password` 和 `confirm`。管理员也可以通过 `/api/account` 管理账户和角色。

会话接口：

- `POST /api/auth/refresh`：请求体为 `{ "refreshToken": "…", "deviceName": "…" }`，每次成功都会返回一对新令牌。
- `POST /api/auth/logout`：传入刷新令牌，注销当前设备。
- `POST /api/auth/logout-all`：注销当前账户的全部设备。
- `GET /api/auth/sessions`、`DELETE /api/auth/sessions/{sessionId}`：查看或踢出某个设备会话。
- `POST /api/account/change-password`：登录后传入 `currentPassword`、`newPassword`、`confirmPassword`；成功后会退出全部设备。

## 聊天

调用者须已认证并获得持久化策略中的 `chat` 功能授权，`model` 必须配置可用路由。客户端声明 `Accept: text/event-stream` 时返回 SSE：

```bash
curl -N -X POST http://localhost:5111/api/ai/chat \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  -H 'Accept: text/event-stream' \
  -d '{"model":"gpt-4.1-mini","messages":[{"role":"user","content":"用一句话介绍这个服务"}],"stream":true,"vision":false}'
```

```text
data: {"type":"delta","text":"你好","code":null}

data: {"type":"completed","text":null,"code":null}

```

只在收到 `completed` 后标记成功；`error` 或缺少完成事件的 EOF 是中断。输出前失败使用 HTTP ProblemDetails；输出后失败发送 `error` 事件，错误代码为 `ai_unavailable`。旧客户端未声明 SSE 时仍收到纯文本，输出后失败会断开连接。JSON 文本可包含转义换行，客户端必须跨网络分片解码 UTF-8 和事件边界。

请求只包含 `messages`、`model`、`stream`、`vision`。采样、推理和输出参数由供应商使用模型默认值；旧客户端发送的生成参数不再绑定。Anthropic 协议必填的 `max_tokens` 保持 8192 兜底。`GET /api/ai/models` 返回当前可选模型，要求已认证。

## 模型与密钥管理

下列接口均要求 `admin` 角色：

| 目的 | 接口 |
| --- | --- |
| 查询或创建模型 | `GET` / `POST /api/model` |
| 更新、删除或启停模型 | `PUT` / `DELETE /api/model/{modelId}`；`POST /api/model/changeModelStatus/{modelId}` |
| 查询或创建供应商密钥 | `GET` / `POST /api/key` |
| 更新或删除密钥 | `PUT` / `DELETE /api/key/{keyId}` |
| 获取供应商类型编号 | `GET /api/key/types` |
| 刷新内存密钥池 | `POST /api/sync/loadKeys` |

创建密钥时填写 `BaseUrl`、`ApiKey`、`RequestIdentifier` 与启用状态；创建模型时填写逻辑模型名、聊天/视觉能力与启用状态，并通过 `ModelKeyBinds` 将它们关联。每条关联可设置 `ProviderModelValue`（该供应商的实际模型名）、`Priority`（数字越小越优先）、`RoutingWeight`（独立相对权重；0 排除，null 兼容旧行为）与 `Fee`（相对成本系数，仅未配置权重时使用其倒数）。不要在浏览器日志、源码或请求记录中保存 API 密钥。

例如，客户端统一请求 `fast-chat`，但可将该逻辑模型配置为优先调用 OpenAI 的 `gpt-4.1-mini`，失败时再调用另一个供应商的 `gemini-2.5-flash`：

```json
{
  "modelKeyBinds": [
    { "modelId": 12, "providerModelValue": "gpt-4.1-mini", "priority": 0, "fee": 0.4, "enable": true },
    { "modelId": 12, "providerModelValue": "gemini-2.5-flash", "priority": 1, "fee": 0.2, "enable": true }
  ]
}
```

## 其他接口

- `POST /api/media/tts`、`/whisper-translate`、`/whisper-transcription`、`/dall-e`、`/gpt-image`：OpenAI-compatible 媒体能力（分别检查对应功能策略）。
- `POST /api/fileextractor`：提取 PDF、DOCX、TXT、Markdown、CSV、TSV、JSON 和日志文件中的文本。单文件最多 10 MiB。无可提取文本或文件损坏时返回 400，格式不受支持时返回 415。
- `/api/sync/data`：版本化聊天历史、消息与任务同步。`/api/request` 和 `/api/receiver` 保留旧客户端入口，写入也生成变更日志。
- `GET /api/open/model`：获取公开模型列表。

接口的字段以运行中的 Swagger 文档为准；生产环境如需对外发布 OpenAPI，请在反向代理或应用层显式启用并进行访问控制。

## API 响应语言

用户端普通 HTTP 请求与流式聊天请求均传递 `Accept-Language`（`zh`、`en`、`ja`）。界面历史语言值 `jp` 在发送前转换为标准的 `ja`。服务端支持地区标签及语言权重，不支持的语言回退到中文，通过 `Content-Language` 返回实际语言。

应用内的账户、角色、模型管理、密钥池刷新、聊天参数校验、媒体无可用密钥，以及全局异常文案均使用当前语言。全局异常返回 ProblemDetails，包含稳定的 `code: internal_error` 和 `traceId`；重复注册返回 HTTP 409 与 `{code: "account_exists", message: "本地化提示"}`。客户端应读取 `message` 或 `title`，不要直接将响应对象拼接成字符串。供应商原始响应和框架自动生成的字段校验详情保留原文。

### 验证码图像格式

`GET /api/verification/generate` 返回 `{img, code, contentType}`。`img` 仍是 base64 字节，图像格式改为 `image/svg+xml`。前端应根据 `contentType` 构造 data URL；与旧服务端联调时缺省按 `image/jpeg` 处理。SVG 使用客户端字体，后端不再依赖 ImageSharp 或系统字体。

## 邮件设置与重置密码

- `POST /api/auth/password-reset/request`：匿名请求，JSON `{username, email}`，匹配账户后发送设置密码链接。无论是否匹配都返回 HTTP 202 与 `code: reset_requested`。
- `POST /api/auth/password-reset/confirm`：匿名请求，JSON `{token, newPassword, confirmPassword}`，密码须为 8–20 字符且确认一致。成功返回 204，无效/过期/已使用链接返回 400 与 `code: reset_invalid`。
- 未启用邮件服务时两者返回 503 与 `code: reset_unavailable`；超出每 IP 限制返回 429。

邮箱只从已存储的账户读取，发件配置与前端地址只从服务端配置读取。链接 30 分钟有效，成功设置密码后所有原登录会话和之前的重置链接失效。无需登录也可激活 SQL 创建的空密码账户，不新增数据库表；已有数据库无需迁移。应用内中英日错误提示和邮件文案随请求语言切换。

## 版本化数据同步

`GET /api/sync/data?cursor=0` 与 `POST /api/sync/data` 要求 `user` 角色和 `X-Sync-Account: <当前账户 ID>`。请求头必须与认证主体一致，避免账户切换或令牌刷新导致错投。

写操作示例：

```json
{
  "operationId": "11111111-1111-4111-8111-111111111111",
  "kind": "message",
  "action": "upsert",
  "entityId": "200",
  "historyId": 100,
  "payload": {"role": "user", "content": "hello", "finish": true}
}
```

- `kind` 为 `history`、`message` 或 `task`；`action` 为 `upsert` 或 `delete`。历史及消息实体 ID 是安全整数的十进制字符串，任务 ID 最多 255 字符。
- 先创建会话，再提交消息；消息操作需要 `historyId`。历史载荷包含 `title`，消息载荷包含 `role/content/fileList/model/finish`，任务使用原 `task_id/task_type/date/images/videos` 字段。
- 载荷中的账户、数据库主键及导航对象不作为所有权依据；所有权来自认证主体。任务不能跨账户覆盖。
- 写成功返回 `{version}`；相同操作 ID 和相同载荷重试返回原版本。相同 ID 的不同载荷返回 409；消息所属会话不存在也返回 409。网络重试必须保留原操作 ID。
- 拉取返回 `{cursor, reset, hasMore, changes}`。每项包含 `version/kind/action/entityId/historyId/payload`；删除项载荷为空。游标为 0 时返回完整当前快照和 `reset:true`，随后每批最多 200 条事件。
- 数据、版本、删除事件和操作幂等记录在同一账户事务内提交；客户端也必须在本地事务提交数据和游标后再读取下一页。冲突采用服务端接收顺序的最后写入，不提供字段级合并。

首次升级导入、旧记录删除歧义、保留策略及上线步骤见 [架构实施记录](architecture-review.md)。

## 请求与响应契约

请求与响应模型集中于 `Contracts/`，不直接公开数据库关系或供应商 SDK 类型。字段命名保持现有 JSON 协议；数据注解校验失败由 ApiController 提前返回 400 ValidationProblemDetails。字段白名单、ID 校验、SDK 返回值映射和兼容限制见 [Controller 契约与官方 SDK](api-contracts-and-sdks.md)。

## 注册邮箱验证码

先调用 `POST /api/auth/register/email-code`，只提交 email；收到邮件后在 `POST /api/auth/register` 中提交 emailCode 与原有注册字段。验证码不通过时不会创建账户。响应、冷却与部署要求见 [注册邮箱验证](registration-email-verification.md)。

路由配置与升级示例见 [模型目录与路由权重](model-catalog-and-routing.md)。
