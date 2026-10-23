# 03. 能力DSL与UI节点映射规范

**版本: 1.0**

**作者: AI 技术总监**

**更新日期: 2025-08-11**

## 1. 概述

本文档是可视化DSL编辑器的核心“施工图”。它旨在将《卡牌能力DSL开发指南》中定义的抽象`AbilityConfig` JSON结构，与《可视化DSL编辑器原型设计》中提出的可视化节点界面，进行精确、详尽的一一映射。

这份规范将详细定义：
*   `AbilityConfig` 中的每一个字段（如 `trigger`, `targets`, `effects`）在UI上如何表现。
*   每一个逻辑部分（如一个“目标筛选器”或一个“伤害效果”）如何对应到一个或多个可配置的UI节点。
*   每个节点需要提供哪些配置参数，以及这些参数应该使用何种UI控件（如输入框、下拉菜单、复选框等）。

这份文档是实现一个功能完备、用户友好的能力编辑器的技术基础。

---

## 3. 触发器配置 (`TriggerConfig`)

触发器定义了能力被游戏事件系统“唤醒”的条件。在UI上，它将表现为一个独立的配置区域或一个“起始节点”。

| JSON 字段 (`TriggerConfig`) | UI 控件类型 | 标签文本 (中文) | 交互与说明 |
| :--- | :--- | :--- | :--- |
| `eventTypes` | 标签式多选框 | `监听事件` | **必选项**。从一个预定义的、对设计师友好的游戏事件列表中进行选择（如“当卡牌被打出时”、“当回合开始时”）。数据源来自后端API，该API会提供所有可用的事件类型及其本地化名称。 |
| `relationType` | 下拉菜单 | `事件关联方` | **必选项**。用于快速筛选事件来源。例如，选择 `TargetSelf` (仅自身)意味着只关心与持有此能力的卡牌自身相关的事件。选项包括：`TargetSelf` (仅自身), `TargetAllies` (所有友方), `TargetEnemies` (所有敌方), `TargetAll` (任何单位)。 |

---

## 4. 条件配置 (`ConditionConfig`)

条件列表 (`conditions`) 决定了能力在被唤醒后，是否真的能够触发。每个条件都是一个最终需要返回 `true` 或 `false` 的DSL表达式。

在UI上，这将是一个可动态添加/删除“条件行”的列表。每一行都内嵌一个**可视化DSL编辑器实例**。

### 4.1. 整体UI结构

```mermaid
graph TD
    subgraph "能力编辑器 - 条件配置区"
        A["条件列表"]
        B["[+] 添加条件"]
        
        subgraph "条件行 1 (示例)"
            direction LR
            C1["条件表达式:"]
            C2["<在此处嵌入可视化DSL编辑器>"]
            C3["[移除]"]
        end

        subgraph "条件行 2 (示例)"
            direction LR
            D1["条件表达式:"]
            D2["<在此处嵌入可视化DSL编辑器>"]
            D3["[移除]"]
        end
        
        A --> B
        A --> "条件行 1"
        A --> "条件行 2"
    end
```

### 4.2. `ConditionConfig` 字段映射

| JSON 字段 (`ConditionConfig`) | UI 控件类型 | 标签文本 (中文) | 交互与说明 |
| :--- | :--- | :--- | :--- |
| `valueSource.formula` | **可视化DSL编辑器 (Visual DSL Editor)** | `条件表达式` | **核心交互区域**。用户通过点击 `[+]` 按钮，从上下文菜单（玩家、对手、卡牌等）开始，逐步构建一个完整的逻辑表达式。编辑器的最终输出是一个字符串，如 `player.maxPp >= 7 and Count(player.hand) < 5`，该字符串将被存入 `formula` 字段。 |

---

## 5. 目标配置 (`TargetConfig`)

目标配置 (`targets`) 是能力系统中最为复杂的模块之一，它定义了效果将作用于哪些卡牌实例。在UI上，它将表现为一个“目标选择器”节点，该节点内部可以嵌套多个“操作”子节点，形成一个处理流程。

### 5.1. 整体UI结构

```mermaid
graph TD
    subgraph "能力编辑器 - 目标配置区"
        A["目标列表"]
        B["[+] 添加目标"]
        
        subgraph "目标 1 (示例)"
            direction TB
            T1["**目标选择器节点**"]
            T2["配置面板"]
            T1 --> T2
            
            subgraph "T2: 配置面板"
                direction TB
                P1["选择方式: 玩家选择"]
                P2["目标数量: 1"]
            end

            subgraph "T1: 节点内部流程"
                direction TB
                N1["起始卡池: 对手的战场"]
                N2["筛选: 类型为'随从'"]
                N3["排序: 按攻击力降序"]
                N4["取前N个: 1个"]
                N1 --> N2 --> N3 --> N4
            end
        end
        
        A --> B
        A --> "目标 1"
    end
```

### 5.2. `TargetConfig` 字段映射

| JSON 字段 (`TargetConfig`) | UI 控件类型 | 标签文本 (中文) | 交互与说明 |
| :--- | :--- | :--- | :--- |
| `selectorType` | 下拉菜单 | `选择方式` | 定义目标是如何被最终确定的。选项包括 `PlayerChoice` (玩家选择), `Auto` (自动选择), `All` (选择所有)。 |
| `query.sourcePoolExpression` | **可视化DSL编辑器** | `起始卡池` | **DSL表达式**。用于定义一个初始的卡牌集合。例如 `opponent.field` (对手战场), `player.hand` (我方手牌)。 |
| `query.operations` | **可堆叠的操作节点列表** | `处理流程` | 这是目标选择的核心。用户可以点击 `[+]` 添加多种操作节点，如“筛选”、“排序”、“取前N个”等，对起始卡池进行链式处理。 |

### 5.3. `Operation` 节点映射

| 操作类型 (`$type`) | UI 节点名称 | 配置参数 | 参数UI控件 |
| :--- | :--- | :--- | :--- |
| `FilterOperation` | **筛选节点** | `filters` | 一个内嵌的**可视化DSL编辑器**列表，用于定义过滤条件。 |
| `SortOperation` | **排序节点** | `keyExpression`, `direction` | `keyExpression`: **DSL编辑器** (如 `self.Atk`)。<br>`direction`: 下拉菜单 (`Ascending`, `Descending`)。 |
| `TakeOperation` | **取前N个节点** | `count` | **数字输入框** 或 **DSL编辑器** (用于动态计算数量)。 |
| `RandomOperation` | **随机选择节点** | `count` | **数字输入框** 或 **DSL编辑器**。 |

---

## 6. 效果配置 (`EffectConfig`)

效果配置 (`effects`) 定义了能力最终执行的动作。在UI上，每个`effect`都将是一个独立的、可配置的“效果节点”。

### 6.1. 整体UI结构

```mermaid
graph TD
    subgraph "能力编辑器 - 效果配置区"
        A["效果链"]
        B["[+] 添加效果"]
        
        subgraph "效果 1: 造成伤害"
            direction LR
            E1["**伤害效果节点**"]
            P1["配置: <br>伤害值: 5<br>目标: 目标1"]
            E1 -- 有 --> P1
        end

        subgraph "效果 2: 抽牌"
            direction LR
            E2["**抽牌效果节点**"]
            P2["配置: <br>数量: 1"]
            E2 -- 有 --> P2
        end
        
        A --> B
        A --> "效果 1" --> "效果 2"
    end
```

### 6.2. `EffectConfig` 字段映射

| JSON 字段 (`EffectConfig`) | UI 控件类型 | 标签文本 (中文) | 交互与说明 |
| :--- | :--- | :--- | :--- |
| `type` | **效果节点选择器** | `效果类型` | 用户点击 `[+] 添加效果` 后，弹出一个包含所有可用效果类型的列表（如“造成伤害”、“治疗”、“抽牌”等），选择后添加一个对应的效果节点。 |
| `parameters` | **动态配置面板** | `效果参数` | **核心配置区域**。根据所选的 `type`，动态生成不同的参数配置项。 |

### 6.3. `parameters` 动态参数映射 (部分示例)

| 效果类型 (`type`) | 参数名 | 参数UI控件 |
| :--- | :--- | :--- |
| `Damage` | `damageAmount` | **数字输入框** 或 **DSL编辑器** (用于动态计算伤害值)。 |
| `Heal` | `healAmount` | **数字输入框** 或 **DSL编辑器**。 |
| `Draw` | `drawCount` | **数字输入框** 或 **DSL编辑器**。 |
| `Summon` | `cardDefId`, `count` | `cardDefId`: **卡牌选择器** (模态窗口)。<br>`count`: **数字输入框**。 |
| `ModifyStat` | `attackMod`, `healthMod` | `attackMod`: **数字输入框** (可为负)。<br>`healthMod`: **数字输入框** (可为负)。 |

---

## 附录A: 【设计规约】效果参数UI的动态生成架构

**重要说明：** 本章节描述的是一套用于实现卡牌编辑器动态UI的**目标架构**。它旨在将当前后端代码中“隐式”的参数约定，转化为前端可以消费的“显式”元数据，从而实现UI的动态生成。

### A.1. 现状分析：隐式的参数约定

通过对 `EffectExecutor.cs` 和 `EffectConfig.cs` 的分析，我们了解到当前系统的数据流如下：

1.  **配置时**: 设计者在 `EffectConfig` 中，为一个效果（如 `Damage`）的 `Parameters` 字典添加条目。例如：`{ "DamageAmount": new ValueConfig(...) }`。这里的键名 `"DamageAmount"` 是一个**隐式的约定**。
2.  **运行时**: `EffectExecutor` 读取这个配置，解析 `ValueConfig` 得到具体数值，然后通过反射，将这个值赋给 `DamageEffectParams` 实例的 `DamageAmount` 属性。

这个流程的问题在于，卡牌编辑器UI作为配置工具，它**无从知晓**对于 `Damage` 效果，它应该提供一个名为 `"DamageAmount"` 的参数。

### A.2. 目标架构：元数据驱动UI

为了解决上述问题，我们提出以下“元数据驱动UI”架构。该架构的核心思想是：**利用后端的运行时参数类（如 `DamageEffectParams`）作为元数据的来源，来指导前端配置界面（`EffectConfig`）的生成。**

#### A.2.1. 实现步骤

1.  **后端：标注运行时参数类 (C#)**
    *   创建自定义特性 `[EffectMetadata]` 和 `[ParameterMetadata]`。
    *   使用这些特性来“标注”现有的运行时参数类（如 `DamageEffectParams`）及其属性。这些标注提供了UI生成所需的一切信息：显示名、描述、UI控件类型、默认值等。

    ```csharp
    // 示例：效果元数据特性 (定义效果本身)
    [AttributeUsage(AttributeTargets.Class)]
    public class EffectMetadataAttribute : Attribute
    {
        public string DisplayName { get; } // UI中显示的效果名称，如 "造成伤害"
        public string Description { get; } // 效果的详细描述
        public EffectMetadataAttribute(string displayName, string description) { /* ... */ }
    }

    // 示例：参数元数据特性 (定义效果的一个参数)
    [AttributeUsage(AttributeTargets.Property)]
    public class ParameterMetadataAttribute : Attribute
    {
        public string DisplayName { get; } // UI中显示的参数标签，如 "伤害值"
        public UiControlType ControlType { get; } // UI应使用的控件类型，如 "数字输入框"
        // ... 其他UI相关属性，如DefaultValue, Min, Max, Tooltip等
        public ParameterMetadataAttribute(string displayName, UiControlType controlType) { /* ... */ }
    }

    // 应用示例：对现有的 DamageEffectParams.cs 进行标注
    [EffectMetadata("造成伤害", "对目标造成指定数值的伤害。")]
    public class DamageEffectParams : BaseEffectParams
    {
        // 通过标注 DamageAmount 属性，我们定义了 EffectConfig.Parameters 中
        // 应该有一个键为 "DamageAmount" 的条目，其UI表现形式如下。
        [ParameterMetadata("伤害值", UiControlType.NumberStepper, DefaultValue = 1, Min = 0)]
        public int DamageAmount { get; set; }
    }
    ```

2.  **后端：元数据生成服务 (C#)**
    *   创建一个单例服务 `EffectMetadataGenerator`。
    *   在服务器**启动时**，该服务通过**反射**扫描所有标记了 `[EffectMetadata]` 的运行时参数类，读取所有元数据，并在内存中构建一个完整的UI元数据缓存。这份缓存将 `EffectType` 映射到其所需的参数列表和UI规则。

3.  **后端：元数据API端点**
    *   创建一个API端点 `GET /api/effects/ui-metadata`。
    *   该API直接从 `EffectMetadataGenerator` 服务的缓存中返回指定 `EffectType` 的UI元数据JSON。

4.  **前端：动态UI渲染引擎**
    *   当用户在编辑器中选择一个效果类型（如 "Damage"）时，前端请求 `GET /api/effects/ui-metadata?type=Damage`。
    *   前端根据返回的元数据，动态渲染出“伤害值”的数字输入框，并知道当用户保存时，应在 `EffectConfig.Parameters` 中创建一个键为 `"DamageAmount"` 的 `ValueConfig` 对象。

#### A.2.2. 架构流程图

```mermaid
graph TD
    subgraph "后端 (启动时)"
        A[运行时参数类<br>(e.g., DamageEffectParams.cs)<br>+ 元数据特性] -- 反射扫描 --> B[内存中构建UI元数据缓存];
    end

    subgraph "后端 (API请求时)"
        C[前端请求<br>GET /api/effects/ui-metadata] --> D{API控制器};
        D -- "从缓存读取 (极快)" --> B;
        B --> E[返回UI元数据JSON<br>描述EffectConfig.Parameters的结构];
    end

    subgraph "前端 (编辑器交互)"
        F[用户选择'造成伤害'效果] --> G{UI渲染引擎};
        E -- "提供渲染规则" --> G;
        G --> H[查找'Damage'的元数据];
        H --> I[读取'DamageAmount'参数的UI配置];
        I --> J[动态创建并渲染'数字步进器'组件];
        J -- 用户输入 --> K[更新EffectConfig.Parameters];
    end
```

#### A.2.3. 方案优势

*   **单一事实来源**: 后端运行时参数类 (`...Params.cs`) 成为驱动UI的唯一真实来源，完美连接了配置与执行。
*   **高度可扩展**: 添加新效果或为现有效果增加新参数，只需修改后端的参数类并添加标注，前端无需任何代码改动。
*   **维护简单**: 修改一个参数的显示名称或UI控件，只需在C#特性中修改一处。
*   **高性能**: 将有性能开销的反射操作前置到服务器启动时，保证了API的快速响应。

## 2. 顶层能力配置 (`AbilityConfig`)

这是能力配置的根节点，对应能力编辑器的整体设置面板。

| JSON 字段 (`AbilityConfig`) | UI 控件类型 | 标签文本 (中文) | 交互与说明 |
| :--- | :--- | :--- | :--- |
| `id` | 只读文本 | `能力ID` | 系统自动生成或从数据库读取，不可编辑。 |
| `name` | 单行文本输入框 | `能力名称` | 必填项。为这个能力起一个易于识别的名称。 |
| `type` | 下拉菜单 | `能力类型` | 必选项。当前主要为 `Triggered` (触发型)。未来可扩展 `Activated` (主动型)等。 |
| `abilityTag` | 下拉菜单 | `能力标签` | 必选项。数据源来自一个预定义的枚举（如 `Fanfare`, `LastWords`, `Clash` 等），并支持本地化。 |

---