# 模型示例与路由权重（2026-11-05）

## 模型示例目录

快捷添加目录与前端离线示例统一为：`gpt-6-luna`、`claude-sonnet-5-5`、`claude-haiku-5-5`、`claude-opus-5-5`、`gemini-3.8-flash`。均标记聊天及图片输入能力。它们是示例，不会自动绑定密钥，也不保证特定代理或账户已开通。

核实来源：[OpenAI GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna)、[OpenAI 停用公告](https://developers.openai.com/api/docs/deprecations)、[Claude 模型目录](https://platform.claude.com/docs/en/models/overview)、[Gemini 3.8 Flash](https://aistudio.google.com/docs/models/gemini-3.8-flash)、[Gemini 停用公告](https://aistudio.google.com/docs/deprecations)。GPT-6 Luna 支持现有 Chat Completions；若后续启用工具调用，需遵循其 reasoning_effort=none 限制，或单独迁移 Responses API。没有修改现有音频、图片、视频端点。

旧目录的 34 条 GPT-4o、GPT-4.1、早期 GPT-5、o 系列和 Gemini 2.x 示例移出默认推荐；这不表示它们全部已经下线。`chatgpt-4o-latest` 和 Gemini 2.0 Flash 等已退出服务，部分 OpenAI 旧型号仍处于停用预告期。仍需使用的受支持型号可以手动添加。

`model-catalog-refresh.sql` 在事务内按明确的 ModelValue 清单删除旧 AvailableModels，再幂等添加新示例；保留清单外的自定义示例。不删除 Models、密钥、账户和聊天历史。不要通过改写聊天历史里的模型字符串迁移模型。

本次已核对本地开发库 niko：有 34 条旧示例，Models 和路由绑定为空；执行后为 5 条新示例。按用户要求未生成备份。账户、消息、密钥行数保持不变，启动结构检查通过。

## 数据结构与升级

ModelKeyBinds 新增 `RoutingWeight double NULL`。保留 Fee 避免破坏旧客户端，不臆造币种、每 token 价格或真实扣费规则。

- 新库：使用更新后的 `merge/init.sql` 和 `open-initdata.sql`。
- 已完成架构升级的旧库：运行一次 `merge/routing-weight-upgrade.sql`，再运行 `merge/model-catalog-refresh.sql`。后者可重复运行；前者重复运行会报告重复列，需先核对实际结构。
- 更早版本：先完成 `architecture-upgrade.sql` 及需要的邮箱验证升级，再添加权重列。
- 启动只读校验结构，缺列会提示专用升级脚本，绝不自动修改数据库。

MySQL ALTER TABLE 隐式提交，不能依靠事务回滚 DDL。回滚应用前应导出显式权重配置；旧版本会忽略新权重，不应直接删除此列丢失配置。

更新密钥绑定时按当前密钥及 ModelId 匹配已拥有的绑定，保留绑定主键与熔断身份；新增才生成主键，移除才删除，拒绝同一请求重复绑定同一模型。不信任客户端提交的数据库关系图或路由 ID。

## 加权随机路由

`Priority` 数字越小越优先。仅在最优的健康、未排除的优先级内选择：

| RoutingWeight | 行为 |
| --- | --- |
| null / 未提供 | 兼容旧行为：Fee > 0 时取 1/Fee，否则取 1 |
| 0 | 不参与选择，可用于暂停路由 |
| (0, 1000000] | 使用显式相对权重，不再依赖 Fee |

例如同模型、同优先级的两条路由权重 1 和 3，健康时选择比例为 25% 和 75%；费用可独立记录为 0.2 和 0.8。优先级不同则不能这样计算比例。熔断或首个输出前失败后，会在剩余路由中重新计算；首个输出后不切换供应商。

Fee 目前是相对成本系数，不是用户实际账单。需要独立流量分配时应明确填写 RoutingWeight；只有兼容模式仍使用费用倒数。旧配置不强行回填权重，以保留既有客户端更新 Fee 的行为。

选择前过滤非有限值和非正权重，极小正费用的倒数饱和到最大有限数，同层权重先除以最大值再求和，避免总和溢出及最后一条路由偏置。禁用密钥不会进入普通路由或 ApiMart 池。

## 验证

后端 155 项常规测试通过，包含确定性的 1:3 边界、优先级、零权重、非法数值与大数溢出测试；MySQL 8.4.13 的 12 项架构测试通过，验证新建/旧库升级结构一致、并发同步及 12 个路由触发器。另有独立 MySQL 测试覆盖权重持久化、路由主键保留、版本递增及目录刷新幂等性。测试只使用随机命名的临时库，结束后删除。

未向真实模型供应商发出付费请求；模型可用性仍需管理员使用实际供应商权限核对。
