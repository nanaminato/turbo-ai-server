# 开发与测试

## 本地开发

1. 安装 `global.json` 指定的 .NET SDK。
2. 从 `src/Turbo.Auth/appsettings.example.json` 创建 `appsettings.Development.json`，并填写本地 MySQL、JWT 和 CORS 配置。
3. 导入 `merge/init.sql`、`merge/open-initdata.sql`，填写并导入 `account/create-admin.sql` 的真实用户名和邮箱。配置邮件服务后，从登录页申请邮件设置初始密码；具体见部署文档。
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

## 多语言与验证码回归

后端测试覆盖语言协商（地区、权重及回退）、中英日聊天参数校验响应与 SVG 验证码结构/内容。不需要系统字体或图像库许可证。

前端在 `turbo-user` 运行 `npm run test:i18n` 检查三语言键集合、插值参数及代码/模板引用，再运行 `npm test -- --watch=false --browsers=ChromeHeadless`。`npm run build` 验证生产预算；Mermaid 从全局脚本迁移为聊天组件按需加载，不能重新加入 `angular.json` 的全局脚本。

## 初始化命令

`--init-db` / `--create-admin` 在 Web 主机构建前执行，不读取 JWT 配置或启动密钥池加载。数据库连接可交互输入，或从 `ConnectionStrings__ciko` 获取。测试覆盖数据库名校验、管理员输入校验、凭据诊断隐藏及嵌入式 SQL，且不需要真实数据库。

部署联调时还应验证完整初始化、角色绑定、密码哈希校验，以及重复命令拒绝覆盖现有数据。不要将数据库凭据或管理员初始密码写入仓库。

## 邮件重置测试

常规测试使用临时 Data Protection provider、模拟邮件发送器和内存存储，覆盖首次设置密码、令牌篡改/过期/一次使用、邮箱匹配、发送冷却和禁用状态，不发送真实邮件。

`PasswordResetDatabaseTests` 是显式运行的 MySQL 集成测试：在临时环境变量 `TURBO_TEST_MYSQL` 设置有建库权限的测试连接，用 `dotnet test --filter FullyQualifiedName~Sql_admin_activation_revokes_sessions_and_password_reset_is_atomic` 执行。测试只在自己生成的 `turbo_reset_test_<guid>` 库运行，结束删除该库，验证 SQL 管理员初始化、密码哈希、会话撤销、并发重置和重复脚本保护。不要将连接凭据写进仓库。

## 架构回归与数据库联调

架构实施与上线边界见 [审查记录](architecture-review.md)。普通测试新增真实 SQLite 事务、供应商模拟 SSE、IndexedDB 对应前端回归和双实例版本刷新测试，不调用真实供应商。

MySQL 并发、触发器及旧库升级需要单独运行显式集成用例。配置进程环境变量 `TURBO_TEST_MYSQL` 为专用测试连接，运行：

```bash
dotnet test tests/Turbo.Auth.Tests/Turbo.Auth.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitectureDatabaseTests
```

也可传入被 Git 忽略的 `--settings tests/mysql.runsettings.user`，其中提供测试环境变量。使用单独类过滤器，确认结果显示全部 10 项执行，避免普通/显式测试的混合 OR 过滤器忽略显式用例。

测试仅创建并清理 `turbo_arch_test_<guid>` 临时库，验证并发幂等操作、连续游标、分页、账户锁、12 个路由触发器、双实例刷新、事务回滚与真实旧版本升级。旧库基线固定为 e01436d；升级脚本直接嵌入实际部署资源。不得用生产数据库替代隔离测试库。

2026-11-04 在 MySQL 8.4.13 上 10 项全部通过，并补充了 EF 跟踪版本缓存修复；详见 [MySQL 验证记录](mysql-verification.md)。普通 Release 回归 100 项通过。
