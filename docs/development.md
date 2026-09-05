# 开发与测试

## 本地开发

1. 安装 `global.json` 指定的 .NET SDK。
2. 从 `src/Turbo.Auth/appsettings.example.json` 创建 `appsettings.Development.json`，并填写本地 MySQL、JWT 和 CORS 配置。
3. 创建空 MySQL 数据库，执行 `src/Turbo.Auth/Resources/merge/init.sql` 和 `open-initdata.sql`。
4. 恢复、构建并启动：

```bash
dotnet restore
dotnet build Turbo.AI.Server.sln --configuration Release --no-restore
dotnet run --project src/Turbo.Auth/Turbo.Auth.csproj --launch-profile http
```

开发环境会启用 Swagger，可在服务监听地址的 `/swagger` 检查接口定义。

## 自动化测试

```bash
dotnet test Turbo.AI.Server.sln --configuration Release --nologo
```

测试项目是 `Turbo.Auth.Tests`，覆盖文档提取、账户密码处理，以及模型路由的快照发布、供应商模型映射、优先级与熔断逻辑。测试应使用临时文件与本地 fixture，不能依赖开发者机器中的绝对路径、真实数据库或供应商密钥。

## 前端联调

用户端与管理端已合并至 `turbo-user`。本地运行服务端的 `http` 启动配置时，API 默认监听 `http://localhost:5111`；前端的 `src/environments/environment.ts` 应使用 `http://localhost:5111/`。若调整服务端调试端口，同步修改该环境文件，并将 Angular 开发服务器地址加入 `Cors:AllowedOrigins`。

生产构建使用 `src/environments/environment.prod.ts` 中的 `apiUrl: '/'`。将前端构建产物部署到本服务的 `wwwroot` 根目录，即可使用同源请求；不再使用 `assets/config.json`、`/ai` 或独立的 `/admin` 站点。联调时至少验证登录、模型读取、流式聊天、管理路由和密钥池刷新。
