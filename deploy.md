# 部署

## 安全配置（必需）

仓库中的 `Turbo-Auth/appsettings.json` 不包含可运行凭据。部署时请通过环境专用配置、密钥存储或环境变量提供以下值，且不要将真实值提交到仓库：

```bash
ConnectionStrings__ciko='server=...;port=3306;database=...;user=...;password=...;Charset=utf8mb4'
ConnectionStrings__Redis='redis-host:6379'
Jwt__Issuer='turbo-ai-server'
Jwt__Audience='turbo-ai-client'
Jwt__SecretKey='a-long-random-secret'
Cors__AllowedOrigins__0='https://app.example.com'
Cors__AllowedOrigins__1='https://admin.example.com'
```

生产环境必须列出精确的前端来源；未配置来源时，服务不会允许任何跨域请求。`Diagnostics:EnableSensitiveDataLogging` 仅可在本地排障时临时启用，生产环境必须保持 `false`。如历史配置曾包含真实凭据，请在部署前于对应服务中完成轮换。

密码哈希上线前，先在已备份的数据库上执行 `Turbo-Auth/Resources/account/upgrade-password-column.sql`。应用会在旧明文密码首次成功登录时自动升级为哈希；在所有活跃账户完成迁移前，请保留数据库备份和回滚窗口。

## 服务端

在部署和测试之前：

1. 观察并复制 `Turbo-Auth/appsettings.example.json`，创建环境专用配置。
2. 创建对应的数据库。
3. 执行 `Turbo-Auth/Resources/merge` 下的初始化数据脚本：`init.sql` 和 `open-initdata.sql`。

数据库准备完毕后，在 Visual Studio 或命令行执行发布构建，再启动服务。Linux 服务器应通过反向代理终止 TLS 并按实际环境配置路由。

## 用户端与管理端

- 用户端：https://github.com/nanaminato/turbo-user
- 管理端：https://github.com/nanaminato/turboai-admin

在对应前端项目根目录执行：

```bash
ng build
```

将 `turbo-user` 的构建产物放到 `Turbo-Auth/wwwroot/ai`，将 `turboai-admin` 的构建产物放到 `Turbo-Auth/wwwroot/admin`。部署后访问：

- `host:8000/ai`：用户端
- `host:8000/admin`：管理端

根路径默认没有页面；如需跳转，可在 `wwwroot` 添加 `index.html`。
