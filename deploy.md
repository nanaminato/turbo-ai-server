# 服务端
在部署和测试的前置条件很相似
观察/Turbo-Auth/appsettings.Product.json  
创建对应的数据库  
然后执行数据库初始化和数据初始化语句在/Turbo-Auth/Resources/merge  
- init.sql
- open-initdata.sql   

当数据库准备完毕之后，在Visual Studio中（或者使用命令行）进行发布(产品)构建。  
构建之后启动即可。
当然在linux服务器部署时要进行适当的反向代理和配置，这里不过多介绍。  

# 用户端，管理端
在项目根目录执行
```
ng build
```
构建项目之后，将构建的静态资源部署到静态服务程序上（如nginx)  
