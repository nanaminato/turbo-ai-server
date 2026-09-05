# Turbo AI Server

`Turbo AI Server` 是面向 AI 应用的 ASP.NET Core 后端。它提供账户与角色鉴权、供应商密钥和模型管理、流式聊天、媒体生成，以及聊天记录同步 API。

## 文档

- [部署与配置](deploy.md)：本地启动、生产配置和前端部署。
- [架构](Architecture.md)：组件职责、请求路径和扩展点。
- [API 使用](WhatCanIDo.md)：认证、聊天和管理接口。
- [开发与测试](test.md)：构建、测试和数据库准备。
- [技术栈与依赖](technoligy.md)：运行时、存储和供应商 SDK。

## 快速启动

先准备 .NET SDK 10、MySQL 8.4+，并创建本地配置：

```bash
cp Turbo-Auth/appsettings.example.json Turbo-Auth/appsettings.Development.json
```

编辑 `appsettings.Development.json` 中的 `ConnectionStrings:ciko`、JWT 和 `Cors:AllowedOrigins`。随后初始化数据库并启动服务：

```bash
dotnet restore
dotnet run --project Turbo-Auth --launch-profile http
```

默认示例配置监听 `http://0.0.0.0:6000`。开发环境可通过 `/swagger` 查看由控制器生成的接口定义。

首次启动后，使用管理员接口配置供应商密钥、模型以及密钥与模型的关联；已启用的关联会在启动时加载到内存密钥池。详见 [部署与配置](deploy.md) 和 [API 使用](WhatCanIDo.md)。
