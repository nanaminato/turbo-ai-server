# 技术栈与依赖

| 范围 | 技术 |
| --- | --- |
| 运行时 | .NET 10、ASP.NET Core、Entity Framework Core |
| 数据与缓存 | MySQL、Redis（可选） |
| 身份与 API | JWT Bearer、Swagger / OpenAPI、SignalR、Newtonsoft.Json |
| 文档提取 | PdfPig、.NET Open XML/ZIP 处理 |
| 图像处理 | SixLabors ImageSharp、ImageSharp.Drawing |
| AI 提供商 SDK | tryAGI.OpenAI、DotnetGeminiSDK、Anthropic.SDK、DashScope |
| 测试 | NUnit、Microsoft.NET.Test.Sdk、coverlet |

AI SDK 的稳定版本由项目文件显式固定。当前 OpenAI、Gemini、Anthropic 和 DashScope SDK 已是 NuGet 可用的最新稳定版本；不要自动采用预览包。ImageSharp 4.x / Drawing 3.x 需要额外的 Six Labors 许可证，因此项目保留可构建的兼容版本。更新依赖后执行：

```bash
dotnet restore
dotnet test Turbo.AI.Server.sln --configuration Release --nologo
```

完整的第三方归属见 [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)。
