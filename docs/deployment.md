# 部署与配置

## 前置条件

- .NET SDK 10（版本由 `global.json` 固定）。
- MySQL 8.4 或兼容版本。
- 生产环境可选 Redis；未设置 `ConnectionStrings:Redis` 时服务使用进程内缓存。

## 数据库初始化

创建空数据库后，按顺序执行以下脚本：

```text
src/Turbo.Auth/Resources/merge/init.sql
src/Turbo.Auth/Resources/merge/open-initdata.sql
```

初始化脚本仅面向空数据库，已包含当前应用所需的全部表、列和索引。现有数据库不提供增量升级脚本；请先备份数据，再重新创建数据库并按上述顺序初始化。

## 配置服务

以 `src/Turbo.Auth/appsettings.example.json` 为模板创建环境专用配置。不要提交该文件或真实凭据。生产环境推荐使用环境变量或密钥存储：

```bash
export ConnectionStrings__ciko='server=db;port=3306;database=turboai;user=turboai;password=replace-me;Charset=utf8mb4'
export ConnectionStrings__Redis='redis:6379'
export Jwt__Issuer='turbo-ai-server'
export Jwt__Audience='turbo-ai-client'
export Jwt__SecretKey='a-long-random-secret'
export Cors__AllowedOrigins__0='https://app.example.com'
export Cors__AllowedOrigins__1='https://admin.example.com'
```

必须配置 `ConnectionStrings:ciko`、`Jwt:Issuer`、`Jwt:Audience` 与 `Jwt:SecretKey`；缺少这些值时应用会拒绝启动。`Cors:AllowedOrigins` 需要填写前端实际来源（协议、域名和端口）。`Diagnostics:EnableSensitiveDataLogging` 仅能在本地开发时临时设为 `true`。

`AiRouting:FailureThreshold` 指连续失败多少次后短暂熔断某一路由，`AiRouting:BreakDurationSeconds` 指熔断持续时间。默认值为 3 次和 60 秒；健康状态仅保存在进程内，重启后清空。

## 构建与运行

```bash
dotnet restore
dotnet publish src/Turbo.Auth/Turbo.Auth.csproj --configuration Release --output ./publish
ASPNETCORE_ENVIRONMENT=Production dotnet ./publish/Turbo.Auth.dll
```

示例配置中的 Kestrel 监听 `0.0.0.0:6000`。在生产环境使用反向代理终止 TLS，并将代理来源加入 CORS 白名单。Swagger 仅在 `Development` 环境注册，不应默认公开到生产网络。

## 部署前端

用户端和管理端为独立项目：

- 用户端：[turbo-user](https://github.com/nanaminato/turbo-user)
- 管理端：[turboai-admin](https://github.com/nanaminato/turboai-admin)

在各自项目中构建后，将产物分别放到：

```text
src/Turbo.Auth/wwwroot/ai
src/Turbo.Auth/wwwroot/admin
```

服务会把 `/ai/*` 和 `/admin/*` 回退到对应的单页应用入口。若前端和后端分开部署，在前端的 `assets/config.json` 中将 `apiUrl` 设置为后端公开地址；同源部署时使用空值。

## 配置模型和密钥

1. 使用管理员账户登录，取得 JWT。
2. 通过 `GET /api/key/types` 选择供应商类型编号。
3. 创建启用的供应商密钥和模型，并添加它们的关联。
4. 调用 `POST /api/sync/loadKeys`，使更改立即进入内存密钥池。

详见 [API 使用](api.md)。
