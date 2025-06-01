该系统包含3个代码存储库。  
分别是 turboai-user,turboai-admin,turboai-server(open-turboai-server).  
该系统采用前后端分离的架构进行设计，前端采用Angular17框架，并使用ioniframeword和
angular-ant.  
后端采用 .net Core8框架。  
前端和后端之间以json的形式使用API来交换数据。  
在部署的时候，可以在nginx配置路由规则，把用户端和服务端部署在一个规则下，管理端部署在
一个静态文件服务下。  
当然，也可以把他们分开部署。分开部署比较方便。
分开部署时，用户端和管理端的编译之后的文件需要进行修改，以适应实际的后端地址。  
代码位于 /assets/config.json
```json
{
  "apiUrl": "https://localhost:44301/"
}

```
修改其中的apiUrl即可。如果是用户端和服务端部署在同一个规则下，直接清空值即可。
