# 图像生成工作室（2026-11-05）

工作室默认使用 gpt-image-2.6-flare，同时提供 gpt-image-2.6-sunburst 和 2.5 Flare/Sunburst。按用户要求，DALL·E 与 GPT Image 2 及更早型号已移出工作室和官方生成支持范围。前端选择模型不会创建供应商路由；管理员仍需配置相应逻辑模型与可用上游别名。图片生成沿用 api/Media/gpt-image、登录及 image 功能权限；旧 DALL·E 与 APIMart 2.0 创建地址返回 HTTP 410，且不向上游发起请求。

桌面布局为参数与结果两栏，窄屏自动改为纵向滚动。高级参数按模型能力展示；生成中禁止重复提交。结果支持大图预览和下载；SVG 保持在 img 图片上下文中，不注入页面 HTML。历史保存失败不会丢失生成结果，可单独重试保存，避免为恢复历史再次生成付费图片。APIMart 原有创建页面仅提供 2.0 模型，已移除入口；旧页面地址跳转到当前工作室，历史任务和结果查询保留。

## 参数

2.6 支持 complexity=low/medium/high。2.5/2.6 支持 xhigh/max 质量及 PNG/JPEG/WebP/GIF/SVG。SVG 每次只生成一张；GIF 使用自动尺寸，先采用参考文档共同范围 2–16 帧、每帧 20–2000ms。GIF/SVG 预览权限仍取决于供应商与账户开通情况。透明背景只配 PNG/WebP；压缩只发送给 JPEG/WebP。不同模型切换会重置不支持的选项，后台按最终上游模型和逻辑模型再次校验；自定义别名需要逻辑模型或最终上游对应已支持的 2.5/2.6 型号。

后端沿用官方 SDK 的客户端、地址和鉴权，通过协议请求承载当前强类型 SDK 尚未包含的新字段。返回值仅保留本地响应契约，供应商错误不透传凭据或内部响应。本次扩展现有文字生成图片流程，没有新增官方图片编辑或标注端点。

## 官方来源

- [GPT Image 2.6 Sunburst](https://developers.openai.com/api/docs/models/gpt-image-2.6-sunburst)
- [Images 生成 API](https://developers.openai.com/api/reference/resources/images/methods/generate)
- [图像生成指南](https://developers.openai.com/api/docs/guides/image-generation)
- [停用公告](https://developers.openai.com/api/docs/deprecations)



## 验证

后端 193 项普通测试通过；新增图像测试使用模拟上游，覆盖参数剪裁、别名、格式约束、错误清理与响应解码。未向真实图像供应商发送付费请求。

前端最终验证：101 项 ChromeHeadless 测试、6 项国际化检查、应用/测试 TypeScript 和生产构建通过；桌面与 390px 手机布局已实测，无横向溢出。

APIMart 2.5/2.6 独立异步接口现已恢复，详见 [APIMart 接入说明](apimart-image-generation.md)；旧 2.0 创建地址仍返回 410。
