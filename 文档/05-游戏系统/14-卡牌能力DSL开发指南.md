# 卡牌能力DSL开发指南 v2.0

## 目录

- [1. 整体设计](#1-整体设计)
  - [1.1 什么是能力系统](#11-什么是能力系统)
  - [1.2 能力的生命周期](#12-能力的生命周期)
- [2. 能力配置详解 (`AbilityConfig`)](#2-能力配置详解-abilityconfig)
  - [2.1 JSON结构总览](#21-json结构总览)
  - [2.2 字段详解](#22-字段详解)
- [3. DSL - 表达式语言](#3-dsl---表达式语言)
  - [3.1 简介与用途](#31-简介与用途)
  - [3.2 语法详解](#32-语法详解)
  - [3.3 内置函数](#33-内置函数)
- [4. DSL上下文变量大全](#4-dsl上下文变量大全)
  - [4.1 顶级关键字](#41-顶级关键字)
  - [4.2 `GameState` 属性](#42-gamestate-属性)
  - [4.3 `PlayerState` 属性](#43-playerstate-属性)
  - [4.4 `CardInstance` 属性](#44-cardinstance-属性)
- [5. 实践范例](#5-实践范例)

---

## 1. 整体设计

### 1.1 什么是能力系统

能力系统是游戏的核心，它允许我们通过数据配置来定义卡牌在游戏中的行为。系统的核心是一个**配置驱动**的架构，这意味着绝大部分卡牌逻辑（如触发条件、目标选择、产生的效果）都定义在数据库的JSON配置中，而不是硬编码在游戏程序里。

这套系统的“大脑”是一门专属的**领域特定语言 (DSL)**，它让我们可以用简洁的文本表达式来描述复杂的逻辑判断和数值计算。

### 1.2 能力的生命周期

要理解DSL的应用场景，首先需要了解一个卡牌能力从“蓝图”到“实际效果”的完整旅程：

```mermaid
graph TD
    A[数据库 (JsonConfig)] --反序列化--> B(AbilityConfig 对象);
    B --传入--> C{AbilityFactory};
    D[CardInstance (卡牌实例)] --传入--> C;
    C --创建--> E(Ability 实例);
    E --注册到--> F[AbilityManager];
    F --按事件类型索引--> G{EventType -> List<Ability>};
    
    subgraph 游戏循环
        H[游戏事件发生 (如 PlayCardEvent)] --> I[EventProcessor];
        I --调用--> F;
        F --使用事件类型从 G 中查找--> J[获取相关Ability列表];
        J --遍历--> K{执行每个Ability的条件检查 (DSL在此处执行)};
        K --条件满足--> L[返回可触发的Ability];
        L --> M[效果队列 (EffectQueue)];
        M --执行--> N[目标选择 (DSL在此处执行)];
        N --目标确定--> O[效果执行 (如造成伤害)];
    end

    style B fill:#f9f,stroke:#333,stroke-width:2px
    style E fill:#ccf,stroke:#333,stroke-width:2px
    style K fill:#9cf,stroke:#333,stroke-width:2px
    style N fill:#9cf,stroke:#333,stroke-width:2px
```

从上图可见，DSL主要在两个关键节点发挥作用：
1.  **条件检查 (K):** 判断一个被事件唤醒的能力是否满足其触发条件。
2.  **目标选择 (N):** 在效果执行前，筛选出符合条件的卡牌作为效果的目标。

---

## 2. 能力配置详解 (`AbilityConfig`)

`AbilityConfig` 是能力的静态“蓝图”，它由数据库中的JSON配置反序列化而来。

### 2.1 JSON结构总览

```json
{
    "id": "angelic_arrow",
    "name": "天使之箭",
    "type": "Triggered",
    "abilityTag": "Fanfare",
    "trigger": {
        "eventTypes": ["PlayCard"],
        "relationType": "TargetSelf"
    },
    "conditions": [],
    "targets": [
        {
            "selectorType": "PlayerChoice",
            "query": {
                "sourcePoolExpression": "opponent.field",
                "operations": [{
                    "$type": "FilterOperation",
                    "filters": [{
                        "valueSource": {
                            "$type": "FormulaValueConfig",
                            "formula": "self.Type == \"Follower\" or self.Type == \"Leader\""
                        }
                    }]
                }]
            }
        }
    ],
    "effects": [
        {
            "type": "Damage",
            "parameters": { "damageAmount": { "value": 1 } }
        }
    ]
}
```

### 2.2 字段详解

| 字段 | 类型 | 描述 | DSL应用场景 |
| :--- | :--- | :--- | :--- |
| `id` | string | 能力的唯一标识符。 | |
| `name` | string | 能力名称。 | |
| `type` | string | 能力类型 (如 `Triggered`, `Activated`)。 | |
| `abilityTag` | string | 能力标签 (如 `Fanfare`, `LastWords`)。 | |
| `trigger` | TriggerConfig | **触发器**：定义能力何时被唤醒。 | |
| `conditions`| List<ConditionConfig> | **条件列表**：定义能力被唤醒后需要满足的额外条件。 | **是**，`ConditionConfig` 中的 `expression` 字段使用DSL。 |
| `targets` | List<TargetConfig> | **目标列表**：定义能力需要作用的目标。 | **是**，`TargetConfig` 中的 `sourcePoolExpression` 和 `filters` 使用DSL。 |
| `effects` | List<EffectConfig> | **效果列表**：定义能力最终执行的操作。 | **是**，`EffectConfig` 的参数中可以使用 `FormulaValueConfig` 来动态计算数值。 |

---

## 3. DSL - 表达式语言

### 3.1 简介与用途

DSL是一门专为本游戏设计的“迷你编程语言”，用于**条件评估**和**值计算**。

*   **条件评估:** `player.pp >= 5` (返回 `true` 或 `false`)
*   **值计算:** `self.Atk * 2` (返回一个数字)

### 3.2 语法详解

| 类型 | 格式 / 操作符 | 示例 |
| :--- | :--- | :--- |
| **数字** | `10`, `-5` | `self.Atk > 10` |
| **字符串** | `"..."` | `self.Type == "Follower"` |
| **布尔值**| `true`, `false` | `player.isMyTurn == true` |
| **空值** | `null` | `TriggeringEvent == null` |
| **括号** | `( ... )` | `(2 + 3) * 4` |
| **成员访问** | `.` | `self.Atk` |
| **一元操作** | `not`, `-` (负号) | `not player.isMyTurn` |
| **乘除** | `*`, `/`, `%` | `self.Atk * 2` |
| **加减** | `+`, `-` | `self.Hp + 1` |
| **比较** | `>`, `>=`, `<`, `<=` | `player.pp >= 5` |
| **相等** | `==`, `!=` | `self.Type != "Amulet"` |
| **逻辑与** | `and` | `player.isMyTurn and not IsEmpty(player.field)` |
| **逻辑或** | `or` | `self.Type == "Follower" or self.Type == "Leader"` |

### 3.3 内置函数

| 函数名 | 功能描述 |
| :--- | :--- |
| `Abs(number)` | 返回一个数字的绝对值。 |
| `Contains(list, item)` | 检查一个列表是否包含某个项目。 |
| `Count(list)` | 返回列表中的项目数量。 |
| `If(cond, then, else)` | 如果条件为真，返回`then`的值，否则返回`else`的值。 |
| `IsEmpty(list)` | 检查一个列表是否为空。 |
| `Max(num1, num2, ...)` | 返回一系列数字中的最大值。 |
| `Min(num1, num2, ...)` | 返回一系列数字中的最小值。 |
| `Sum(list)` | 计算一个数字列表的总和。 |

---

## 4. DSL上下文变量大全

### 4.1 顶级关键字

| 关键字 | 解释 |
| :--- | :--- |
| `self`, `this`, `current` | 指代**正在发动这个能力的卡牌实例**。 |
| `player` | 指代**正在执行动作的玩家**（通常是我方）。 |
| `opponent` | 指代**当前玩家的对手**。 |
| `gamestate` | 访问完整的游戏状态树。 |
| `TriggeringEvent` | 访问**触发本次能力的那个游戏事件**。 |

### 4.2 `GameState` 属性

通过 `gamestate` 关键字访问。

| 属性 | 类型 | 描述 |
| :--- | :--- | :--- |
| `Status` | enum | 游戏状态 (如 `Starting`, `Finished`)。 |
| `Phase` | enum | 游戏阶段 (如 `Mulligan`, `Main`)。 |
| `CurrentPlayerId` | Guid | 当前回合玩家的ID。 |
| `Player1` / `Player2` | PlayerState | 访问具体的玩家状态。 |
| `AllField` | List<CardInstance> | 双方战场所有卡牌的列表。 |
| `AllHand` | List<CardInstance> | 双方手牌所有卡牌的列表。 |

### 4.3 `PlayerState` 属性

通过 `player` 或 `opponent` 关键字访问。

| 属性 | 类型 | 描述 |
| :--- | :--- | :--- |
| `PlayerId` | Guid | 玩家的唯一ID。 |
| `MaxPlayPoints` | int | 最大能量点。 |
| `CurrentPlayPoints` | int | 当前能量点。 |
| `MaxEvolutionPoints` | int | 最大进化点。 |
| `CurrentEvolutionPoints` | int | 当前进化点。 |
| `TurnNumber` | int | 该玩家已经历的回合数。 |
| `PlayedCardCountThisTurn` | int | 本回合已使用卡牌数。 |
| `Leader` | CardInstance | 该玩家的主战者实例。 |
| `Hand` | List<CardInstance> | 手牌列表。 |
| `Field` | List<CardInstance> | 战场列表。 |
| `Deck` | List<CardInstance> | 牌库列表。 |
| `Graveyard` | List<CardInstance> | 墓地列表。 |

### 4.4 `CardInstance` 属性

通过 `self` 或访问列表中的卡牌时使用。

| 属性 | 类型 | 描述 |
| :--- | :--- | :--- |
| `InstanceId` | Guid | 卡牌实例的唯一ID。 |
| `CurrentDefinitionId` | int | 当前卡牌定义ID（会因进化改变）。 |
| `OwnerPlayerId` | Guid | 该卡牌拥有者的ID。 |
| `CurrentZone` | enum | 卡牌当前所在的区域。 |
| `CardType` | enum | 卡牌类型 (如 `Follower`, `Spell`)。 |
| `CurrentCost` | int | 当前费用（计算buff后）。 |
| `CurrentAttack` | int | 当前攻击力（计算buff后）。 |
| `CurrentHealth` | int | 当前生命值。 |
| `MaxHealth` | int | 最大生命值（计算buff后）。 |
| `IsEvolved` | bool | 是否已进化。 |
| `Keywords` | List<enum> | 该卡牌拥有的所有关键字列表。 |
| `AbilityState` | Dictionary | 用于存储能力的内部状态（如计数器）。 |

---

## 5. 实践范例

---

### **附录B: DSL实践范例**

理论知识需要通过实践来巩固。本章节将通过分析项目中的具体卡牌能力配置，展示DSL在不同场景下的实际用法。

#### **范例1: 基础目标筛选 (天使之箭)**

“天使之箭”的效果是“对一个敌方单位（随从或领袖）造成1点伤害”。这里的核心在于如何准确地筛选出“敌方随从或领袖”。

**配置片段:**
```json
"targets": [
    {
        "selectorType": "PlayerChoice",
        // ... 其他配置
        "query": {
            "sourcePoolExpression": "opponent.field", // 目标池：对手的战场
            "operations": [
                {
                    "$type": "FilterOperation",
                    "filters": [
                        {
                            "valueSource": {
                                "$type": "FormulaValueConfig",
                                "formula": "self.Type == 'Follower' or self.Type == 'Leader'"
                            }
                        }
                    ]
                }
            ]
        }
    }
]
```

**DSL解析:**

*   **`sourcePoolExpression: "opponent.field"`**: 这不是DSL，但它定义了DSL执行的初始数据集。在这里，我们指定了目标池是对手的战场区域。
*   **`formula: "self.Type == 'Follower' or self.Type == 'Leader'"`**: 这是本例的核心DSL。
    *   `self`: 这是一个特殊的关键字，代表当前正在被筛选的卡牌池中的**每一个单位**。
    *   `.Type`: 访问单位的 `Type` 属性。根据我们之前分析的 `CardInstance`，我们知道它拥有 `Type` 属性（随从、法术、护符、领袖）。
    *   `== 'Follower'`: 判断单位类型是否为“随从”。
    *   `or`: 逻辑“或”运算符。
    *   **整体含义**: 这个表达式会遍历 `opponent.field` 中的每一个单位，如果单位的类型是“随从”或者“领袖”，则表达式返回 `true`，该单位被保留在目标列表中，否则被剔除。

---

#### **范例2: 动态数值计算 (推演)**

假设我们要设计一张卡牌，效果是“对一个敌方随从造成等同于我方场上随从数量的伤害”。

**配置片段 (推演):**
```json
"effects": [
    {
        "type": "Damage",
        "parameters": {
            "damageAmount": {
                "$type": "FormulaValueConfig",
                "formula": "owner.field.Count(card => card.Type == 'Follower')" 
            }
        }
    }
]
```

**DSL解析:**

*   **位置**: 这次DSL位于 `effects` 的参数中，用于动态计算效果的数值。
*   **`formula: "owner.field.Count(card => card.Type == 'Follower')"`**:
    *   `owner.field`: 从 `GameContext` 中获取我方战场上的所有卡牌实例列表。
    *   `.Count(...)`: 这是C# LINQ中的一个方法，DSL支持直接调用。它用于计算集合中满足特定条件的元素数量。
    *   `card => card.Type == 'Follower'`: 这是一个Lambda表达式。`card` 是一个临时变量名，代表列表中的每个元素；`=>` 之后是条件，即卡牌类型必须是“随从”。
    *   **整体含义**: 这个表达式会计算我方场上类型为“随从”的卡牌数量，并将这个数值作为最终的伤害值。

---

#### **范例3: 复杂的复合条件判断 (推演)**

假设一张卡的战吼效果是：“如果你的PP最大值大于等于7，并且你的手牌数量少于5张，则抽一张牌”。

**配置片段 (推演):**
```json
"conditions": [
    {
        "valueSource": {
            "$type": "FormulaValueConfig",
            "formula": "player.maxPp >= 7 and player.hand.Count < 5"
        }
    }
],
"effects": [
    // ... 抽牌效果
]
```

**DSL解析:**

*   **位置**: DSL位于 `conditions` 数组中，用于判断能力是否能够触发。只有当所有`conditions`中的`formula`都返回`true`时，`effects`才会执行。
*   **`formula: "player.maxPp >= 7 and player.hand.Count < 5"`**:
    *   `player.maxPp`: 从 `GameContext` 中获取当前玩家的PP最大值。
    *   `>= 7`: 判断PP最大值是否大于或等于7。
    *   `and`: 逻辑“与”运算符。
    *   `player.hand.Count`: 获取当前玩家手牌的数量。
    *   `< 5`: 判断手牌数量是否小于5。
    *   **整体含义**: 这个表达式清晰地描述了能力触发所需的两个游戏状态条件，只有同时满足时，能力才会生效。

通过以上范例，我们可以看到DSL的强大之处：它允许游戏设计师用接近自然语言的简洁文本，来描述复杂的游戏逻辑，而无需修改任何底层代码。