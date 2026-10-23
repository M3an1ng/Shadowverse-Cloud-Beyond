from pathlib import Path
from urllib.parse import quote
from collections import Counter
import json
import os
import re

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / 'docs/archive/2026-09-27'
manifest = json.loads((ARCHIVE / 'manifest.json').read_text(encoding='utf-8'))


def link(label, target, parent):
    relative = os.path.relpath(ROOT / target, ROOT / parent).replace('\\', '/')
    return f'[{label}]({quote(relative, safe="/.-_")})'


def write(path, text):
    (ROOT / path).write_text(text.strip() + '\n', encoding='utf-8')


write('README.md', '''# Shadowverse-Cloud-Beyond

受影之诗启发的网页卡牌项目，包含实时对战、牌组构建、卡牌与能力编辑、任务及调试工具。前端使用原生 JavaScript + Vite，后端使用 ASP.NET Core 8 + SignalR + EF Core SQLite。

## 文档入口

- [项目文档导航](docs/README.md)
- [当前功能与规划边界](docs/01-项目现状.md)
- [开发启动与测试](docs/02-开发与验证.md)
- [架构与代码导航](docs/03-架构与代码导航.md)
- [运行部署与排障](docs/07-运行部署与排障.md)
- [OpenSpec 待核实清单](docs/OpenSpec待核实清单.md)
- [旧文档归档清单](docs/归档清单.md)

## 本地开发

准备好项目依赖后，在两个终端从仓库根目录运行：

```powershell
dotnet run --project "Server/ShadowverseBeyond.Server.csproj" --launch-profile http
```

```powershell
npm run dev
```

浏览器访问 `http://127.0.0.1:5173`；API 和 SignalR 代理到 `http://localhost:5100`。

**注意：后端启动会执行数据库迁移和种子逻辑。** 连接已有数据前先核对配置与备份。依赖准备、真实测试项目路径及旧脚本偏差详见开发文档。

文档基线为 **2026-09-27 的本地工作区**，不是发布承诺。近期 UI 目标、研究及 OpenSpec 提案与当前实现分开维护；本次验证范围见 [验证记录](docs/验证记录.md)。

项目操作约定见 [AGENTS.md](AGENTS.md)。
''')

write('openspec/project.md', '''# 项目上下文

> 核对日期：2026-09-27。本文件只为规范工作流提供入口，当前实现细节统一维护在 docs。

## 项目与技术

Shadowverse-Cloud-Beyond 是网页卡牌项目，包含实时对战、牌组、卡牌工坊和能力 DSL。前端为原生 JavaScript/HTML/CSS、Vite、SignalR 客户端；后端为 .NET 8 ASP.NET Core、SignalR、EF Core SQLite。测试使用 Jest/jsdom 与 xUnit。

- [文档导航](../docs/README.md)
- [功能现状与规划边界](../docs/01-项目现状.md)
- [开发与验证](../docs/02-开发与验证.md)
- [架构与代码入口](../docs/03-架构与代码导航.md)
- [文档维护](../docs/08-文档维护.md)
- [OpenSpec 待核实清单](../docs/OpenSpec待核实清单.md)

## 规范边界

- 主规范位于 [specs](specs/)，变更位于 [changes](changes/)，历史归档位于 [changes/archive](changes/archive/)。
- 提案是需求和设计记录，任务勾选不是代码或部署证据；先核对相关实现与测试再声明完成。
- 旧变更可能相互覆盖，不按最后修改日期批量覆盖主规范。
- 当前代码使用 SQLite；本文件不将 PostgreSQL 或旧分层设计列为已实现能力。
- 账号登录不是旧说明里的 JWT / Refresh Token 流程；房间 ID 与单局 ID 分开；回放日志写入默认关闭。
- 简体中文反馈，改动保持最小闭环；操作确认与 Git 限制以 [AGENTS.md](../AGENTS.md) 为准，不创建额外分支或提交。

本文件原版本保存在 [历史归档](../docs/archive/2026-09-27/openspec/project.md)。
''')

write('docs/archive/2026-09-27/INDEX.md', '''# 2026-09-27 历史文档归档

本目录保存整理前的 95 份原文，沿用原仓库相对路径。它们包含历史设计、旧进度、运行说明和原入口；归档不表示每一句都错误，也不代表其中未完成需求被取消。

**这里不是当前实现说明。** 请从 [当前文档入口](../../README.md) 开始。

- [原路径、原因及替代文档清单](../../归档清单.md)
- [原文字节 SHA-256 清单](manifest.json)
- [本次验证记录](../../验证记录.md)

历史原文按字节保存，未修复其原有链接、命令或结论。历史中的相对链接仍按原仓库布局理解；按迁移清单查找目标，不把失效的旧示例用于当前实现。

旧路径保留简短兼容入口，以便源码注释和历史提案继续找到当前文档与原文。根 README 和 OpenSpec 项目上下文已更新为当前入口。
''')

lines = [
    '# 文档归档清单', '',
    '> 执行日期：2026-09-27。共归档 95 份原文；保留原字节及 SHA-256，旧路径保留导航。', '',
    '## 为什么整理', '',
    '- 原有说明混合当前事实、历史设计和早期进度；旧认证文档的 JWT 流程与当前返回结构不符。',
    '- 旧环境说明缺少当前 SDK 解析、启动脚本端口和测试项目路径差异；旧回放说明遗漏默认关闭 Journal 写入的配置。',
    '- 退出当前入口不等于需求取消，也不表示本次逐句证明了旧文档全部错误。新文档只写已找到实现证据的结论。', '',
    '## 保留范围', '',
    '- 游戏业务代码、配置、资源和数据保持原样。',
    '- 近期 UI 现状/目标地图、提示词、research、superpowers 文件原位保留，分类见 [文档入口](README.md)。',
    '- OpenSpec changes/specs 原样保留；仅更新项目上下文入口并生成 [待核实清单](OpenSpec待核实清单.md)。',
    '- AGENTS、技能指令保留。根 Memory Bank 和旧文档路径改为兼容入口，原内容存于历史目录。', '',
    '## 原文与当前入口', '',
    '表中每项原因均为：旧说明退出当前事实入口，历史内容保留追溯；替代文档是当前主题入口，不是旧文档每一条细节的逐字重写。', '',
    '| 原路径（兼容入口） | 历史原文 | 当前主题入口 |', '|---|---|---|',
]
for item in manifest['files']:
    lines.append('| ' + ' | '.join([
        link(item['original'], item['original'], 'docs'),
        link('历史原文', item['archive'], 'docs'),
        link(Path(item['replacement']).stem, item['replacement'], 'docs'),
    ]) + ' |')
lines += ['', '机器可读清单与逐文件原因：[manifest.json](archive/2026-09-27/manifest.json)。',
          '完整性检查：`python "scripts/check-docs.py"`。实际结果见 [验证记录](验证记录.md)。']
write('docs/归档清单.md', '\n'.join(lines))

changes=[]
capabilities=Counter()
for directory in sorted((ROOT/'openspec/changes').iterdir()):
    if not directory.is_dir() or directory.name == 'archive':
        continue
    task=directory/'tasks.md'
    text=task.read_text(encoding='utf-8-sig') if task.exists() else ''
    done=len(re.findall(r'^\s*[-*]\s+\[[xX]\]',text,re.M))
    pending=len(re.findall(r'^\s*[-*]\s+\[ \]',text,re.M))
    specs=sorted((directory/'specs').glob('*/spec.md'))
    for spec in specs:
        capabilities[spec.parent.name]+=1
    missing=sum(not (ROOT/'openspec/specs'/spec.parent.name/'spec.md').exists() for spec in specs)
    group='缺任务文件' if not task.exists() else ('有未勾选项' if pending else ('任务全勾选' if done else '无任务条目'))
    changes.append({'name':directory.name,'done':done,'pending':pending,'specs':len(specs),'missing':missing,'group':group})

lines=['# OpenSpec 待核实清单', '',
       '> 2026-09-27 快照。任务数来自当前 tasks.md；统计不是实现验收、测试报告或自动归档决定。', '',
       '## 盘点结果', '',
       f"- 未归档变更目录：{len(changes)}。",
       f"- 任务全勾选：{sum(c['group']=='任务全勾选' for c in changes)}；含未勾选项：{sum(c['group']=='有未勾选项' for c in changes)}；缺任务文件：{sum(c['group']=='缺任务文件' for c in changes)}。",
       f"- 当前主规范文件：{len(list((ROOT/'openspec/specs').glob('*/spec.md')))}。",
       f"- 多个变更涉及同一 capability 的情况：{sum(n>1 for n in capabilities.values())} 组。", '',
       '本次保留所有 changes/specs 的内容、勾选和位置。未选定并逐项验收的变更不批量同步或归档，避免旧增量覆盖较新的需求。', '',
       '## 已核对的具体边界', '',
       '| 变更 | 当前证据 | 保留原因 |', '|---|---|---|',
       '| [房间/单局 ID 分离](../openspec/changes/add-roomid-and-keep-gameid-per-match/tasks.md) | [MatchController](../Server/Controllers/MatchController.cs) 已有 RoomId/ActiveGameId 及兼容字段；[GameHub](../Server/Hubs/GameHub.cs) 同时读取新旧参数 | tasks 仍含删除兼容分支和多局/重连验证项，未按目录存在即归档 |',
       '| [对局表情](../openspec/changes/add-in-match-emote-system/tasks.md) | [EmoteCatalogService](../Server/Services/EmoteCatalogService.cs)、[前端目录](../src/emoteCatalog.js) 已存在 | tasks 仍含聊天链路测试等未勾选项；本次局部测试不等于验收全部场景 |',
       '| [活动任务](../openspec/changes/add-activity-quest-system/tasks.md) | [QuestProgressService](../Server/Services/QuestProgressService.cs) 有周期与来源去重；奖励领取在经济服务 | 源码存在不等于所有迁移、人工与集成验证完成，状态按下表保留 |',
       '| [回放日志](../openspec/changes/add-replay-event-journal/tasks.md) | [ReplayController](../Server/Controllers/ReplayController.cs) 存在，但 [ReplayOptions](../Server/Options/ReplayOptions.cs) 默认关闭 Journal 写入 | 验收应覆盖实际配置、数据写入和读取，不沿用旧运维说明 |',
       '| [实例连续性](../openspec/changes/refactor-card-instance-continuity-version/proposal.md) | 目前该变更仅提供提案，没有 tasks 文件 | 不以相近代码或旧战斗连续性字段代替本提案验收 |', '',
       '## 下一步核验标准', '',
       '1. 先选定变更并读取 proposal、design、tasks、delta specs；跨变更冲突先梳理替代关系。',
       '2. 每条需求映射到实际实现与测试；把测试存在、测试已运行、人工验收分开记录。',
       '3. 对比主规范后再同步增量，最后按项目 OpenSpec 技能归档；保留原 .openspec.yaml。',
       '4. “主规范缺对应项”只表示未发现同名主规范文件，不表示实现缺失或内容从未被其他规范覆盖。', '',
       '## 全量清单', '',
       '| 变更 | 已勾选/总任务 | 待核实状态 | 增量规范数 | 主规范缺对应项 |', '|---|---:|---|---:|---:|']
for c in changes:
    total=c['done']+c['pending']
    lines.append(f"| [{c['name']}](../openspec/changes/{c['name']}/) | {c['done']}/{total} | {c['group']} | {c['specs']} | {c['missing']} |")
lines += ['', '## 同一主题的多次变更', '',
          '这些主题需要核对先后关系，而不是把多个增量文件直接拼接：', '',
          '| Capability | 涉及变更数 |', '|---|---:|']
for name,count in sorted(capabilities.items(),key=lambda item:(-item[1],item[0])):
    if count>1:
        lines.append(f'| `{name}` | {count} |')
write('docs/OpenSpec待核实清单.md','\n'.join(lines))
print('Wrote root and OpenSpec entrypoints, archive index, migration map and change inventory.')
print(dict(Counter(c['group'] for c in changes)))
