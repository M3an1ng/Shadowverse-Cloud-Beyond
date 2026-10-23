# GameService事件队列重构计划

## 概述

本文档记录将GameService中的事件队列改为效果队列的重构计划，以解决回合开始事件和回合数显示问题。

## 当前问题

### 1. 回合数架构问题
- ✅ **已完成**: 删除GameState.TurnNumber全局回合数
- ✅ **已完成**: 使用PlayerState.TurnNumber管理每个玩家的回合数
- ✅ **已完成**: 修复所有编译错误，项目构建成功
- ❌ **待修复**: 回合开始事件尚未修复，回合数显示不正常

### 2. 事件队列架构问题
- GameService当前使用事件队列处理游戏逻辑
- 需要改为效果队列以符合统一架构
- 回合开始逻辑需要使用EffectHandler处理

## 重构目标

### 1. 架构统一
- 将GameService中的事件队列改为效果队列
- 使用EffectHandler处理系统操作（回合开始、回合结束等）
- 保持事件驱动架构的一致性

### 2. 回合管理修复
- 修复回合开始事件处理
- 确保回合数正确显示和更新
- 保证前后端数据同步

## 重构计划

### 阶段1: 分析现状
- [ ] 分析GameService中的事件队列实现
- [ ] 识别需要迁移到EffectHandler的系统操作
- [ ] 评估回合开始事件的具体问题

### 阶段2: 设计新架构
- [ ] 设计效果队列架构
- [ ] 定义系统操作的EffectHandler接口
- [ ] 规划回合开始事件的处理流程

### 阶段3: 实施重构
- [ ] 实现效果队列基础设施
- [ ] 迁移系统操作到EffectHandler
- [ ] 修复回合开始事件处理
- [ ] 更新GameService使用效果队列

### 阶段4: 测试验证
- [ ] 单元测试验证
- [ ] 集成测试验证
- [ ] 前端功能测试
- [ ] 回合数显示验证

## 技术要点

### 1. 效果队列设计
```csharp
// 效果队列接口设计
public interface IEffectQueue
{
    Task EnqueueEffectAsync(EffectRequest request);
    Task ProcessQueueAsync();
    bool HasPendingEffects { get; }
}

// 效果请求基类
public abstract class EffectRequest
{
    public Guid RequestId { get; set; }
    public Guid RequestingPlayerId { get; set; }
    public EffectConfig Effect { get; set; }
```

### 2. 系统操作EffectHandler
```csharp
// 回合开始效果处理器
public class TurnStartEffectHandler : EffectHandlerBase<TurnStartEffectParams>
{
    public override async Task<GameStateActionResult> ApplyAsync(
        GameContext context, 
        TurnStartEffectParams parameters)
    {
        // 处理回合开始逻辑
        // 更新玩家回合数
        // 触发回合开始事件
    }
}
```

### 3. 回合数管理
- 使用PlayerState.TurnNumber管理每个玩家的回合数
- 在回合开始时正确递增回合数
- 确保前端能正确显示当前回合数

## 风险评估

### 高风险
- GameService重构可能影响核心游戏逻辑
- 事件队列到效果队列的迁移可能引入新bug

### 中风险
- 回合开始事件的修复可能影响其他回合相关功能
- 前后端数据同步可能出现问题

### 低风险
- 编译错误（已解决）
- 单元测试失败（可通过测试发现）

## 成功标准

### 1. 功能正确性
- 回合数正确显示和更新
- 回合开始事件正常触发
- 游戏流程正常运行

### 2. 架构一致性
- 使用统一的效果队列架构
- 系统操作通过EffectHandler处理
- 事件驱动架构保持一致

### 3. 代码质量
- 编译无错误
- 单元测试通过
- 代码符合架构规范

## 相关文档

- [回合数架构重构](./02-回合数架构重构计划.md)
- [EffectHandler统一架构](../05-游戏系统/05-EffectHandler统一架构.md)
- [游戏事件系统](../05-游戏系统/04-游戏事件系统.md)
- [服务层设计](../02-后端架构/01-服务层设计.md)

## 更新记录

| 日期 | 版本 | 更新内容 | 更新人 |
|------|------|----------|--------|
| 2024-12-19 | 1.0 | 创建重构计划文档 | AI Assistant |
