// 自动化测试脚本
// 使用方法: 在浏览器控制台中运行此脚本

class GameTestAutomation {
    constructor() {
        this.connection = null;
        this.gameId = 'auto-test-' + Date.now();
        this.player1Id = 'test-player-1';
        this.player2Id = 'test-player-2';
        this.testResults = [];
    }

    // 连接到服务器
    async connectToServer(serverUrl = 'http://localhost:5000') {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(`${serverUrl}/gameHub`)
                .build();

            // 注册事件监听器
            this.connection.on("GameStateUpdated", (gameState) => {
                console.log('🎮 游戏状态更新:', gameState);
            });

            this.connection.on("GameEventOccurred", (events) => {
                console.log('⚡ 游戏事件:', events);
            });

            this.connection.on("ErrorOccurred", (error) => {
                console.error('❌ 错误:', error);
            });

            await this.connection.start();
            console.log('✅ 成功连接到服务器');
            return true;
        } catch (error) {
            console.error('❌ 连接失败:', error);
            return false;
        }
    }

    // 创建测试游戏
    async createTestGame() {
        try {
            await this.connection.invoke("CreateGame", this.gameId, this.player1Id);
            console.log(`✅ 创建游戏成功: ${this.gameId}`);
            
            // 等待一秒后加入第二个玩家
            await this.sleep(1000);
            await this.connection.invoke("JoinGame", this.gameId, this.player2Id);
            console.log(`✅ 第二个玩家加入成功`);
            
            return true;
        } catch (error) {
            console.error('❌ 创建游戏失败:', error);
            return false;
        }
    }

    // 测试出牌功能
    async testPlayCard() {
        console.log('🧪 开始测试出牌功能...');
        
        try {
            // 测试出牌
            await this.connection.invoke("PlayCard", this.gameId, this.player1Id, "1", null);
            console.log('✅ 出牌测试通过');
            this.recordTestResult('PlayCard', true, '出牌功能正常');
            return true;
        } catch (error) {
            console.error('❌ 出牌测试失败:', error);
            this.recordTestResult('PlayCard', false, error.toString());
            return false;
        }
    }

    // 测试攻击功能
    async testAttack() {
        console.log('🧪 开始测试攻击功能...');
        
        try {
            // 这里需要实际的单位UID，在真实测试中需要从游戏状态获取
            const attackerUid = 'test-unit-1';
            const targetUid = 'test-target-1';
            
            await this.connection.invoke("Attack", this.gameId, this.player1Id, attackerUid, targetUid);
            console.log('✅ 攻击测试通过');
            this.recordTestResult('Attack', true, '攻击功能正常');
            return true;
        } catch (error) {
            console.error('❌ 攻击测试失败:', error);
            this.recordTestResult('Attack', false, error.toString());
            return false;
        }
    }

    // 测试回合结束
    async testEndTurn() {
        console.log('🧪 开始测试回合结束...');
        
        try {
            await this.connection.invoke("EndTurn", this.gameId, this.player1Id);
            console.log('✅ 回合结束测试通过');
            this.recordTestResult('EndTurn', true, '回合结束功能正常');
            return true;
        } catch (error) {
            console.error('❌ 回合结束测试失败:', error);
            this.recordTestResult('EndTurn', false, error.toString());
            return false;
        }
    }

    // 压力测试 - 快速连续操作
    async stressTest() {
        console.log('🧪 开始压力测试...');
        
        const operations = [];
        const startTime = Date.now();
        
        try {
            // 快速连续出牌
            for (let i = 0; i < 5; i++) {
                operations.push(
                    this.connection.invoke("PlayCard", this.gameId, this.player1Id, "1", null)
                        .catch(e => console.log(`操作 ${i} 失败:`, e))
                );
            }
            
            await Promise.allSettled(operations);
            const endTime = Date.now();
            
            console.log(`✅ 压力测试完成，耗时: ${endTime - startTime}ms`);
            this.recordTestResult('StressTest', true, `完成5次操作，耗时${endTime - startTime}ms`);
            return true;
        } catch (error) {
            console.error('❌ 压力测试失败:', error);
            this.recordTestResult('StressTest', false, error.toString());
            return false;
        }
    }

    // 记录测试结果
    recordTestResult(testName, passed, message) {
        this.testResults.push({
            test: testName,
            passed: passed,
            message: message,
            timestamp: new Date().toISOString()
        });
    }

    // 生成测试报告
    generateReport() {
        console.log('\n📊 测试报告:');
        console.log('='.repeat(50));
        
        let passedCount = 0;
        let totalCount = this.testResults.length;
        
        this.testResults.forEach(result => {
            const status = result.passed ? '✅ PASS' : '❌ FAIL';
            console.log(`${status} ${result.test}: ${result.message}`);
            if (result.passed) passedCount++;
        });
        
        console.log('='.repeat(50));
        console.log(`总计: ${passedCount}/${totalCount} 测试通过`);
        console.log(`成功率: ${((passedCount / totalCount) * 100).toFixed(1)}%`);
        
        return {
            total: totalCount,
            passed: passedCount,
            failed: totalCount - passedCount,
            successRate: (passedCount / totalCount) * 100
        };
    }

    // 运行完整测试套件
    async runFullTestSuite() {
        console.log('🚀 开始运行完整测试套件...');
        
        // 连接服务器
        const connected = await this.connectToServer();
        if (!connected) return;
        
        // 创建游戏
        const gameCreated = await this.createTestGame();
        if (!gameCreated) return;
        
        // 等待游戏初始化
        await this.sleep(2000);
        
        // 运行各项测试
        await this.testPlayCard();
        await this.sleep(1000);
        
        await this.testEndTurn();
        await this.sleep(1000);
        
        await this.stressTest();
        await this.sleep(1000);
        
        // 生成报告
        const report = this.generateReport();
        
        console.log('\n🎯 测试套件执行完成!');
        return report;
    }

    // 工具方法：等待
    sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    // 清理资源
    async cleanup() {
        if (this.connection) {
            await this.connection.stop();
            console.log('🧹 连接已清理');
        }
    }
}

// 快速测试函数
async function quickTest() {
    const tester = new GameTestAutomation();
    try {
        const report = await tester.runFullTestSuite();
        return report;
    } finally {
        await tester.cleanup();
    }
}

// 导出到全局作用域
window.GameTestAutomation = GameTestAutomation;
window.quickTest = quickTest;

console.log(`
🛠️ 游戏自动化测试工具已加载!

使用方法:
1. 快速测试: quickTest()
2. 自定义测试:
   const tester = new GameTestAutomation();
   await tester.runFullTestSuite();

3. 单项测试:
   const tester = new GameTestAutomation();
   await tester.connectToServer();
   await tester.createTestGame();
   await tester.testPlayCard();
`);
