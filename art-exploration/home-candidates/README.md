# 首页候选探索：图片索引与交付记录

## 当前结论
**5 / 5 个主题首页均已生成、保存并目视检查。** 本轮新增 B、C、D、E 四张，A 保持原图并已获用户同意列为候选。
这不是五套全界面交付；目前每主题仅 01 主菜单一张。后续选定主题后，才按 26 个编号进入正式系列。
未修改游戏源码，未新增玩法，未把静态概念图当作已实现界面。

## 独立图片与来源
|主题|首页图|上游原图|共享美术规范|实际提交提示词|
|---|---|---|---|---|
|A 星界航行|[16:9 PNG](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/A-starfarer/A-01-home.png)|[原图](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/A-starfarer/A-01-home-native.png)|[规范](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/A-starfarer/art-direction.md)|[提示词](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/A-starfarer/01-home-prompt.txt)|
|B 月辉圣典|[16:9 PNG](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/B-moonlit-codex/B-01-home.png)|[原图](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/B-moonlit-codex/B-01-home-native.png)|[规范](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/B-moonlit-codex/art-direction.md)|[提示词](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/B-moonlit-codex/01-home-prompt.txt)|
|C 装甲竞技场|[16:9 PNG](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/C-armored-arena/C-01-home.png)|[原图](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/C-armored-arena/C-01-home-native.png)|[规范](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/C-armored-arena/art-direction.md)|[提示词](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/C-armored-arena/01-home-prompt.txt)|
|D 都市街机|[16:9 PNG](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/D-neon-arcade/D-01-home.png)|[原图](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/D-neon-arcade/D-01-home-native.png)|[规范](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/D-neon-arcade/art-direction.md)|[提示词](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/D-neon-arcade/01-home-prompt.txt)|
|E 星象收藏馆|[16:9 PNG](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/E-astral-archive/E-01-home.png)|[原图](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/E-astral-archive/E-01-home-native.png)|[规范](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/E-astral-archive/art-direction.md)|[提示词](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/E-astral-archive/01-home-prompt.txt)|

- [逐图检查与比较说明](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/候选比较与检查.md)
- [26 个界面统一编号清单](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/界面统一编号清单.md)
- [五个完整提示词与美术方向汇总](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/briefs.json)
- [生成审计记录](D:/workspace/Shadowverse-Cloud-Beyond/art-exploration/home-candidates/generation-audit.json)

## 模型、质量与尺寸
- backend：official-codex-mcp；所有成功任务的 requested_model 均为 **gpt-image-2.5-sunburst**，未切换模型。
- 五张成功任务均请求 quality=auto；上游回报 quality=medium。
- 五张上游原图均为 **1672 × 941**，原始 PNG 均另存。
- 交付统一为 **1536 × 864（16:9）PNG**，由工具本地 contain 重采样输出，不宣称原生 1536 × 864。
- 续作继续使用已有完整提示词；不将 A 当作 B—E 样式参考，避免跨风格同质化。当前四张首页均独立生成，未传参考图。
- 先前 A 的 high 质量请求因上游返回 medium 被工具判定失败；B 先前两次容量失败。此次 C 两次失败后成功，E 一次失败后成功；失败任务未计入完成数。
- 部分失败任务记录中包含上游图片元数据，但任务未完成验证，未作为候选交付。
- 未使用外部图库模板；路由预览仅用于确认 image2 逻辑通道，实际请求模型/参数以上述 MCP 审计为准。

## 审核边界与下一步
五张主导航内容齐全，卡牌示例均为费用3/攻击2/生命4。主要中文完成目视核查，长装饰文案不作为可直接上线的文案。
B 关于图标语义、部分主题的剧情暗示副文案、D 卡框风格、C 重复多人入口均已具体记入检查说明。
首页仅验证主题与导航表达；战场可读性、编辑器效率和跨页一致性尚未被生成图验证。
用户选定方向后：实际引用该批首页生成 04 战场和 06 牌组编辑，再沿用已确认卡框完成其他页面，布局随用途重排。
