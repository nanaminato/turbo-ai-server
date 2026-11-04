# Third-party notices

## PdfPig 0.1.12

`Turbo.Kit` uses [PdfPig](https://github.com/UglyToad/PdfPig) to extract PDF text. PdfPig is distributed under the Apache License, Version 2.0. The package replaces the legacy `iTextSharp` dependency, whose .NET Framework-only restore assets generated a `NU1701` compatibility warning for this .NET 10 project.

The previous NPOI dependency was removed. DOCX text extraction now uses the Open XML package format (`ZipArchive` and `XDocument`) provided by .NET, so the project no longer needs to accept NPOI's OSMF EULA.

## Captcha rendering

The ImageSharp, ImageSharp.Drawing and Fonts dependencies have been removed. The verification endpoint generates a self-contained SVG using .NET XML APIs, with no server font installation or graphics license required. Its JSON response retains `img` (base64-encoded bytes) and `code`, and adds `contentType: image/svg+xml`. Clients must use the returned content type when constructing the image data URL.

## Official AI clients

The backend now uses [OpenAI 2.14.0](https://github.com/openai/openai-dotnet) (MIT), [Anthropic 13.4.0](https://github.com/anthropics/anthropic-sdk-csharp) (Apache-2.0), and [Google.GenAI 1.28.0](https://github.com/googleapis/dotnet-genai) (Apache-2.0). Their license notices remain with the NuGet packages. The tryAGI.OpenAI, Anthropic.SDK, DotnetGeminiSDK and DashScope dependencies have been removed. Alibaba chat uses the official OpenAI client's compatible protocol.

Swagger's Newtonsoft adapter uses Swashbuckle.AspNetCore.Newtonsoft 10.2.3 (MIT), matching the existing Swashbuckle version. See [migration and compatibility notes](docs/api-contracts-and-sdks.md).
