# 服务端
### 下载代码和环境
首先下载本项目代码  
然后安装对应的nuget包
### 创建数据库
导航到/Turbo-Auth/appsettings.Development.json
创建测试的数据库名  
然后导航到/Turbo-Auth/Resources/merge/  
在数据库管理软件中连接刚创建的数据库，然后分别执行
init.sql和open-initdata.sql。这样数据库中就具备了基本的数据，
额外的数据可以通过系统的管理端进行管理。  
然后可以构建项目并启动测试

# 用户端，管理端
下载代码之后，使用npm进行安装依赖包，在此之前首先安装Angular CLI工具。
然后直接启动项目即可

### 启动的配置
在分离部署或者测试的时候，前端通过一个一个指定的top链接连接后端服务。
比如  
https://turboai.cloud  
这个链接在src/assets/config.json中改变  
```json
{
  "apiUrl": "https://localhost:44301/"
}

```
在本地测试时，修改后端服务对应的端口号即可。