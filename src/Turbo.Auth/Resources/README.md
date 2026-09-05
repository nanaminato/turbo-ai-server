SQL脚本使用指南  
首先根据appsettings.Development.json
,appsettings.Production.json的内容在对应的操作系统上创建
MySqL数据库。（你可以修改这些内容，只需要你的数据库和这个配置契合。）  
简要来说Resources目录下除了 merge文件夹，其余的文件夹下的脚本主要用于
本地测试环境（windows下MySQL不区分大小写）。  
你可以使用 merge/init.sql 在空数据库中创建当前应用所需的全部数据表。
然后使用merge/open-initdata.sql来填充一些数据，这是必须的。这将主要添加一些模型和身份。
初始化不会创建默认账户。需要通过 SQL 创建用户时，先在交互式终端运行 `dotnet run --project src/Turbo.Auth/Turbo.Auth.csproj -- --hash-password` 生成密码哈希，再将哈希填入 `account/input.sql` 并执行；首个管理员还需按该文件末尾的注释分配 `admin` 和 `vip` 角色。已有数据库升级登录体系时，请先备份并只执行一次 `account/upgrade-auth-sessions.sql`。
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

