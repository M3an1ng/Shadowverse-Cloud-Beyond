const { test, expect } = require('@playwright/test');

test.describe('BUG-019: Dead Unit Cleanup', () => {
  test('should immediately remove dead units after combat', async ({ page }) => {
    // 导航到游戏页面
    await page.goto('http://localhost:3000');
    
    // 等待游戏加载
    await page.waitForSelector('.game-board', { timeout: 10000 });
    
    // 等待初始状态稳定
    await page.waitForTimeout(2000);
    
    // 截图：初始状态
    await page.screenshot({ path: 'tests/screenshots/bug-019-01-initial.png', fullPage: true });
    
    // 跳过Mulligan阶段
    const mulliganButton = page.locator('button:has-text("完成换牌")');
    if (await mulliganButton.isVisible()) {
      await mulliganButton.click();
      await page.waitForTimeout(1000);
    }
    
    // 等待游戏开始
    await page.waitForSelector('.turn-indicator', { timeout: 5000 });
    await page.waitForTimeout(2000);
    
    // 截图：游戏开始后
    await page.screenshot({ path: 'tests/screenshots/bug-019-02-game-start.png', fullPage: true });
    
    // 等待轮到玩家回合
    await page.waitForFunction(() => {
      const turnIndicator = document.querySelector('.turn-indicator');
      return turnIndicator && turnIndicator.textContent.includes('你的回合');
    }, { timeout: 10000 });
    
    // 打出一张随从卡
    const handCards = page.locator('.hand-card');
    const firstCard = handCards.first();
    if (await firstCard.isVisible()) {
      await firstCard.click();
      await page.waitForTimeout(1000);
    }
    
    // 截图：打出随从后
    await page.screenshot({ path: 'tests/screenshots/bug-019-03-after-play-card.png', fullPage: true });
    
    // 结束回合
    const endTurnButton = page.locator('button:has-text("结束回合")');
    if (await endTurnButton.isVisible()) {
      await endTurnButton.click();
      await page.waitForTimeout(2000);
    }
    
    // 等待AI回合完成
    await page.waitForFunction(() => {
      const turnIndicator = document.querySelector('.turn-indicator');
      return turnIndicator && turnIndicator.textContent.includes('你的回合');
    }, { timeout: 15000 });
    
    // 截图：AI回合后
    await page.screenshot({ path: 'tests/screenshots/bug-019-04-after-ai-turn.png', fullPage: true });
    
    // 检查场上是否有随从
    const playerField = page.locator('.player-field .field-card');
    const aiField = page.locator('.ai-field .field-card');
    
    const playerFieldCount = await playerField.count();
    const aiFieldCount = await aiField.count();
    
    console.log(`Player field count: ${playerFieldCount}, AI field count: ${aiFieldCount}`);
    
    // 如果有随从，进行战斗测试
    if (playerFieldCount > 0 && aiFieldCount > 0) {
      // 点击玩家随从进行攻击
      const playerUnit = playerField.first();
      await playerUnit.click();
      await page.waitForTimeout(500);
      
      // 点击AI随从作为攻击目标
      const aiUnit = aiField.first();
      await aiUnit.click();
      await page.waitForTimeout(2000);
      
      // 截图：攻击后立即状态
      await page.screenshot({ path: 'tests/screenshots/bug-019-05-after-attack.png', fullPage: true });
      
      // 检查死亡单位是否立即被清理
      const playerFieldAfterAttack = await page.locator('.player-field .field-card').count();
      const aiFieldAfterAttack = await page.locator('.ai-field .field-card').count();
      
      console.log(`After attack - Player field: ${playerFieldAfterAttack}, AI field: ${aiFieldAfterAttack}`);
      
      // 验证：死亡单位应该立即被清理，不应该等到下一回合
      // 这里我们检查是否有生命值为0或负数的单位仍在场上
      const deadUnitsOnField = await page.evaluate(() => {
        const fieldCards = document.querySelectorAll('.field-card');
        const deadUnits = [];
        
        fieldCards.forEach(card => {
          const healthElement = card.querySelector('.card-health');
          if (healthElement) {
            const health = parseInt(healthElement.textContent);
            if (health <= 0) {
              deadUnits.push({
                name: card.querySelector('.card-name')?.textContent || 'Unknown',
                health: health
              });
            }
          }
        });
        
        return deadUnits;
      });
      
      console.log('Dead units still on field:', deadUnits);
      
      // 断言：场上不应该有生命值<=0的单位
      expect(deadUnitsOnField.length).toBe(0);
      
      // 截图：最终状态
      await page.screenshot({ path: 'tests/screenshots/bug-019-06-final-state.png', fullPage: true });
    }
    
    // 等待一段时间确保所有异步操作完成
    await page.waitForTimeout(3000);
    
    // 最终截图
    await page.screenshot({ path: 'tests/screenshots/bug-019-07-test-complete.png', fullPage: true });
  });
});
