# 开发与测试

## 本地开发

1. 安装 `global.json` 指定的 .NET SDK。
2. 从 `Turbo-Auth/appsettings.example.json` 创建 `appsettings.Development.json`，并填写本地 MySQL、JWT 和 CORS 配置。
3. 创建空 MySQL 数据库，执行 `Turbo-Auth/Resources/merge/init.sql` 和 `open-initdata.sql`。
4. 恢复、构建并启动：

```bash
dotnet restore
dotnet build Turbo-Auth.sln --configuration Release --no-restore
dotnet run --project Turbo-Auth --launch-profile http
```

开发环境会启用 Swagger，可在服务监听地址的 `/swagger` 检查接口定义。

## 自动化测试

```bash
dotnet test Turbo-Auth.sln --configuration Release --nologo
```

测试项目是 `Turbo-Kit-Test`，覆盖文档提取、账户密码处理，以及模型路由的快照发布、供应商模型映射、优先级与熔断逻辑。测试应使用临时文件与本地 fixture，不能依赖开发者机器中的绝对路径、真实数据库或供应商密钥。

## 前端联调

用户端与管理端分别在其仓库构建。若它们不与服务同源部署，在各自 `assets/config.json` 中设置：

```json
{
  "apiUrl": "https://api.example.com/"
}
```

该地址必须同时出现在后端 `Cors:AllowedOrigins` 中。联调时至少验证登录、模型读取、流式聊天和密钥池刷新。
