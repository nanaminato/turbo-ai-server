# Controller 契约与官方 SDK 迁移

实施日期：2026-11-04。

## 选型

优先选择供应商官方维护、支持当前 .NET 10、可注入 HttpClient 且有类型化流接口的稳定版。流行度没有可靠的统一排名，因此不以下载量代替维护和兼容性。以下版本在实施日通过 NuGet 稳定版本索引核验并固定在项目文件中。

| 提供商 | 原依赖 | 当前依赖 | 调用方式 |
| --- | --- | --- | --- |
| OpenAI 及兼容供应商 | tryAGI.OpenAI 4.2.10 | OpenAI 2.14.0 | 官方 Chat、Audio、Images 客户端 |
| Anthropic | Anthropic.SDK 5.10.0 | Anthropic 13.4.0 | 官方 Messages 流 |
| Gemini | DotnetGeminiSDK 2.0.0 | Google.GenAI 1.28.0 | 官方 GenerateContent 流 |
| Alibaba / Qwen | DashScope 0.9.4 | 复用 OpenAI 2.14.0 | 阿里云官方支持的 OpenAI-compatible 协议 |

依据：[OpenAI 官方 C# SDK](https://developers.openai.com/api/docs/libraries)、[Anthropic 官方 C# SDK](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp)、[Google 官方 SDK 清单](https://ai.google.dev/gemini-api/docs/libraries)、[Google .NET SDK 配置](https://googleapis.github.io/dotnet-genai/)、[阿里云兼容接口说明](https://www.alibabacloud.com/help/en/model-studio/compatibility-of-openai-with-dashscope)。版本核验源分别为 NuGet flatcontainer 的 openai / anthropic / google.genai 索引。

不改变数据库中的逻辑模型、模型别名、供应商编号或优先级。SDK 升级不等于自动选用更贵的模型，也没有引入额外的编排框架。

## Controller 边界

新增 `src/Turbo.Auth/Contracts/`，按 Auth、Catalog、Chat、Media、Sync、Files、FeatureAccess 和 Common 分组。请求、响应与 EF 实体、供应商 SDK 模型分离，返回数据的端点使用 `ActionResult<T>`；流式、空响应和文件代理保留相应 HTTP 类型。

- 认证请求增加必填、长度、邮箱和确认密码验证；登录允许已有密码长度，创建账户和修改密码保留各自规则。账户响应不包含密码或 EF 角色导航关系。
- 管理接口只映射允许修改的字段。更新使用路由 ID，拒绝非零且不一致的请求体 ID；创建模型、密钥和账户拒绝指定已有 ID。密钥关联只接收 modelId、启用状态、费用、别名和优先级，不绑定客户端嵌套的 Model / SupplierKey 图。管理密钥响应仍返回 apiKey，权限仍为 admin。
- 聊天内容明确为字符串或文本/图片分段，移除 dynamic；系统、developer 和助手角色由适配器显式映射。空消息集合、未知角色、非法分段和无效图片 URI 返回 400 ValidationProblemDetails。图片 URL 接受 HTTP(S) 或 image base64 data URL；上游仍负责图片内容的实际解码和格式支持。
- 媒体输入校验 base64、语速、温度、数量和格式；控制器不再创建 SDK 客户端。MediaService 统一处理模型路由与 SDK 返回值映射，保留语音 base64、转录 text、图像 data/b64_json/revised_prompt 等外部字段。
- 同步写入忽略客户端账户及数据库关系字段，由登录身份决定账户；旧入口也使用独立契约。增量同步的 payload 仍为按 kind 区分的 JSON 对象，服务只读取指定字段，拒绝非对象或无法反序列化的载荷。同步响应不返回 EF 导航对象。
- 文件提取使用独立请求；临时文件使用随机名称和安全扩展名，并在 finally 中删除，不再永久写入上传目录。
- 集合验证拒绝 null 项；功能策略也验证嵌套角色配额。校验特性按 ASP.NET Core record 构造参数规则声明，避免运行时元数据异常。
- Swagger 使用与 MVC 相同的 Newtonsoft 命名规则，聊天 content 文档为 string/array 的 oneOf；SDK 模型与数据库关系不再进入 API schema。

保留既有端点、camelCase 字段和明确标记的 snake_case 字段。不添加统一成功响应外壳，已有前端服务无需修改。无效输入现在可能更早收到结构化 400；更新/创建接口的 ID 校验更严格。数据库关系 ID 和导航对象不再作为受支持的返回字段；第三方消费者若依赖这些实现细节，需要调整。

## SDK 与流行为

所有适配器使用命名 `AiProvider` HttpClient、配置的 BaseUrl、路由后的真实模型名及请求取消令牌。OpenAI / Anthropic 禁用内部重试，Google 设置一次尝试，首段输出前的切换继续由 ChatService 控制，防止 SDK 与业务路由叠加重试。Anthropic 使用系统指令字段；其 max_tokens=8192 是协议所需的既有默认值，不额外注入采样参数。

OpenAI 和 Alibaba 检查 finish_reason；Anthropic 检查 message_stop；Gemini 检查候选 finishReason。提前 EOF 作为失败。真实 Kestrel 测试发现并修复了多段输出重复设置响应头的问题，避免第二段/终止事件导致 SSE 或纯文本连接提前断开。

Alibaba 默认使用 `https://dashscope.aliyuncs.com/compatible-mode/v1`。已有阿里云根地址或 `/api/v1` 改为同一域名下 `/compatible-mode/v1`，保留国内/国际区域；自定义代理及工作空间地址保持配置路径。原生 DashScope 专属功能、Qwen-Audio 等不兼容 OpenAI 协议的模型不在此聊天适配器范围内，需要单独实现。上线前应核对已有 Qwen 模型对兼容协议的支持。

OpenAI 2.14.0 稳定包仍将 developer 和部分 GPT Image 选项标记为 experimental。仅在对应适配器局部抑制 OPENAI001，保留现有功能并由回归测试覆盖；没有全局关闭诊断。后续 SDK 更新需要复验这些 API。

## 验证

- Release 构建通过；现有与新增常规测试共 137 项通过。
- 新增真实 HTTP 契约测试：字符串/图片消息、非法类型、null 集合项、嵌套策略、ID 不匹配、关系图忽略、SSE/纯文本多段输出以及 Swagger 字段。
- 新增官方 SDK 的模拟 HTTP 测试：四种供应商流、system/vision/实际模型名、一次尝试、Alibaba 区域与代理路径、语音二进制、JSON/text/SRT 转录及图像字节/元数据。
- 专用测试 MySQL 的并发同步、路由触发器与旧库升级 10 项回归全部通过，仅使用隔离临时库。
- NuGet 已知漏洞检查包括传递依赖，实施日未报告已知漏洞。
- 未使用真实供应商密钥发起计费调用，实际供应商模型权限、配额及网络仍需部署环境验证。

复现常规验证：

```powershell
dotnet restore Turbo.AI.Server.sln
dotnet build Turbo.AI.Server.sln --configuration Release --no-restore
dotnet test tests/Turbo.Auth.Tests/Turbo.Auth.Tests.csproj --configuration Release --no-build --results-directory ./TestResults
dotnet list Turbo.AI.Server.sln package --vulnerable --include-transitive
```

本次不涉及数据库结构修改。独立测试库的并发、触发器和升级脚本回归另见 [MySQL 验证](mysql-verification.md)。本地连接配置继续忽略，不写入文档。
