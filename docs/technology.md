# 技术栈与依赖

| 范围 | 技术 |
| --- | --- |
| 运行时 | .NET 10、ASP.NET Core、Entity Framework Core |
| 数据与缓存 | MySQL、Redis（可选） |
| 身份与 API | JWT Bearer、Swagger / OpenAPI、Newtonsoft.Json |
| 文档提取 | PdfPig、.NET Open XML/ZIP 处理 |
| 验证码 | .NET XML 生成自包含 SVG |
| AI 官方 SDK | OpenAI 2.14.0、Anthropic 13.4.0、Google.GenAI 1.28.0 |
| 阿里云聊天 | 使用 OpenAI SDK 对接 DashScope 兼容接口 |
| 测试 | NUnit、Microsoft.NET.Test.Sdk、coverlet、SQLite / 专用 MySQL |

SDK 版本于 2026-11-04 核验为 NuGet 最新稳定版并显式固定；不自动采用预览包。具体迁移、官方依据和兼容范围见 [Controller 契约与 SDK](api-contracts-and-sdks.md)。Swagger 增加同版本的 Newtonsoft 支持，使文档字段与实际请求保持一致。

更新依赖后执行：

```bash
dotnet restore
dotnet build Turbo.AI.Server.sln --configuration Release --no-restore
dotnet test Turbo.AI.Server.sln --configuration Release --no-build
dotnet list Turbo.AI.Server.sln package --vulnerable --include-transitive
```

完整的第三方归属见 [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)。
