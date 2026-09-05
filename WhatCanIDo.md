# API 使用

开发环境启动服务后可在 `/swagger` 浏览完整的请求模型和响应。以下是最常用的调用路径。

## 认证

注册和登录均使用 JSON 请求体：

```bash
curl -X POST http://localhost:6000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"developer","password":"change-me"}'
```

成功响应包含 `token` 和 `id`。在受保护接口中传入令牌：

```text
Authorization: Bearer <token>
```

`POST /api/auth/register` 接收 `email`、`username`、`password` 和 `confirm`。管理员也可以通过 `/api/account` 管理账户和角色。

## 聊天

调用者须具有 `vip` 角色，且 `model` 必须已在管理员端启用并绑定到可用密钥。服务以流式文本响应：

```bash
curl -N -X POST http://localhost:6000/api/ai/chat \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  -d '{
    "model":"gpt-4.1-mini",
    "messages":[{"role":"user","content":"用一句话介绍这个服务"}],
    "stream":true,
    "max_completion_tokens":256,
    "temperature":0.7
  }'
```

常用字段包括 `messages`、`model`、`max_completion_tokens`、`temperature`、`top_p`、`presence_penalty`、`frequency_penalty` 与 `vision`。`GET /api/ai/models`（`user` 角色）返回当前可选模型。

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

创建密钥时填写 `BaseUrl`、`ApiKey`、`RequestIdentifier` 与启用状态；创建模型时填写展示名、上游模型标识、聊天/视觉能力与启用状态，并通过 `ModelKeyBinds` 将它们关联。不要在浏览器日志、源码或请求记录中保存 API 密钥。

## 其他接口

- `POST /api/media/tts`、`/whisper-translate`、`/whisper-transcription`、`/dall-e`、`/gpt-image`：OpenAI-compatible 媒体能力（`vip`）。
- `POST /api/fileextractor`：提取 TXT、DOCX、PDF 内容。
- `/api/request` 和 `/api/receiver`：聊天历史、消息与任务同步。
- `GET /api/open/model`：获取公开模型列表。

接口的字段以运行中的 Swagger 文档为准；生产环境如需对外发布 OpenAPI，请在反向代理或应用层显式启用并进行访问控制。
