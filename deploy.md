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
https://github.com/nanaminato/turbo-user  
https://github.com/nanaminato/turboai-admin  
在项目根目录执行
```
ng build
```
构建之后,将turbo-user的项目放入 /wwwroot/ai 目录
将turboai-admin的构建文件放入/wwwroot/admin 目录
![img.png](img.png)
部署完成之后访问
host:8000/ai 访问对话界面  
host:8000/admin 访问 管理系统  
访问 host:8000/ 你将得不到任何东西，也可以放置一个index.html 到wwwroot下面。
并设置自动跳转， 以便于可以跳转到/ai或者/admin。 
