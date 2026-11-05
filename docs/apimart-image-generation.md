# APIMart GPT Image 2.5 / 2.6（2026-11-05）

恢复 `/image-studio/APIMart-image`，独立于 OpenAI 直连入口。后台使用启用的 APIMart 类型供应商密钥，不向浏览器暴露密钥。旧 GPT Image 2.0 创建端点继续返回 410。

| 模型 | 主要能力与当前限制 |
| --- | --- |
| 2.5 Flare / Sunburst | 15 种画幅、像素尺寸、1k/2k/4k、1–4 张、参考图与蒙版；PNG/JPEG/WebP |
| 2.6 Flare / Sunburst | 标准版能力加复杂度、标注编辑、GIF、SVG；SVG 需要指定文本作者模型，且只允许单张文生图 |
| 2.5 Ext | version 选择 Flare/Sunburst，10 种画幅，1K/2K/4K，1–4 张，参考图；不发送标准版专有参数 |
| 2.6 Ext | 当前仅 1k、每任务 n=1；version、quality、complexity、GIF/SVG；不提供蒙版/标注或无效的背景/压缩控件 |

APIMart 2.6 文档已公开，但其模型介绍页仍提示尚未上线。此处完成接口接入，不代表某个账户/通道已开通，也未发起付费生成验证。模型及价格状态以实际供应商为准。

## 上传、生成与查询

- `POST /api/apimart/uploads/images`：multipart `file`，最多 20MB，PNG/JPEG/WebP/GIF，校验文件签名。返回公开 URL，官方存储有效期为 72 小时。标准版参考图须用公开 HTTP(S) URL；本地文件先上传。
- `POST /api/apimart/image-generate/gpt-image`：按模型校验后调用 `/v1/images/generations`。支持最多 16 张参考图，蒙版和标注互斥，GIF/SVG 仅文生图。标准版的 GIF 每帧尺寸按整张帧表预算校验；UI 使用保守的自动尺寸。
- `GET /api/apimart/getTask/{id}`：校验账户归属，使用提交时保存的供应商密钥 ID。密钥被移除或停用时返回路由不可用，不随机换账户查询。
- `POST /api/apimart/image-generate/receipt`：上游已受理但本地回执暂存失败时，只恢复归属记录，不重新生成。恢复凭据经过 Data Protection 保护，绑定账户/任务/密钥，24 小时过期。多实例须共享既有 Data Protection 密钥环。

页面保存全部受理任务 ID，可切换、暂停和恢复查询；每 4 秒查询，单轮最多 5 分钟，离开页面只停止本地轮询。生成结果单独保存和同步，失败可独立重试。提交结果不确定时不自动重新 POST；幂等键不构成供应商不支持幂等或密钥池变化时的恰好一次保证。

生成和上传受 image 功能权限限制，任务查询受 image-task 限制。恢复回执仅进行本地记录校验，不消耗生成配额。参考图/蒙版尺寸及透明区域须符合供应商规范；本服务不主动下载这些用户 URL。返回的供应商错误经过清理。

## 数据库与部署

`FeatureAccessTasks` 新增 nullable `SupplierKeyId`，无外键，保留密钥删除后的任务归属。已有部署停止写入后执行一次 `src/Turbo.Auth/Resources/merge/apimart-task-route-upgrade.sql`，再启动新版。不要通过重跑 `feature-access.sql` 代替升级，因为 CREATE IF NOT EXISTS 不会给旧表加列。新库初始化已同步。旧回执保留 NULL，延续兼容查询；新任务都写入密钥绑定。

开发库 niko 已完成升级，全部 22 张业务表行数不变。升级前/后记录位于 Git 忽略的 `TestResults/apimart-route-upgrade-20261105-234838/`。新版启动检查通过，创建、上传、回执恢复和任务查询的未登录请求均返回 401；验证实例已关闭。

## 来源与验证

实现依据 APIMart 文档索引发现的官方 Markdown 页面，原 cn/images 目录未直接返回内容：

- [2.5 标准版](https://docs.apimart.ai/en/api-reference/images/gpt-image-2.5/generation.md)
- [2.6 标准版](https://docs.apimart.ai/en/api-reference/images/gpt-image-2.6/generation.md)
- [2.5 Ext](https://docs.apimart.ai/en/api-reference/images/gpt-image-2.5-ext/generation.md)
- [2.6 Ext](https://docs.apimart.ai/en/api-reference/images/gpt-image-2.6-ext/generation.md)
- [图像上传](https://docs.apimart.ai/en/api-reference/uploads/images.md)、[任务查询](https://docs.apimart.ai/en/api-reference/tasks/status.md)

前端 122 项浏览器测试、6 项国际化检查、TypeScript 与生产构建通过；后端 238 项普通测试通过，包含模拟供应商协议与上传测试；隔离 MySQL 回执及架构测试 15/15 通过。桌面与 390px 手机界面实测无横向溢出，2.6 Ext 正确限制 1k/n=1。未调用真实上传或付费生成接口。
