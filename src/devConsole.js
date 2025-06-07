// 游戏内开发者控制台
// 在游戏界面中提供调试和测试功能
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - 配置管理系统: 文档/05-游戏系统/09-配置管理系统.md
//

import { gameState } from './gameManager.js';
import { connection } from './main.js';

class DevConsole {
    constructor() {
        this.isVisible = false;
        this.commandHistory = [];
        this.historyIndex = -1;
        this.commands = new Map();
        this.initCommands();
        this.createConsoleUI();
        this.bindEvents();
    }

    // 初始化命令
    initCommands() {
        this.commands.set('help', {
            description: '显示所有可用命令',
            execute: () => this.showHelp()
        });

        this.commands.set('clear', {
            description: '清空控制台',
            execute: () => this.clearOutput()
        });

        this.commands.set('gamestate', {
            description: '显示当前游戏状态',
            execute: () => this.showGameState()
        });

        this.commands.set('play', {
            description: '出牌 - 用法: play <cardId> [targetUid]',
            execute: (args) => this.playCard(args)
        });

        this.commands.set('attack', {
            description: '攻击 - 用法: attack <attackerUid> <targetUid>',
            execute: (args) => this.attack(args)
        });

        this.commands.set('evolve', {
            description: '进化 - 用法: evolve <unitUid>',
            execute: (args) => this.evolve(args)
        });

        this.commands.set('endturn', {
            description: '结束回合',
            execute: () => this.endTurn()
        });

        this.commands.set('addcard', {
            description: '添加卡牌到手牌 - 用法: addcard <cardId>',
            execute: (args) => this.addCard(args)
        });

        this.commands.set('setpp', {
            description: '设置PP - 用法: setpp <amount>',
            execute: (args) => this.setPP(args)
        });

        this.commands.set('setep', {
            description: '设置EP - 用法: setep <amount>',
            execute: (args) => this.setEP(args)
        });

        this.commands.set('damage', {
            description: '对目标造成伤害 - 用法: damage <targetUid> <amount>',
            execute: (args) => this.dealDamage(args)
        });

        this.commands.set('heal', {
            description: '治疗目标 - 用法: heal <targetUid> <amount>',
            execute: (args) => this.heal(args)
        });

        this.commands.set('skip', {
            description: '跳到指定回合 - 用法: skip <turnNumber>',
            execute: (args) => this.skipToTurn(args)
        });

        this.commands.set('ai', {
            description: 'AI操作 - 用法: ai <action> (action: play, attack, evolve, endturn)',
            execute: (args) => this.aiAction(args)
        });
    }

    // 创建控制台UI
    createConsoleUI() {
        const consoleHTML = `
            <div id="dev-console" class="dev-console hidden">
                <div class="console-header">
                    <span>🛠️ 开发者控制台</span>
                    <button id="console-close" class="console-close">×</button>
                </div>
                <div class="console-output" id="console-output">
                    <div class="console-welcome">
                        欢迎使用开发者控制台！输入 'help' 查看可用命令。
                    </div>
                </div>
                <div class="console-input-container">
                    <span class="console-prompt">></span>
                    <input type="text" id="console-input" class="console-input" placeholder="输入命令...">
                </div>
            </div>
            <button id="console-toggle" class="console-toggle">🛠️</button>
        `;

        document.body.insertAdjacentHTML('beforeend', consoleHTML);

        // 添加CSS样式
        const style = document.createElement('style');
        style.textContent = `
            .dev-console {
                position: fixed;
                top: 10px;
                right: 10px;
                width: 400px;
                height: 300px;
                background: rgba(0, 0, 0, 0.9);
                border: 1px solid #333;
                border-radius: 8px;
                color: #00ff00;
                font-family: 'Courier New', monospace;
                font-size: 12px;
                z-index: 10000;
                display: flex;
                flex-direction: column;
                box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
            }

            .dev-console.hidden {
                display: none;
            }

            .console-header {
                background: #333;
                padding: 8px 12px;
                border-bottom: 1px solid #555;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-radius: 8px 8px 0 0;
            }

            .console-close {
                background: none;
                border: none;
                color: #ff6b6b;
                font-size: 16px;
                cursor: pointer;
                padding: 0;
                width: 20px;
                height: 20px;
            }

            .console-close:hover {
                background: #ff6b6b;
                color: white;
                border-radius: 50%;
            }

            .console-output {
                flex: 1;
                padding: 8px;
                overflow-y: auto;
                background: rgba(0, 0, 0, 0.8);
            }

            .console-output::-webkit-scrollbar {
                width: 6px;
            }

            .console-output::-webkit-scrollbar-track {
                background: #333;
            }

            .console-output::-webkit-scrollbar-thumb {
                background: #666;
                border-radius: 3px;
            }

            .console-input-container {
                display: flex;
                align-items: center;
                padding: 8px;
                background: #222;
                border-radius: 0 0 8px 8px;
            }

            .console-prompt {
                color: #00ff00;
                margin-right: 8px;
            }

            .console-input {
                flex: 1;
                background: transparent;
                border: none;
                color: #00ff00;
                font-family: inherit;
                font-size: inherit;
                outline: none;
            }

            .console-toggle {
                position: fixed;
                top: 10px;
                right: 10px;
                background: rgba(0, 0, 0, 0.7);
                border: 1px solid #333;
                color: #00ff00;
                padding: 8px 12px;
                border-radius: 4px;
                cursor: pointer;
                z-index: 9999;
                font-size: 14px;
            }

            .console-toggle:hover {
                background: rgba(0, 0, 0, 0.9);
            }

            .console-line {
                margin: 2px 0;
                word-wrap: break-word;
            }

            .console-command {
                color: #00ff00;
            }

            .console-output-text {
                color: #ffffff;
            }

            .console-error {
                color: #ff6b6b;
            }

            .console-success {
                color: #51cf66;
            }

            .console-info {
                color: #74c0fc;
            }

            .console-welcome {
                color: #ffd43b;
                font-style: italic;
            }

            /* 响应式设计 */
            @media (max-width: 768px) {
                .dev-console {
                    width: calc(100vw - 20px);
                    height: 250px;
                    top: 10px;
                    right: 10px;
                    left: 10px;
                }
            }
        `;
        document.head.appendChild(style);
    }

    // 绑定事件
    bindEvents() {
        const toggleBtn = document.getElementById('console-toggle');
        const closeBtn = document.getElementById('console-close');
        const input = document.getElementById('console-input');

        toggleBtn.addEventListener('click', () => this.toggle());
        closeBtn.addEventListener('click', () => this.hide());

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                this.executeCommand(input.value.trim());
                input.value = '';
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                this.navigateHistory(-1);
            } else if (e.key === 'ArrowDown') {
                e.preventDefault();
                this.navigateHistory(1);
            }
        });

        // 全局快捷键 (Ctrl + `)
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === '`') {
                e.preventDefault();
                this.toggle();
            }
        });
    }

    // 显示/隐藏控制台
    toggle() {
        this.isVisible = !this.isVisible;
        const console = document.getElementById('dev-console');
        const toggleBtn = document.getElementById('console-toggle');
        
        if (this.isVisible) {
            console.classList.remove('hidden');
            toggleBtn.style.display = 'none';
            document.getElementById('console-input').focus();
        } else {
            console.classList.add('hidden');
            toggleBtn.style.display = 'block';
        }
    }

    hide() {
        this.isVisible = false;
        document.getElementById('dev-console').classList.add('hidden');
        document.getElementById('console-toggle').style.display = 'block';
    }

    // 输出文本到控制台
    output(text, type = 'output') {
        const outputDiv = document.getElementById('console-output');
        const line = document.createElement('div');
        line.className = `console-line console-${type}`;
        line.textContent = text;
        outputDiv.appendChild(line);
        outputDiv.scrollTop = outputDiv.scrollHeight;
    }

    // 清空输出
    clearOutput() {
        document.getElementById('console-output').innerHTML = '';
        this.output('控制台已清空', 'info');
    }

    // 执行命令
    executeCommand(commandLine) {
        if (!commandLine) return;

        this.commandHistory.push(commandLine);
        this.historyIndex = this.commandHistory.length;

        this.output(`> ${commandLine}`, 'command');

        const [command, ...args] = commandLine.split(' ');
        const cmd = this.commands.get(command.toLowerCase());

        if (cmd) {
            try {
                cmd.execute(args);
            } catch (error) {
                this.output(`错误: ${error.message}`, 'error');
            }
        } else {
            this.output(`未知命令: ${command}。输入 'help' 查看可用命令。`, 'error');
        }
    }

    // 命令历史导航
    navigateHistory(direction) {
        const input = document.getElementById('console-input');
        
        if (direction === -1 && this.historyIndex > 0) {
            this.historyIndex--;
            input.value = this.commandHistory[this.historyIndex];
        } else if (direction === 1 && this.historyIndex < this.commandHistory.length - 1) {
            this.historyIndex++;
            input.value = this.commandHistory[this.historyIndex];
        } else if (direction === 1 && this.historyIndex === this.commandHistory.length - 1) {
            this.historyIndex = this.commandHistory.length;
            input.value = '';
        }
    }

    // 显示帮助
    showHelp() {
        this.output('可用命令:', 'info');
        for (const [name, cmd] of this.commands) {
            this.output(`  ${name} - ${cmd.description}`, 'output');
        }
        this.output('', 'output');
        this.output('快捷键: Ctrl + ` 切换控制台', 'info');
        this.output('使用 ↑↓ 键浏览命令历史', 'info');
    }

    // 显示游戏状态
    showGameState() {
        this.output('当前游戏状态:', 'info');
        this.output(`游戏ID: ${gameState.gameId || '未设置'}`, 'output');
        this.output(`玩家ID: ${gameState.localPlayerId || '未设置'}`, 'output');
        this.output(`游戏阶段: ${gameState.gamePhase}`, 'output');
        this.output(`回合数: ${gameState.turnNumber}`, 'output');
        this.output(`当前玩家: ${gameState.currentPlayerId || '未设置'}`, 'output');
        
        if (gameState.activePlayer) {
            this.output(`手牌数量: ${gameState.activePlayer.hand?.length || 0}`, 'output');
            this.output(`场上单位: ${gameState.activePlayer.field?.length || 0}`, 'output');
            this.output(`当前PP: ${gameState.activePlayer.currentPP || 0}/${gameState.activePlayer.maxPP || 0}`, 'output');
            this.output(`当前EP: ${gameState.activePlayer.currentEP || 0}/${gameState.activePlayer.maxEP || 0}`, 'output');
        }
    }

    // 出牌命令
    async playCard(args) {
        if (args.length < 1) {
            this.output('用法: play <cardId> [targetUid]', 'error');
            return;
        }

        const cardId = args[0];
        const targetUid = args[1] || null;

        try {
            if (connection && connection.state === 'Connected') {
                await connection.invoke('PlayCard', gameState.gameId, gameState.localPlayerId, cardId, targetUid);
                this.output(`出牌命令已发送: 卡牌${cardId}`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`出牌失败: ${error.message}`, 'error');
        }
    }

    // 攻击命令
    async attack(args) {
        if (args.length < 2) {
            this.output('用法: attack <attackerUid> <targetUid>', 'error');
            return;
        }

        const attackerUid = args[0];
        const targetUid = args[1];

        try {
            if (connection && connection.state === 'Connected') {
                await connection.invoke('Attack', gameState.gameId, gameState.localPlayerId, attackerUid, targetUid);
                this.output(`攻击命令已发送: ${attackerUid} -> ${targetUid}`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`攻击失败: ${error.message}`, 'error');
        }
    }

    // 进化命令
    async evolve(args) {
        if (args.length < 1) {
            this.output('用法: evolve <unitUid>', 'error');
            return;
        }

        const unitUid = args[0];

        try {
            if (connection && connection.state === 'Connected') {
                await connection.invoke('Evolve', gameState.gameId, gameState.localPlayerId, unitUid);
                this.output(`进化命令已发送: ${unitUid}`, 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`进化失败: ${error.message}`, 'error');
        }
    }

    // 结束回合
    async endTurn() {
        try {
            if (connection && connection.state === 'Connected') {
                await connection.invoke('EndTurn', gameState.gameId, gameState.localPlayerId);
                this.output('回合结束命令已发送', 'success');
            } else {
                this.output('未连接到服务器', 'error');
            }
        } catch (error) {
            this.output(`结束回合失败: ${error.message}`, 'error');
        }
    }

    // 其他命令的占位符实现
    addCard(args) {
        this.output('addcard 命令暂未实现', 'info');
    }

    setPP(args) {
        this.output('setpp 命令暂未实现', 'info');
    }

    setEP(args) {
        this.output('setep 命令暂未实现', 'info');
    }

    dealDamage(args) {
        this.output('damage 命令暂未实现', 'info');
    }

    heal(args) {
        this.output('heal 命令暂未实现', 'info');
    }

    skipToTurn(args) {
        this.output('skip 命令暂未实现', 'info');
    }

    aiAction(args) {
        this.output('ai 命令暂未实现', 'info');
    }
}

// 创建全局实例
let devConsole = null;

// 初始化开发者控制台
export function initDevConsole() {
    if (!devConsole) {
        devConsole = new DevConsole();
        
        // 添加到全局作用域以便在浏览器控制台中访问
        window.devConsole = devConsole;
        
        console.log('🛠️ 开发者控制台已加载! 按 Ctrl + ` 打开控制台');
    }
    return devConsole;
}

// 导出实例
export { devConsole };
