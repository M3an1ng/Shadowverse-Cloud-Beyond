# EP管理和进化按钮修复验证

## 修复概述

本次修复解决了影之诗卡牌游戏中的两个关键问题：
1. **EP（进化点）管理机制错误**
2. **前端进化按钮显示问题**

## 修复详情

### 1. EP管理机制修复

#### 问题描述
- **错误行为**：EP每回合自动回复1点，不符合影之诗规则
- **正确规则**：进化解锁前EP上限为0，解锁时一次性给予全部EP，之后不再自动回复

#### 修复内容

**A. 游戏初始化修复**
- **文件**：`Server/Services/GameService.cs`
- **位置**：第1204-1205行和1224-1225行
- **修改前**：
  ```csharp
  CurrentEP = 0,
  MaxEP = player1IsFirst ? 2 : 3,    // 错误：初始化时就设置EP上限
  ```
- **修改后**：
  ```csharp
  CurrentEP = 0, // Start with 0 EP
  MaxEP = 0,     // Start with 0 EP limit, will be set when evolution unlocks
  ```

**B. 回合开始EP管理修复**
- **文件**：`Server/Services/GameService.cs`
- **位置**：第428-456行
- **修改前**：每回合自动增加EP的错误逻辑
- **修改后**：正确的进化解锁逻辑
  ```csharp
  // Evolution unlock: Set EP limit and grant full EP on first unlock
  if (shouldUnlockEvolution)
  {
      int maxEpForPlayer = startingPlayerState.IsFirst ? 2 : 3; // P1 gets 2 EP, P2 gets 3 EP
      startingPlayerState.MaxEP = maxEpForPlayer;
      startingPlayerState.CurrentEP = maxEpForPlayer; // Grant full EP immediately on unlock
  }
  // Note: EP does NOT auto-recover after unlock - only consumed through evolution
  ```

### 2. 前端进化按钮显示修复

#### 问题描述
- **属性名不一致**：前端检查 `canEvolveThisTurn` 但后端设置的是 `CanEvolve`
- **UID属性错误**：前端使用 `cardInstance.uid` 但应该使用 `cardInstance.instanceId`
- **DTO属性不匹配**：EP更新事件的属性名不一致

#### 修复内容

**A. 进化条件检查修复**
- **文件**：`src/ui.js`
- **位置**：第857行
- **修改前**：
  ```javascript
  playerState.canEvolveThisTurn === true &&
  ```
- **修改后**：
  ```javascript
  playerState.canEvolve === true && // Fixed: use canEvolve instead of canEvolveThisTurn
  ```

**B. UID属性修复**
- **文件**：`src/ui.js`
- **位置**：第868行
- **修改前**：
  ```javascript
  evolveBtn.dataset.evolveUid = cardInstance.uid;
  ```
- **修改后**：
  ```javascript
  evolveBtn.dataset.evolveUid = cardInstance.instanceId || cardInstance.uid; // Fixed: use instanceId first, fallback to uid
  ```

**C. EP更新事件DTO修复**
- **文件**：`src/gameManager.js`
- **位置**：第1422-1440行
- **修改前**：期望 `NewEpValue` 和 `NewMaxEpValue` 属性
- **修改后**：使用正确的 `CurrentEp` 和 `MaxEp` 属性
  ```javascript
  if (!eventData || !eventData.SourcePlayerId || eventData.CurrentEp === undefined || eventData.MaxEp === undefined) {
      // ...
  }
  playerToUpdate.ep = eventData.CurrentEp; // Fixed: use CurrentEp
  playerToUpdate.maxEP = eventData.MaxEp; // Fixed: use MaxEp
  ```

## 修复后的工作流程

### EP管理流程
1. **游戏开始**：所有玩家EP上限和当前值都为0
2. **进化解锁**：
   - 先手玩家：第5回合开始时，EP上限设为2，当前EP设为2
   - 后手玩家：第4回合开始时，EP上限设为3，当前EP设为3
3. **EP消耗**：每次进化消耗1点EP，不会自动回复
4. **进化权限**：只有当 `MaxEP > 0` 且 `CurrentEP > 0` 时才能进化

### 进化按钮显示流程
1. **条件检查**：
   - 玩家状态存在
   - 是人类玩家的回合
   - 游戏阶段为Main
   - 玩家有可用EP (`playerState.ep > 0`)
   - 玩家有进化权限 (`playerState.canEvolve === true`)
   - 卡牌类型为Follower
   - 卡牌未进化 (`cardInstance.isEvolved === false`)
2. **按钮创建**：使用正确的 `instanceId` 作为标识符
3. **事件处理**：正确接收和处理EP更新事件

## 验证步骤

### 1. EP管理验证
1. **启动游戏**：确认初始EP为0/0
2. **回合推进**：
   - 先手玩家到第5回合：EP应变为2/2
   - 后手玩家到第4回合：EP应变为3/3
3. **进化测试**：
   - 进化一个随从：EP应减少1点
   - 确认EP不会在后续回合自动回复

### 2. 进化按钮验证
1. **按钮显示**：
   - 在满足条件时，场上的随从应显示"Evolve"按钮
   - 不满足条件时，按钮不应显示
2. **按钮功能**：
   - 点击按钮应正确触发进化
   - 进化后按钮应消失
   - UI应正确更新随从状态

### 3. 前后端同步验证
1. **事件传输**：检查浏览器开发者工具中的SignalR事件
2. **状态同步**：确认前端状态与后端状态一致
3. **错误处理**：验证异常情况的处理

## 预期结果

### 成功指标
- [ ] EP在正确的回合解锁并给予正确的数量
- [ ] EP不会自动回复
- [ ] 进化按钮在正确条件下显示
- [ ] 进化功能正常工作
- [ ] 前后端状态完全同步
- [ ] 无控制台错误

### 测试场景
1. **基础进化**：在第5回合（先手）或第4回合（后手）成功进化随从
2. **EP限制**：EP用完后无法继续进化
3. **回合限制**：过早回合无法进化
4. **UI响应**：所有UI元素正确更新

## 相关文件

### 后端文件
- `Server/Services/GameService.cs` - EP管理逻辑
- `Server/Dtos/SignalR/Events/EpUpdateEventDto.cs` - EP更新事件DTO

### 前端文件
- `src/ui.js` - 进化按钮显示逻辑
- `src/gameManager.js` - EP更新事件处理

### 文档文件
- `文档/进化机制完善总结.md` - 进化机制总体设计
- `文档/进化机制测试计划.md` - 详细测试计划

## 注意事项

1. **服务器重启**：修改后端代码后需要重启服务器
2. **浏览器刷新**：修改前端代码后需要刷新浏览器
3. **状态清理**：测试时建议重新开始游戏以确保状态干净
4. **日志监控**：关注控制台日志以发现潜在问题

## 后续改进

1. **视觉效果**：添加进化动画和音效
2. **AI优化**：改进AI的进化决策逻辑
3. **错误处理**：增强异常情况的处理
4. **性能优化**：优化事件处理和UI更新性能
