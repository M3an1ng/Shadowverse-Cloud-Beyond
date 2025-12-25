# 03. DSL类型系统与后续节点规范

**版本: 1.0**

**作者: AI 技术总监**

**更新日期: 2025-08-24**

## 1. 概述

本文档定义了DSL编辑器中所有可能出现的数据类型，以及每种类型可以进行的后续操作。这是DSL编辑器`getAvailableOptions`方法的核心设计依据。

## 2. 基础类型分类

### 2.1 标量类型 (Scalar Types)

#### 2.1.1 数值类型
- **Int32**: 32位整数
- **Int64**: 64位整数  
- **Single**: 单精度浮点数
- **Double**: 双精度浮点数

**后续操作:**
- 比较操作符: `==`, `!=`, `>`, `>=`, `<`, `<=`
- 算术操作符: `+`, `-`, `*`, `/`, `%`
- 逻辑操作符: `&&`, `||` (用于组合条件)

#### 2.1.2 文本类型
- **String**: 字符串
- **Guid**: 全局唯一标识符（作为文本处理）

**后续操作:**
- 比较操作符: `==`, `!=`
- 字符串操作符: `contains`, `startsWith`, `endsWith`
- 逻辑操作符: `&&`, `||`

#### 2.1.3 布尔类型
- **Boolean**: 布尔值

**后续操作:**
- 比较操作符: `==`, `!=`
- 逻辑操作符: `&&`, `||`, `!`

### 2.2 文本类型扩展 (Extended Text Types)

#### 2.2.1 枚举类型（作为文本处理）
- **CardType**: 卡牌类型 (Follower, Spell, Amulet)
- **Rarity**: 稀有度 (Bronze, Silver, Gold, Legendary)
- **Craft**: 职业 (Forestcraft, Swordcraft, Runecraft, etc.)
- **CardKeyword**: 卡牌关键字 (Rush, Ward, Storm, etc.)

**重要说明**: 枚举类型在DSL中应被视为**文本类型**进行处理，因为它们的核心操作（比较）与字符串兼容。

**后续操作:**
- 比较操作符: `==`, `!=`
- 字符串操作符: `contains` (用于关键字列表检查)
- 逻辑操作符: `&&`, `||`

### 2.3 对象类型 (Object Types)

#### 2.3.1 游戏实体
- **CardInstance**: 卡牌实例
- **PlayerState**: 玩家状态
- **GameState**: 游戏状态
- **AbilityContext**: 技能上下文

**后续操作:**
- 属性访问: `.propertyName`
- 比较操作符: `==`, `!=` (对象引用比较)
- 逻辑操作符: `&&`, `||`

### 2.4 集合类型 (Collection Types)

#### 2.4.1 列表类型
- **List&lt;T&gt;**: 泛型列表，如 `List<CardInstance>`

**后续操作:**
- 列表查询: `.query(condition)` (筛选集合元素)
- 函数调用: `count(collection)`, `sum(collection)`, `min(collection)`, `max(collection)`, `contains(collection, value)`, `isEmpty(collection)`
- 比较操作符: `==`, `!=` (集合比较)
- 逻辑操作符: `&&`, `||`

**重要说明**: DSL中的列表操作主要通过函数实现，而不是方法链。`.query()`是唯一的列表方法操作。

## 3. 类型推断规则

### 3.1 属性访问类型推断
```
对象.属性 → 属性的声明类型
例如: player.health → Int32
     player.cards → List<CardInstance>
     card.type → CardType
```

### 3.2 函数调用类型推断
```
count(List<T>) → Int32
sum(List<T>) → Object (数值类型)
min(List<T>) → Object
max(List<T>) → Object
contains(List<T>, value) → Boolean
isEmpty(List<T>) → Boolean
List<T>.query(condition) → List<T> (唯一的列表方法)
```

### 3.3 操作符类型推断
```
数值 比较操作符 数值 → Boolean
字符串 比较操作符 字符串 → Boolean
布尔值 逻辑操作符 布尔值 → Boolean
```

## 4. 后续节点决策矩阵

### 4.1 基于当前类型的后续选项

| 当前类型 | 可用操作类别 | 具体选项 |
|---------|-------------|----------|
| Int32/Int64/Single/Double | 操作符 + 函数 | ==, !=, >, >=, <, <=, +, -, *, /, %; abs() |
| String/Guid | 操作符 + 函数 | ==, !=; contains(), isEmpty() |
| Boolean | 操作符 + 函数 | ==, !=, &&, \|\|, !; if() |
| CardType/Rarity/Craft | 操作符 | ==, != (作为文本类型处理) |
| CardInstance/PlayerState | 属性访问 | 根据类型元数据的properties |
| List&lt;T&gt; | 函数 + 查询 | count(), sum(), min(), max(), contains(), isEmpty(); .query() |

### 4.2 特殊情况处理

#### 4.2.1 操作符右侧
当用户选择操作符后，右侧应该提供：
- **静态值输入**: 根据左侧类型提供相应的输入控件
- **上下文对象**: 可以选择其他上下文对象
- **函数调用**: 返回兼容类型的函数

#### 4.2.2 列表查询内部
在`.query(condition)`内部，上下文切换为：
- **self**: 代表列表中的当前元素，类型为T
- 可以访问T类型的所有属性和方法

## 5. 实现指导原则

### 5.1 类型检查优先级
1. **精确匹配**: 类型完全相同
2. **兼容匹配**: 数值类型之间的兼容性
3. **继承匹配**: 对象类型的继承关系
4. **通用匹配**: Object类型的通用兼容性

### 5.2 选项排序规则
1. **最常用操作**: 根据使用频率排序
2. **类型相关性**: 最相关的操作优先
3. **语义逻辑**: 符合用户思维习惯的顺序

### 5.3 错误处理策略
1. **类型不匹配**: 提供友好的错误提示
2. **缺失元数据**: 降级到基础操作
3. **循环引用**: 设置递归深度限制

## 6. 扩展性考虑

### 6.1 新类型添加
- 在相应的类型分类中添加定义
- 更新后续操作矩阵
- 实现相应的类型检查方法

### 6.2 新操作添加
- 定义操作的输入输出类型
- 更新决策矩阵
- 实现操作的渲染和表达式生成

## 7. 下一步实施计划

1. **实现类型检查方法**: `isNumericType()`, `isEnumType()`, `isObjectType()`, `isCollectionType()`
2. **重构getAvailableOptions**: 基于此规范完全重写
3. **实现静态值输入**: 支持各种类型的值输入
4. **完善操作符支持**: 实现所有定义的操作符
5. **添加枚举支持**: 从元数据获取枚举值选项

## 8. 具体实现示例

### 8.1 类型检查方法实现
```javascript
// 基础类型检查
isBaseType(typeName) {
    const baseTypes = ['Int32', 'Int64', 'Single', 'Double', 'Boolean', 'String', 'Guid'];
    return baseTypes.includes(typeName);
}

// 数值类型检查
isNumericType(typeName) {
    const numericTypes = ['Int32', 'Int64', 'Single', 'Double'];
    return numericTypes.includes(typeName);
}

// 文本类型检查（包括枚举和GUID）
isTextType(typeName) {
    const textTypes = ['String', 'Guid', 'CardType', 'Rarity', 'Craft', 'CardKeyword'];
    return textTypes.includes(typeName);
}

// 集合类型检查
isCollectionType(typeName) {
    return typeName && typeName.startsWith('List<') && typeName.endsWith('>');
}

// 获取集合内部类型
getCollectionInnerType(typeName) {
    if (this.isCollectionType(typeName)) {
        return typeName.slice(5, -1); // 移除 "List<" 和 ">"
    }
    return null;
}
```

### 8.2 getAvailableOptions重构示例
```javascript
getAvailableOptions() {
    const nodeType = this.nodeTree?.type;
    const returnType = this.nodeTree?.returnType;

    // 根节点：显示上下文对象
    if (!this.nodeTree) {
        return this.getContextObjects();
    }

    // 上下文对象：显示属性
    if (nodeType === "context") {
        return this.getContextProperties();
    }

    // 属性节点：根据返回类型决定后续操作
    if (nodeType === "property") {
        return this.getOptionsForType(returnType);
    }

    // 操作符节点：需要右操作数
    if (nodeType === "operator") {
        return this.getRightOperandOptions();
    }

    return [];
}

getOptionsForType(typeName) {
    let options = [];

    // 数值类型：提供数值操作符和函数
    if (this.isNumericType(typeName)) {
        options.push(...this.getNumericOperators());
        options.push(...this.getNumericFunctions());
    }

    // 文本类型（包括枚举）：提供文本操作符和函数
    else if (this.isTextType(typeName)) {
        options.push(...this.getTextOperators());
        options.push(...this.getTextFunctions());
    }

    // 布尔类型：提供布尔操作符和函数
    else if (typeName === 'Boolean') {
        options.push(...this.getBooleanOperators());
        options.push(...this.getBooleanFunctions());
    }

    // 集合类型：提供集合函数和查询操作
    else if (this.isCollectionType(typeName)) {
        options.push(...this.getCollectionFunctions());
        options.push(...this.getListQueryOperations());
    }

    // 对象类型：提供属性访问
    else {
        options.push(...this.getObjectProperties(typeName));
    }

    return options;
}
```

### 8.3 静态值输入实现示例
```javascript
getRightOperandOptions() {
    const leftType = this.nodeTree.leftOperandType;
    let options = [];

    // 添加静态值输入选项
    options.push({
        type: "staticValue",
        value: "input",
        label: "输入值",
        description: `输入${this.getTypeDisplayName(leftType)}类型的值`,
        inputType: this.getInputTypeForType(leftType)
    });

    // 添加上下文对象选项
    options.push(...this.getCompatibleContextObjects(leftType));

    // 添加兼容函数选项
    options.push(...this.getCompatibleFunctions(leftType));

    return options;
}

getInputTypeForType(typeName) {
    if (this.isNumericType(typeName)) return "number";
    if (typeName === "String") return "text";
    if (typeName === "Boolean") return "boolean";
    if (this.isEnumType(typeName)) return "enum";
    return "text";
}
```

## 9. 测试用例规范

### 9.1 类型推断测试
```javascript
// 测试用例1: 基础属性访问
// 输入: player.health
// 预期: returnType = "Int32", 后续选项包含数值操作符

// 测试用例2: 列表属性访问
// 输入: player.cards
// 预期: returnType = "List<CardInstance>", 后续选项包含列表操作

// 测试用例3: 列表查询操作
// 输入: player.cards.query(self.cost > 5)
// 预期: returnType = "List<CardInstance>", 后续选项包含集合函数
```

### 9.2 操作符测试
```javascript
// 测试用例4: 数值比较
// 输入: player.health >
// 预期: 右侧提供数值输入、兼容属性、兼容函数

// 测试用例5: 文本类型比较（包括枚举）
// 输入: card.type ==
// 预期: 右侧提供文本输入、兼容属性、兼容函数
```

---

**注意**: 此文档是DSL编辑器开发的核心规范，任何类型系统相关的修改都应该先更新此文档，然后再进行代码实现。
