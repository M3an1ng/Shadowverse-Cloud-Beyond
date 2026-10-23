from pathlib import Path
import hashlib
import json
import os

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / 'docs/archive/2026-09-27'
BASELINE = json.loads((ROOT / '.codex/tmp-docs-refresh/docs-before.json').read_text(encoding='utf-8'))
SOURCES = sorted((ROOT / '文档').rglob('*.md')) + [ROOT / path for path in (
    'README.md', 'README-开发者工具.md', 'projectbrief.md', 'productContext.md',
    'systemPatterns.md', 'techContext.md', 'activeContext.md', 'progress.md',
    'openspec/project.md', 'docs/debug-console-commands.md',
    'docs/dsl_editor_design_v1.md', 'docs/dsl_editor_modularization_plan.md',
    'docs/dsl_editor_user_guide.md', 'docs/DslEditor_Cleanup_Report.md',
    'docs/replay-event-journal-ops.md',
)]


def replacement_for(source):
    path = source.relative_to(ROOT).as_posix()
    if any(term in path for term in ('DSL', 'dsl_', 'DslEditor', '能力系统', '目标选择')):
        return 'docs/05-卡牌创作与DSL.md'
    if any(term in path for term in ('开发环境', 'techContext')):
        return 'docs/02-开发与验证.md'
    if any(term in path for term in ('API', '认证', '数据库', '数据结构')):
        return 'docs/06-接口与数据.md'
    if any(term in path for term in ('debug', 'replay', '日志', '配置', '开发者工具')):
        return 'docs/07-运行部署与排障.md'
    if any(term in path for term in ('架构', 'systemPatterns', '组件', '状态管理')):
        return 'docs/03-架构与代码导航.md'
    if '05-游戏系统/' in path:
        return 'docs/04-对战与玩法.md'
    if any(term in path for term in ('项目管理', 'Bug', '参考', 'activeContext', 'progress')):
        return 'docs/08-文档维护.md'
    return 'docs/README.md'


def link_from(source, target):
    return os.path.relpath(target, source.parent).replace('\\', '/')


if ARCHIVE.exists():
    raise SystemExit('Archive already exists; inspect before continuing.')

records = []
for source in SOURCES:
    source = source.resolve(strict=True)
    source.relative_to(ROOT)
    relative = source.relative_to(ROOT).as_posix()
    destination = (ARCHIVE / relative).resolve()
    destination.relative_to(ARCHIVE)
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if BASELINE.get(relative) != digest:
        raise SystemExit(f'Document changed since inspection: {relative}')
    if destination.exists():
        raise SystemExit(f'Destination already exists: {destination}')
    records.append({
        'original': relative,
        'archive': destination.relative_to(ROOT).as_posix(),
        'sha256': digest,
        'replacement': replacement_for(source),
        'reason': '旧说明退出当前事实入口；混合历史设计、进度或待核验内容，原文保留供追溯，不表示逐句均错误。',
    })

ARCHIVE.mkdir(parents=True)
manifest = {'date': '2026-09-27', 'files': records}
(ARCHIVE / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
for record in records:
    source = ROOT / record['original']
    destination = ROOT / record['archive']
    destination.parent.mkdir(parents=True, exist_ok=True)
    source.rename(destination)
    source.write_text(
        '# 文档已归档\n\n'
        '> 本路径仅用于兼容旧链接，不再维护当前实现说明。归档日期：2026-09-27。\n\n'
        f"- [当前文档]({link_from(source, ROOT / record['replacement'])})\n"
        f"- [历史原文]({link_from(source, destination)})\n"
        f"- [归档清单与原因]({link_from(source, ROOT / 'docs/归档清单.md')})\n",
        encoding='utf-8',
    )
print(f'Archived {len(records)} original documents; compatibility links preserved.')
