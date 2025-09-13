SQL脚本使用指南  
首先根据appsettings.Development.json
,appsettings.Production.json的内容在对应的操作系统上创建
MySqL数据库。（你可以修改这些内容，只需要你的数据库和这个配置契合。）  
简要来说Resources目录下除了 merge文件夹，其余的文件夹下的脚本主要用于
本地测试环境（windows下MySQL不区分大小写）。  
你可以使用 merge/init.sql 来创建数据表。  
然后使用merge/open-initdata.sql来填充一些数据，这是必须的。这将主要添加一些模型和身份，同时添加一个
具备 “管理员，用户，和会员”的niko用户。  
你可以使用该用户来进行可视化密钥添加和模型添加。  
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

在Controller/Auth/AuthController GenerateToken 中
可以找到expires: DateTime.Now.AddDays(30),
这表明Token将会在30天才会过期，即使本项目包含vip设定，但是如果一个不重新登录，
它的token将会持续30天有效，可以适当更改这个Token的有效期，本项目目前并不适合作为
商业项目，自己部署用于一定数量的朋友或者同事是比较合适的。

