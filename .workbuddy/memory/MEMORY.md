# Turbo AI Server 项目记忆

## 项目概述
- ASP.NET Core 10 + EF Core + MySQL + Redis 的多供应商 AI 路由服务。
- 主要模块：`src/Turbo.Auth`（应用主体）、`src/Turbo.Kit`（PDF/Word/Text 文档处理）、`tests/Turbo.Auth.Tests`。

## 模型与 SDK 版本约定（`docs/technology.md`）
- AI SDK：固定 NuGet 版本，不要自动采用预览包：
  - `tryAGI.OpenAI` 4.2.10（2026-09-05 起替代 Betalgo）
  - `DotnetGeminiSDK` 2.0.0
  - `Anthropic.SDK` 5.10.0
  - `DashScope` 0.9.4
- ImageSharp 4.x / Drawing 3.x 因许可证问题，保持 3.x/2.x。

## Chat 路由架构
- 入口 `Controllers/AI/Chat/ChatController` → `IChatHandlerObtain`（按 `HandlerType` 枚举分发）。
- 提供商处理类均位于 `Application/Chat/*ChatHandler.cs`。
- 路由表存于 `QuickModel` 单例；权重选择由 `IRouteHealthTracker` + `WeightRandom` 完成。
- `OpenAiChatHandler` / `GoogleChatHandler` 均需要 `IHttpClientFactory` 注入的 `AiProvider` 客户端（5 分钟超时）。

## SDK 已知能力缺口（绕过方式）
- **tryAGI.OpenAI 4.2.10**（替代 Betalgo）：GPT-5 / o-series 全功能原生支持；Variant2 上**没有** `Temperature / TopP` 属性，非推理模型走 `AdditionalProperties["temperature" / "top_p"]` 兜底（标记 `[JsonExtensionData]`）。
  - `BetaVerbosity` / `BetaReasoningEffort` 类只是占位 wrapper（仅 `AdditionalProperties`），不能接受枚举值；要用 `VerbosityEnum` / `ReasoningEffortEnum`。
  - `ChatCompletionRequestUserMessageContentPart` 是 `partial struct`，构造走 `new(ChatCompletionRequestMessageContentPartText/Image)`，`FromString` 写死 "describe the following image"，**没有** `FromText/FromImageUrl`。
  - Vision 完整构造：`new ChatCompletionRequestUserMessage(new OneOf<string, IList<...ContentPart>>(parts), role, name: null)`。
  - 自定义 voice：`VoiceIdsShared.FromVoiceIdsSharedVariant1(s)` + `op_Implicit -> VoiceIdsOrCustomVoice`；**没有** `VoiceIdsOrCustomVoice.FromUnchecked`。
  - 第三方代理走 query string `?key=`（Google 兼容）。
- **DotnetGeminiSDK 2.0.0** 不支持 `thinkingBudget`、`responseMimeType`、`systemInstruction` 字段；Gemini 2.5 必须绕开 SDK 走 HTTP（`streamGenerateContent?alt=sse` + `x-goog-api-key` header）。
- 当 BaseUrl 为第三方代理（非 googleapis.com / vertexai）时，key 走 query string `?key=`。

## 数据库
- 初始化 SQL：`src/Turbo.Auth/Resources/merge/` 下的 `init.sql`（建表）、`open-initdata.sql`（默认模型清单，2026-09-05 之后包含 GPT-5/GPT-5.1/GPT-5.2 + Gemini 2.5/2.0 系列）、`select.sql`、`Resources/key/init.sql`、`Resources/sync/init.sql`。

## 构建与开发命令
- `dotnet restore` + `dotnet build src/Turbo.Auth/Turbo.Auth.csproj --no-restore`。
- 沙箱环境 dotnet SDK 10.0.400 在执行 `dotnet restore` 时可能因 `NuGet.targets(782,5): error : Value cannot be null.` 失败，属环境问题，与代码无关；可以用 `--no-restore` 复用 `obj/project.assets.json` 跳过。
- **沙箱 build 完整 workaround**：`dotnet build <project.csproj> --no-restore --no-dependencies /p:GenerateDependencyFile=false /p:GenerateRuntimeConfigurationFiles=false` ——跳过 `GenerateDepsFile` / `GenerateRuntimeConfigurationFiles` 两个读 assets.json 的 task。
- 沙箱环境**反射被禁用**（PowerShell `Add-Type` 拒绝；`Assembly.LoadFrom` 也被拦）；查 tryAGI 真实 API 的最佳方式是 `grep "C:\Users\Administrator\.nuget\packages\tryagi.openai\4.2.10\lib\net10.0\tryAGI.OpenAI.xml"`，里面有完整 XML doc。

## 代码风格
- 使用 Newtonsoft.Json 进行 JSON 序列化（项目标准）。
- 请求体模型使用 `[JsonProperty]` 显式映射字段名。
- C# nullable enabled，message.Content 用 `dynamic`（来自旧 OpenAI 兼容）。