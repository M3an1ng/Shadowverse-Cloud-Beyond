# Change: Convert “神眷的棋盘” card databases

## Why
新增卡包“神眷的棋盘”的原始卡牌数据库已放置在 `data/refer/archive/神眷的棋盘/`，需要转换为现行 `card_data` 结构以便游戏加载与测试。

## What Changes
- 将 `data/refer/archive/神眷的棋盘/cardDatabase_<craft>.json` 转换并输出到 `data/神眷的棋盘/card_data_<craft>.json`，与现有数据分离。
- 按职业逐一转换并在每次转换后运行 DSL/数据校验与场景测试。
- 若新卡包涉及新机制，补充所需的转换规则与系统能力支持（如有）。

## Impact
- 受影响的 specs: `card-data`
- 受影响的代码/数据: `data/神眷的棋盘/card_data_<craft>.json`、转换脚本/辅助工具（如需）
