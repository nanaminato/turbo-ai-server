SQL脚本使用指南  
首先根据appsettings.Development.json
,appsettings.Production.json的内容在对应的操作系统上创建
MySqL数据库。（你可以修改这些内容，只需要你的数据库和这个配置契合。）  
简要来说Resources目录下除了 merge文件夹，其余的文件夹下的脚本主要用于
本地测试环境（windows下MySQL不区分大小写）。  
你可以使用 merge/init.sql 在空数据库中创建当前应用所需的全部数据表。
然后使用merge/open-initdata.sql来填充一些数据，这是必须的。这将主要添加一些模型和身份。
推荐纯 SQL 初始化：先导入 `merge/init.sql` 和 `merge/open-initdata.sql`，再填写并导入 `account/create-admin.sql` 的管理员用户名及真实邮箱。账户初始空密码不可登录。正常部署应用、配置 PasswordReset SMTP 后，从登录页“忘记密码”发送邮件并设置初始密码，不用运行 .NET 初始化命令或生成哈希。
已有管理员直接使用其用户名与可用邮箱申请重置，脚本不会覆盖既有账户。重置功能默认关闭，详细配置见 `docs/deployment.md`。原有 `--init-db` / `--create-admin` 保留为不使用邮件时的可选方式。
升级到删除 Models.Enable 的版本时，请先备份并只执行一次 `merge/upgrade-drop-models-enable.sql`。
既然到这了，你需要注意下面的东西

在开发环境和产品环境，Jwt用于验证用户的身份，由于项目是开源的，如果你不
修改SecretKey，不怀好意的人可以通过公开的默认的这个SecretKey来生成能通过验证的
Token。  

```
"Jwt": {
    "Issuer": "IAMHERE",
    "Audience": "Ciko",
    "SecretKey": "ffriewoougewinlewknr;jr329ouoeuoieyouneededit"
  },
```

访问令牌默认仅有效 15 分钟；刷新令牌默认有效 30 天，服务端仅保存其带 pepper 的 HMAC 哈希，并在每次刷新时轮换。改密码、账户或角色权限变更会更新账户安全版本并撤销全部设备会话。


架构升级还需要 `merge/architecture-upgrade.sql`：新增版本化同步表、唯一索引及路由失效触发器。已有数据库先停止写入、备份和检查重复 ID，再按当前结构执行一次；部分迁移不可盲目重跑。普通 Web 启动只读校验这些结构，不会自动修改数据库。详细步骤见 `docs/deployment.md` 和 `docs/startup-repair.md`。

公开注册邮箱验证新增 `merge/registration-email-verification.sql`；已有库执行一次，新库已包含于 init.sql。它只新增验证码摘要表，不改已有账户。注册默认必须验证邮箱；SMTP 与开关见 `docs/registration-email-verification.md`。
