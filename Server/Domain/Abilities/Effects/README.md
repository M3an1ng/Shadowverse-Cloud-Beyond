# Effect系统使用指南

根据M12设计文档实现的新Effect系统，基于Event驱动的统一架构。

## 核心组件

### 1. IEffectHandler 接口
效果处理器的核心接口，负责执行效果链和单个效果。

### 2. IGameStateActionDispatcher 接口
GameState动作调度器，将效果类型映射到GameState的原子操作方法。

### 3. IValueSourceResolver 接口
值源解析器，负责解析效果配置中的动态值。

### 4. EffectExecutionParams 类
效果执行参数，包含执行效果时所需的所有上下文信息。

### 5. StandardEffectTypes 常量类
定义了标准效果类型，如伤害、治疗、抽牌等。

## 架构设计

### Event驱动的统一架构
```
EffectConfig -> EffectHandler -> GameStateActionDispatcher -> GameState原子操作
                     ↓
              ValueSourceResolver (解析动态值)
```

### 关键特性
1. **配置驱动**: 通过EffectConfig配置效果行为
2. **类型安全**: 使用StandardEffectTypes确保效果类型一致性
3. **动态值解析**: 支持静态值、动态值、公式、变量等多种值源
4. **可扩展**: 通过注册机制支持自定义效果类型
5. **错误处理**: 完善的错误处理和失败策略

## 使用示例

### 基本用法

```csharp
// 1. 创建效果配置
var damageEffect = EffectConfig.Damage(
    TargetConfig.Enemy(TargetScope.Field, 1),
    new ValueConfig { Type = ValueType.Static, StaticValue = 3 }
);

// 2. 创建执行参数
var executionParams = new EffectExecutionParams
{
    CurrentGameState = gameState,
    SourceCard = sourceCard,
    SourcePlayerId = playerId,
    SelectedTargets = targets
};

// 3. 执行效果
var effectHandler = serviceProvider.GetService<IEffectHandler>();
var result = await effectHandler.ExecuteSingleEffectAsync(damageEffect, executionParams);
```

### 动态值解析

```csharp
// 使用动态值 - 基于源卡牌攻击力的伤害
var dynamicDamage = new ValueConfig
{
    Type = ValueType.Dynamic,
    DynamicSource = "source.attack"
};

var effect = EffectConfig.Damage(target, dynamicDamage);
```

### 复合效果

```csharp
// 创建复合效果：造成伤害并抽牌
var subEffects = new List<EffectConfig>
{
    EffectConfig.Damage(enemyTarget, damageAmount),
    EffectConfig.Draw(selfTarget, drawCount)
};

var compositeEffect = EffectConfig.Composite(subEffects);
```

### 自定义效果类型

```csharp
// 注册自定义效果处理器
var dispatcher = serviceProvider.GetService<IGameStateActionDispatcher>();
dispatcher.RegisterHandler("custom_effect", async (gameState, parameters) =>
{
    // 自定义效果逻辑
    return new GameStateActionResult { Success = true };
});
```

## 标准效果类型

### 伤害相关
- `Damage` - 造成伤害
- `Heal` - 治疗
- `ModifyHealth` - 修改生命值

### 属性修改
- `ModifyAttack` - 修改攻击力
- `ModifyStats` - 修改属性
- `AddKeyword` - 添加关键字
- `RemoveKeyword` - 移除关键字

### 卡牌操作
- `DrawCard` - 抽牌
- `DiscardCard` - 弃牌
- `AddCardToHand` - 添加卡牌到手牌
- `SummonUnit` - 召唤单位
- `DestroyUnit` - 摧毁单位

### 资源操作
- `ModifyPP` - 修改PP
- `ModifyEP` - 修改EP
- `GainPPOrb` - 获得PP水晶

### 特殊效果
- `Transform` - 变形
- `Evolve` - 进化
- `Silence` - 沉默
- `Freeze` - 冻结

### 游戏流程
- `EndTurn` - 结束回合
- `SkipTurn` - 跳过回合
- `ExtraTurn` - 额外回合

### 变量操作
- `SetVariable` - 设置变量
- `ModifyVariable` - 修改变量

## 值源类型

### 静态值
```csharp
new ValueConfig { Type = ValueType.Static, StaticValue = 5 }
```

### 动态值
```csharp
new ValueConfig { Type = ValueType.Dynamic, DynamicSource = "source.attack" }
```

支持的动态源：
- `source.attack` - 源卡牌攻击力
- `source.health` - 源卡牌生命值
- `target.attack` - 目标卡牌攻击力
- `player.pp` - 玩家PP
- `player.handcount` - 手牌数量
- `game.turn` - 回合数

### 公式值
```csharp
new ValueConfig { Type = ValueType.Formula, Formula = "source.attack * 2" }
```

### 变量值
```csharp
new ValueConfig { Type = ValueType.Variable, DynamicSource = "damage_bonus" }
```

## 失败处理策略

- `Continue` - 继续执行后续效果
- `Stop` - 停止执行效果链
- `Retry` - 重试执行（暂未实现）

## 依赖注入配置

```csharp
services.AddScoped<IEffectHandler, EffectHandler>();
services.AddScoped<IGameStateActionDispatcher, GameStateActionDispatcher>();
services.AddScoped<IValueSourceResolver, ValueSourceResolver>();
```

## 注意事项

1. **性能考虑**: 避免在效果链中使用过多的动态值解析
2. **错误处理**: 始终检查EffectExecutionResult的Success属性
3. **线程安全**: 当前实现不是线程安全的，需要在调用层确保同步
4. **内存管理**: 大型效果链可能消耗较多内存，注意及时释放资源
5. **调试支持**: 使用SuppressLog参数控制日志输出

## 扩展指南

### 添加新的效果类型
1. 在StandardEffectTypes中定义新常量
2. 在GameStateActionDispatcher中注册处理器
3. 在GameState中实现对应的原子操作方法

### 添加新的值源类型
1. 在ValueSourceType枚举中添加新类型
2. 在ValueSourceResolver中实现解析逻辑

### 自定义失败策略
1. 扩展FailureStrategy枚举
2. 在EffectHandler中实现对应的处理逻辑
